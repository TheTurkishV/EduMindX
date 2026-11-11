using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using EduMindX.SmartBoard.Core;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;

namespace EduMindX.SmartBoard;

/// <summary>
/// Merkezi sunucuyla ASP.NET Core SignalR WebSocket protokolü üzerinden
/// çift yönlü, gerçek zamanlı iletişimi yöneten istemci sınıfı.
/// </summary>
public sealed class DeviceConnection : IAsyncDisposable
{
	/// <summary>
	/// Bağlantı koptuğunda sonsuza kadar yeniden bağlanmayı deneyen yeniden deneme politikası.
	/// İlk 5 deneme 5 saniye arayla, sonrakiler 30 saniye arayla denenir.
	/// </summary>
	private sealed class ForeverRetryPolicy : IRetryPolicy
	{
		public TimeSpan? NextRetryDelay(RetryContext retryContext)
		{
			long previousRetryCount = retryContext.PreviousRetryCount;
			if (previousRetryCount >= 5)
			{
				return TimeSpan.FromSeconds(30L);
			}
			return (previousRetryCount != 0L) ? TimeSpan.FromSeconds(5L) : TimeSpan.Zero;
		}
	}

	private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(15L);
	private readonly HubConnection _hub;
	private readonly BoardStorage _storage;
	private readonly CancellationTokenSource _stop = new CancellationTokenSource();

	/// <summary>
	/// Sunucuya SignalR bağlantısının o an aktif olup olmadığını belirtir.
	/// </summary>
	public bool IsConnected => _hub.State == HubConnectionState.Connected;

	/// <summary>
	/// Sunucudan uzaktan bir komut (Lock, Unlock, Duyuru vb.) geldiğinde tetiklenen olay.
	/// </summary>
	public event Action<BoardCommand>? CommandReceived;

	/// <summary>
	/// Sunucuda ders programı değiştiğinde tetiklenen olay.
	/// </summary>
	public event Action? PlanChanged;

	/// <summary>
	/// Sunucuyla bağlantı başarılı şekilde kurulduğunda tetiklenen olay.
	/// </summary>
	public event Action? Connected;

	public DeviceConnection(BoardConfig config, BoardStorage storage)
	{
		_storage = storage;
		Uri hubUrl = new Uri(new Uri(config.ApiBaseUrl.TrimEnd('/') + "/"), "hubs/device");

		// SignalR Hub bağlantısını yapılandır
		_hub = new HubConnectionBuilder()
			.WithUrl(hubUrl, (HttpConnectionOptions options) =>
			{
				// Tahtanın kimlik doğrulama token'ını taşıyıcı başlık olarak ekle
				options.AccessTokenProvider = () => Task.FromResult<string?>(config.DeviceToken);
				options.HttpMessageHandlerFactory = (HttpMessageHandler inner) => new WafSafeHeadersHandler(inner);
				options.WebSocketConfiguration = (ClientWebSocketOptions socket) =>
				{
					socket.SetRequestHeader("Accept", "*/*");
				};
			})
			.WithAutomaticReconnect(new ForeverRetryPolicy())
			.Build();

		_hub.ServerTimeout = TimeSpan.FromSeconds(60L);

		// Sunucudan gelen RPC çağrılarını dinle
		_hub.On("Command", (BoardCommand command) =>
		{
			CommandReceived?.Invoke(command);
		});

		_hub.On("PlanChanged", () =>
		{
			PlanChanged?.Invoke();
		});

		_hub.Reconnecting += (Exception? error) =>
		{
			_storage.Log("Baglanti koptu, yeniden deneniyor: " + error?.Message);
			return Task.CompletedTask;
		};

		_hub.Reconnected += (string? _) =>
		{
			_storage.Log("Sunucuya yeniden baglandi");
			Connected?.Invoke();
			return Task.CompletedTask;
		};

		_hub.Closed += async (Exception? error) =>
		{
			if (!_stop.IsCancellationRequested)
			{
				_storage.Log("Baglanti kapandi: " + error?.Message);
				await StartAsync();
			}
		};
	}

	/// <summary>
	/// Arka planda sunucuya bağlanmayı dener ve bağlantı koparsa yeniden deneme döngüsünü sürdürür.
	/// </summary>
	public async Task StartAsync()
	{
		while (!_stop.IsCancellationRequested)
		{
			try
			{
				await _hub.StartAsync(_stop.Token);
				_storage.Log("Sunucuya baglandi");
				Connected?.Invoke();
				break;
			}
			catch (OperationCanceledException) when (_stop.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex2)
			{
				_storage.Log("Baglanti kurulamadi: " + ex2.Message);
				try
				{
					await Task.Delay(RetryDelay, _stop.Token);
				}
				catch (OperationCanceledException)
				{
					break;
				}
			}
		}
	}

	public Task ConnectOnceAsync(CancellationToken cancellationToken)
	{
		return _hub.StartAsync(cancellationToken);
	}

	/// <summary>
	/// Sunucudan bu tahtaya tahsis edilmiş en son imzalı ders ve kilit planını çeker.
	/// </summary>
	public Task<SignedPlan?> GetLockPlanAsync()
	{
		return _hub.InvokeAsync<SignedPlan?>("GetLockPlan");
	}

	/// <summary>
	/// Sunucudan plan imzasını doğrulamak için kullanılan ECDSA genel anahtarını çeker.
	/// </summary>
	public Task<string> GetPlanSigningKeyAsync()
	{
		return _hub.InvokeAsync<string>("GetPlanSigningKey");
	}

	/// <summary>
	/// QR kod ile kilit açmak için sunucuda bir oturum meydan okuması (challenge) başlatır.
	/// </summary>
	public Task<UnlockChallenge?> CreateUnlockChallengeAsync()
	{
		return _hub.InvokeAsync<UnlockChallenge?>("CreateUnlockChallenge");
	}

	/// <summary>
	/// Sunucuya tahtanın durumunu bildirir (Heartbeat) ve sunucu UTC saatini alır.
	/// </summary>
	public Task<DateTime> HeartbeatAsync(bool locked, string agentVersion)
	{
		return _hub.InvokeAsync<DateTime>("Heartbeat", new
		{
			AgentVersion = agentVersion,
			Locked = locked
		});
	}

	/// <summary>
	/// Tahta çevrimdışıyken sunucuda biriken bekleyen komut kuyruğunu çeker.
	/// </summary>
	public Task<List<BoardCommand>> FetchPendingAsync()
	{
		return _hub.InvokeAsync<List<BoardCommand>>("FetchPending");
	}

	/// <summary>
	/// Uzaktan komutun işlenme sonucunu sunucuya teyit (Ack) eder.
	/// </summary>
	public Task AckAsync(Guid commandGuid, bool success, string? message)
	{
		return _hub.InvokeAsync("Ack", new
		{
			CommandGuid = commandGuid,
			Status = (success ? "DONE" : "FAILED"),
			Result = ((message == null) ? null : new { message })
		});
	}

	/// <summary>
	/// Tahtada gerçekleşen kilit ve güvenlik olaylarını sunucuya toplu raporlar.
	/// </summary>
	public Task<int> ReportEventsAsync(IReadOnlyList<BoardEvent> events)
	{
		return _hub.InvokeAsync<int>("ReportEvents", events.Select((BoardEvent e) => new
		{
			e.EventType,
			e.Reason,
			e.InLesson,
			e.OccurredAtUtc,
			e.CommandGuid
		}).ToList());
	}

	public async ValueTask DisposeAsync()
	{
		_stop.Cancel();
		await _hub.DisposeAsync();
		_stop.Dispose();
	}
}
