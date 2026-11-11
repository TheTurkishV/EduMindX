using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using EduMindX.SmartBoard.Core;
using Microsoft.Win32;

namespace EduMindX.SmartBoard;

/// <summary>
/// Akıllı tahtanın ana orkestratörü ve denetleyicisi (Board Controller).
/// UI pencerelerini (LockWindow, UnlockedBar, AnnouncementWindow),
/// güvenlik korumalarını (ShellGuard, KeyboardBlocker, VirtualDesktops),
/// durum motorunu (LockEngine) ve bulut bağlantısını (DeviceConnection) koordine eder.
/// </summary>
public sealed class BoardController : IDisposable
{
	private const int MaxPendingEvents = 500;
	private const int EventBatchSize = 200;
	private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30L);
	private static readonly TimeSpan PlanRefreshInterval = TimeSpan.FromMinutes(15L);
	private static readonly string AgentVersion = AppInfo.Version;

	private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
	private readonly BoardStorage _storage = new BoardStorage();
	private readonly BoardClock _clock = new BoardClock();
	private readonly PasswordGate _gate = new PasswordGate();
	private readonly KeyboardBlocker _keys = new KeyboardBlocker();

	// Her saniye tetiklenen ana zamanlayıcı döngüsü
	private readonly DispatcherTimer _tick = new DispatcherTimer
	{
		Interval = TimeSpan.FromSeconds(1L)
	};

	private readonly Stopwatch _sinceHeartbeat = new Stopwatch();
	private readonly Stopwatch _sincePlan = new Stopwatch();
	private readonly Stopwatch _sinceStart = Stopwatch.StartNew();
	private readonly UpdateCheckSchedule _updateCheck = new UpdateCheckSchedule();

	private BoardConfig _config = new BoardConfig();
	private LockEngine _engine = new LockEngine(new LockPlan());
	private List<BoardEvent> _pending = new List<BoardEvent>();
	private HandledCommands _handledCommands = new HandledCommands();
	private UsedUnlockCodes _usedCodes = new UsedUnlockCodes();

	private bool _hasPlan;
	private bool _flushing;
	private bool _hookErrorLogged;

	private LockWindow? _lockWindow;
	private UnlockedBar? _bar;
	private AnnouncementWindow? _announcement;
	private DeviceConnection? _connection;
	private VirtualDesktops? _desktops;
	private ShellGuard? _shell;

	private DateTime NowLocal => _clock.LocalNow(_engine.Plan.UtcOffsetMinutes);

	/// <summary>
	/// Tahta kontrolcüsünü başlatır, yerel ayarları yükler ve servisleri ayağa kaldırır.
	/// </summary>
	public void Start()
	{
		_config = _storage.LoadConfig();
		SignedPlan? signedPlan = _storage.LoadSignedPlan();
		LockPlan? lockPlan = PlanVerifier.Verify(signedPlan, _config.PlanPublicKey, _config.DeviceGuid);

		if (signedPlan != null && lockPlan == null)
		{
			_storage.Log("Diskteki plan dogrulanamadi (imza gecersiz); kullanilmadi");
		}

		_hasPlan = lockPlan != null;
		_engine = new LockEngine(lockPlan ?? new LockPlan());
		_pending = _storage.LoadEvents();
		_handledCommands = _storage.LoadHandledCommands();
		_usedCodes = _storage.LoadUsedCodes();

		// Pencereleri ve güvenlik kancalarını başlat
		_lockWindow = new LockWindow(SubmitPin, RequestQrAsync);
		_bar = new UnlockedBar(TeacherLock, _storage.LoadBarPosition(), _storage.SaveBarPosition);
		_desktops = new VirtualDesktops(_storage.Log);
		_shell = new ShellGuard(_storage.Log);

		// Dokunmatik hareketleri ve tablet modunu sınırla
		TouchGestures.Disable(_storage.Log);
		TabletMode.PreventAutoSwitch(_storage.Log);
		EnsureKeyboardHook();

		_lockWindow.Show();
		SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
		SystemEvents.SessionSwitch += OnSessionSwitch;

		_tick.Tick += (object? _, EventArgs _) =>
		{
			OnTick();
		};
		_tick.Start();
		OnTick();

		_storage.Log($"Basladi v{AgentVersion} (plan {(_hasPlan ? "diskte var" : "yok")})");

		if (!_config.IsComplete)
		{
			_storage.Log("config.dat eksik ya da okunamadi: tahta sunucuya baglanamaz (kurulum yeniden calistirilmali)");
			return;
		}

		// Sunucu SignalR bağlantısını kur
		_connection = new DeviceConnection(_config, _storage);
		_connection.Connected += () =>
		{
			_dispatcher.InvokeAsync((Func<Task>)OnConnectedAsync);
		};
		_connection.PlanChanged += () =>
		{
			_dispatcher.InvokeAsync((Func<Task>)RefreshPlanAsync);
		};
		_connection.CommandReceived += (BoardCommand command) =>
		{
			_dispatcher.InvokeAsync(() => HandleCommandAsync(command));
		};
		_connection.StartAsync();
	}

	/// <summary>
	/// Her saniye çalışan ana denetim döngüsü.
	/// Zaman aşımı, kilit durumları, kalp atışı ve güncelleme şartlarını denetler.
	/// </summary>
	private void OnTick()
	{
		if (!_keys.IsInstalled)
		{
			EnsureKeyboardHook();
		}

		// Kilit motorunun zaman kontrolünü yap
		Record(_engine.Tick(NowLocal, _clock.UtcNow));
		RefreshUi();

		bool online = _connection?.IsConnected ?? false;

		// Güncelleme kontrolü (Yalnızca kilitliyken, ders dışındayken ve çevrimiçiyken)
		if (_updateCheck.ShouldCheck(_sinceStart.Elapsed, _engine.IsLocked, _engine.CurrentLesson(NowLocal) != null, online))
		{
			UpdateTask.TryStart(_storage.Log);
		}

		if (online)
		{
			// 30 saniyede bir Heartbeat gönder
			if (!_sinceHeartbeat.IsRunning || _sinceHeartbeat.Elapsed >= HeartbeatInterval)
			{
				HeartbeatAsync();
			}

			// 15 dakikada bir ders planını yenile
			if (!_sincePlan.IsRunning || _sincePlan.Elapsed >= PlanRefreshInterval)
			{
				RefreshPlanAsync();
			}
		}
	}

	/// <summary>
	/// Kilitli veya açık olma durumuna göre arayüzü ve Windows kabuk durumunu günceller.
	/// </summary>
	private void RefreshUi()
	{
		if (_lockWindow == null || _bar == null || _shell == null)
		{
			return;
		}

		DateTime nowLocal = NowLocal;
		if (TabletMode.TurnOffIfOn(_storage.Log))
		{
			_shell.Restart("tablet modu kapatildi");
		}

		if (_engine.IsLocked)
		{
			// Kilitli Mod: Klavye engeli aktif, Windows Gezgini askıya alınmış
			_keys.Enabled = true;
			if (_bar.IsVisible)
			{
				_bar.Hide();
			}
			if (!_lockWindow.IsVisible)
			{
				_lockWindow.Show();
			}

			KeepOnCurrentDesktop(_lockWindow);
			_lockWindow.Render(BuildState(nowLocal));

			_lockWindow.EnsureOnTop(_announcement == null || !_announcement.IsVisible);
			_announcement?.EnsureOnTop();

			_shell.Freeze();
			_shell.ReportForeignForeground();
		}
		else
		{
			// Açık Mod: Klavye engeli kalktı, Windows Gezgini çözüldü, yüzen çubuk görünür
			_keys.Enabled = false;
			_shell.Thaw();

			if (_lockWindow.IsVisible)
			{
				_lockWindow.ResetPanels();
				_lockWindow.Hide();
			}

			_bar.Render(nowLocal, _engine.UnlockedUntil!.Value, _engine.IsWarning(nowLocal));
			if (!_bar.IsVisible)
			{
				_bar.Show();
			}

			KeepOnCurrentDesktop(_bar);
			_bar.EnsureOnTop();
			_announcement?.EnsureOnTop();
		}
	}

	private void KeepOnCurrentDesktop(Window window)
	{
		if (_desktops == null)
		{
			return;
		}
		if (_shell != null && !_shell.Frozen)
		{
			_desktops.KeepOnCurrent(window);
			if (_announcement != null && _announcement.IsVisible)
			{
				_desktops.KeepOnCurrent(_announcement);
			}
		}
	}

	private LockScreenState BuildState(DateTime now)
	{
		LockPlan plan = _engine.Plan;
		string identity = string.Join(" · ", new string[3] { plan.BranchName ?? string.Empty, plan.EducationLevelName ?? string.Empty, plan.DeviceName }
			.Where((string s) => !string.IsNullOrWhiteSpace(s)));

		string connectionStatus;
		if (!_config.IsComplete)
		{
			connectionStatus = "● Kurulum tamamlanmamış (ayar dosyası eksik)";
		}
		else
		{
			DeviceConnection? connection = _connection;
			if (connection == null || !connection.IsConnected)
			{
				connectionStatus = _hasPlan ? "● Çevrimdışı — son kayıtlı plan kullanılıyor" : "● Sunucuya bağlanılıyor…";
			}
			else
			{
				connectionStatus = "● Çevrimiçi";
			}
		}

		return new LockScreenState(
			now,
			_connection?.IsConnected ?? false,
			_engine.ServiceEnabled,
			!string.IsNullOrEmpty(plan.UnlockPasswordHash) || _usedCodes.Available(plan, now).Count > 0,
			_engine.CurrentLesson(now),
			_engine.NextLesson(now),
			identity,
			connectionStatus,
			_gate.LockoutRemaining
		);
	}

	/// <summary>
	/// Kilit ekranından girilen 8 haneli PIN kodunu doğrular.
	/// </summary>
	private PasswordResult SubmitPin(string pin)
	{
		DateTime nowLocal = NowLocal;
		DateTime utcNow = _clock.UtcNow;

		IReadOnlyList<string> codeHashes = _engine.ServiceEnabled ? _usedCodes.Available(_engine.Plan, nowLocal) : Array.Empty<string>();
		PinCheck pinCheck = _gate.CheckPin(pin, codeHashes, _engine.Plan.UnlockPasswordHash);
		PasswordResult result = pinCheck.Result;

		if (result == PasswordResult.Success)
		{
			string? codeHash = pinCheck.CodeHash;
			if (codeHash != null)
			{
				// Tek kullanımlık kod ile açıldı: Kodu kullanıldı olarak işaretle
				_usedCodes.MarkUsed(codeHash, nowLocal);
				_storage.SaveUsedCodes(_usedCodes);
				Record(_engine.Unlock(UnlockReason.Code, nowLocal, utcNow));
			}
			else
			{
				// Ana idare parolası ile açıldı
				Record(_engine.Unlock(UnlockReason.Password, nowLocal, utcNow));
			}
		}
		else if (result == PasswordResult.Wrong || result == PasswordResult.LockedOut)
		{
			// Başarısız deneme olayını kaydet
			Record(new BoardEvent("FAILED_ATTEMPT", null, _engine.CurrentLesson(nowLocal) != null, utcNow));
		}

		RefreshUi();
		return result;
	}

	/// <summary>
	/// Öğretmen yüzen çubuktan "Kilitle" butonuna bastığında çağrılır.
	/// </summary>
	private void TeacherLock()
	{
		Record(_engine.Lock(LockReason.Teacher, NowLocal, _clock.UtcNow));
		RefreshUi();
	}

	/// <summary>
	/// Güvenlik veya kilit olayını yerel günlüğe yazar ve sunucuya aktarılmak üzere kuyruğa ekler.
	/// </summary>
	private void Record(BoardEvent? boardEvent)
	{
		if (boardEvent == null) return;

		_storage.Log($"{boardEvent.EventType} {boardEvent.Reason} ders={boardEvent.InLesson}");
		_pending.Add(boardEvent);

		if (!_flushing && _pending.Count > MaxPendingEvents)
		{
			_pending.RemoveRange(0, _pending.Count - MaxPendingEvents);
		}

		_storage.SaveEvents(_pending);
		FlushEventsAsync();
	}

	/// <summary>
	/// Biriken olay kuyruğunu SignalR üzerinden sunucuya toplu gönderir.
	/// </summary>
	private async Task FlushEventsAsync()
	{
		if (_flushing) return;
		DeviceConnection? connection = _connection;
		if (connection == null || !connection.IsConnected) return;

		_flushing = true;
		try
		{
			while (_pending.Count > 0 && _connection != null && _connection.IsConnected)
			{
				List<BoardEvent> batch = _pending.Take(EventBatchSize).ToList();
				int writtenCount = await _connection.ReportEventsAsync(batch);
				if (writtenCount < batch.Count)
				{
					_storage.Log($"Sunucu {batch.Count} olaydan {writtenCount} tanesini yazdi (tahta iptal edilmis olabilir)");
				}
				_pending.RemoveRange(0, batch.Count);
				_storage.SaveEvents(_pending);
			}
		}
		catch (Exception ex)
		{
			_storage.Log("Olaylar gonderilemedi: " + ex.Message);
		}
		finally
		{
			_flushing = false;
		}
	}

	/// <summary>
	/// Sunucuya ilk bağlantı kurulduğunda senkronizasyonu başlatır.
	/// </summary>
	private async Task OnConnectedAsync()
	{
		try
		{
			await RefreshPlanAsync();
			if (_connection != null)
			{
				foreach (BoardCommand item in await _connection.FetchPendingAsync())
				{
					await HandleCommandAsync(item);
				}
			}
			await HeartbeatAsync();
			await FlushEventsAsync();
		}
		catch (Exception ex)
		{
			_storage.Log("Baglanti sonrasi esitleme basarisiz: " + ex.Message);
		}
	}

	/// <summary>
	/// Sunucudan güncel ders planını çeker, ECDSA imzasını doğrular ve saatini eşitler.
	/// </summary>
	private async Task RefreshPlanAsync()
	{
		DeviceConnection? connection = _connection;
		if (connection == null || !connection.IsConnected) return;

		_sincePlan.Restart();
		try
		{
			SignedPlan? signedPlan = await _connection.GetLockPlanAsync();
			if (signedPlan == null)
			{
				_storage.Log("Sunucu plan vermedi (tahta iptal edilmis olabilir); son plan kullaniliyor");
				return;
			}

			LockPlan? lockPlan = PlanVerifier.Verify(signedPlan, _config.PlanPublicKey, _config.DeviceGuid);
			if (lockPlan == null)
			{
				_storage.Log("Sunucudan gelen plan dogrulanamadi (imza/tahta uyusmuyor); son plan kullaniliyor");
				return;
			}

			// Donanım destekli saati sunucu UTC saati ile eşitle
			_clock.Sync(lockPlan.ServerUtc);
			Record(_engine.UpdatePlan(lockPlan, NowLocal, _clock.UtcNow));
			_storage.SaveSignedPlan(signedPlan);
			_hasPlan = true;
			RefreshUi();
		}
		catch (Exception ex)
		{
			_storage.Log("Plan alinamadi: " + ex.Message);
		}
	}

	private async Task HeartbeatAsync()
	{
		DeviceConnection? connection = _connection;
		if (connection == null || !connection.IsConnected) return;

		_sinceHeartbeat.Restart();
		try
		{
			DateTime serverUtc = await connection.HeartbeatAsync(_engine.IsLocked, AgentVersion);
			_clock.Sync(serverUtc);
		}
		catch (Exception ex)
		{
			_storage.Log("Heartbeat basarisiz: " + ex.Message);
		}
	}

	/// <summary>
	/// Sunucudan SignalR üzerinden gelen uzaktan komutları işler.
	/// </summary>
	private async Task HandleCommandAsync(BoardCommand command)
	{
		DateTime nowLocal = NowLocal;
		DateTime utcNow = _clock.UtcNow;
		bool success = true;
		string? message = null;

		// Replay Attack engelleme: Komut daha önce işlendiyse tekrar çalıştırma
		if (!_handledCommands.TryBegin(command.RowGuid, utcNow))
		{
			_storage.Log("Komut " + command.CommandType + " daha once uygulanmisti; yalniz onay yineleniyor");
			await AckAsync(command.RowGuid, success: true, "Daha önce uygulandı");
			return;
		}
		_storage.SaveHandledCommands(_handledCommands);

		switch (command.CommandType)
		{
			case "Lock":
			{
				BoardEvent? boardEvent = _engine.Lock(LockReason.Remote, nowLocal, utcNow);
				if (boardEvent != null)
				{
					Record(boardEvent with { CommandGuid = command.RowGuid });
				}
				break;
			}

			case "Unlock":
			{
				if (!_hasPlan)
				{
					success = false;
					message = "Tahtanın planı henüz alınmadı";
					break;
				}
				UnlockReason reason = (ReadPayloadString(command.PayloadJson, "via") == "QR") ? UnlockReason.Qr : UnlockReason.Remote;
				DateTime? requestedUntilLocal = ReadPayloadUtc(command.PayloadJson, "untilUtc")?.AddMinutes(_engine.Plan.UtcOffsetMinutes);
				BoardEvent? boardEvent = _engine.Unlock(reason, nowLocal, utcNow, requestedUntilLocal);
				if (boardEvent == null)
				{
					success = false;
					message = "Tahta hizmet dışı";
				}
				else
				{
					Record(boardEvent with { CommandGuid = command.RowGuid });
				}
				break;
			}

			case "RefreshSchedule":
				await RefreshPlanAsync();
				break;

			case "ShowAnnouncement":
			{
				string? text = ReadPayloadString(command.PayloadJson, "message")?.Trim();
				if (string.IsNullOrEmpty(text))
				{
					success = false;
					message = "Duyuru metni boş";
				}
				else
				{
					(_announcement ??= new AnnouncementWindow()).ShowMessage(text, nowLocal);
				}
				break;
			}

			case "SetServiceState":
			{
				bool? enabled = ParseEnabled(command.PayloadJson);
				if (enabled.HasValue)
				{
					Record(_engine.SetServiceEnabled(enabled.Value, nowLocal, utcNow));
					await RefreshPlanAsync();
				}
				else
				{
					success = false;
					message = "Geçersiz komut verisi";
				}
				break;
			}

			case "Reboot":
			case "Shutdown":
				if (!Shutdown((command.CommandType == "Reboot") ? "/r" : "/s"))
				{
					success = false;
					message = "Kapatma başlatılamadı";
				}
				break;

			default:
				success = false;
				message = "Bu tahta sürümü komutu desteklemiyor";
				break;
		}

		_storage.Log("Komut " + command.CommandType + ": " + (success ? "uygulandi" : message));
		RefreshUi();
		await AckAsync(command.RowGuid, success, message);
	}

	private async Task AckAsync(Guid commandGuid, bool success, string? message)
	{
		if (_connection == null) return;
		try
		{
			await _connection.AckAsync(commandGuid, success, message);
		}
		catch (Exception ex)
		{
			_storage.Log("Komut onayi gonderilemedi: " + ex.Message);
		}
	}

	private static string? ReadPayloadString(string? payloadJson, string name)
	{
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(payloadJson ?? "{}");
			if (jsonDocument.RootElement.ValueKind != JsonValueKind.Object)
			{
				return null;
			}
			foreach (JsonProperty item in jsonDocument.RootElement.EnumerateObject())
			{
				if (string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase) && item.Value.ValueKind == JsonValueKind.String)
				{
					return item.Value.GetString();
				}
			}
			return null;
		}
		catch (JsonException)
		{
			return null;
		}
	}

	private static DateTime? ReadPayloadUtc(string? payloadJson, string name)
	{
		if (!DateTime.TryParse(ReadPayloadString(payloadJson, name), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var result))
		{
			return null;
		}
		return DateTime.SpecifyKind(result, DateTimeKind.Unspecified);
	}

	private async Task<QrDisplay?> RequestQrAsync()
	{
		DeviceConnection? connection = _connection;
		if (connection == null || !connection.IsConnected)
		{
			return null;
		}
		try
		{
			UnlockChallenge? unlockChallenge = await connection.CreateUnlockChallengeAsync();
			if (unlockChallenge == null)
			{
				return null;
			}
			TimeSpan remaining = unlockChallenge.ExpiresAt.ToUniversalTime() - _clock.UtcNow;
			return (remaining > TimeSpan.Zero) ? new QrDisplay(unlockChallenge.Url, remaining) : null;
		}
		catch (Exception ex)
		{
			_storage.Log("QR kodu alinamadi: " + ex.Message);
			return null;
		}
	}

	private static bool? ParseEnabled(string? payloadJson)
	{
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(payloadJson ?? "{}");
			return jsonDocument.RootElement.TryGetProperty("enabled", out JsonElement value) ? new bool?(value.GetBoolean()) : null;
		}
		catch (Exception)
		{
			return null;
		}
	}

	private bool Shutdown(string mode)
	{
		try
		{
			using Process? process = Process.Start(new ProcessStartInfo("shutdown", mode + " /t 10")
			{
				CreateNoWindow = true,
				UseShellExecute = false
			});
			return process != null && process.WaitForExit(5000) && process.ExitCode == 0;
		}
		catch (Exception ex)
		{
			_storage.Log("Kapatma baslatilamadi: " + ex.Message);
			return false;
		}
	}

	private void EnsureKeyboardHook()
	{
		int? err = _keys.Install();
		if (err.HasValue && !_hookErrorLogged)
		{
			_storage.Log($"Klavye kancasi kurulamadi (Win32 hata {err}); yeniden denenecek");
			_hookErrorLogged = true;
		}
	}

	private void OnDisplaySettingsChanged(object? sender, EventArgs e)
	{
		_dispatcher.InvokeAsync(() =>
		{
			_lockWindow?.CoverAllScreens();
			_bar?.Reposition();
			_announcement?.CoverPrimaryScreen();
		});
	}

	private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
	{
		_dispatcher.InvokeAsync(() =>
		{
			_storage.Log($"Windows oturum olayi: {e.Reason}");
			Record(_engine.Lock(LockReason.Session, NowLocal, _clock.UtcNow));
			RefreshUi();
		});
	}

	public void Dispose()
	{
		_tick.Stop();
		SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
		SystemEvents.SessionSwitch -= OnSessionSwitch;
		_keys.Dispose();

		if (_lockWindow != null)
		{
			_lockWindow.AllowClose = true;
		}
		if (_announcement != null)
		{
			_announcement.AllowClose = true;
		}
		_connection?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(3L));
	}
}
