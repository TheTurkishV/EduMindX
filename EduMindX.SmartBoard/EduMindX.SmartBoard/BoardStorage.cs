using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EduMindX.SmartBoard.Core;

namespace EduMindX.SmartBoard;

/// <summary>
/// Akıllı tahtanın yerel veri depolama ve günlükleme (logging) yöneticisi.
/// Hassas dosyaları (config.dat, plan.dat, used-codes.json) Windows DPAPI (Data Protection API) ile şifreler.
/// Dosya bozulmalarını önlemek için geçici dosya (.tmp) üzerinden atomik yazma uygular.
/// </summary>
public sealed class BoardStorage
{
	// Maksimum log dosyası boyutu: 1 MB
	private const long MaxLogBytes = 1000000L;

	/// <summary>
	/// DPAPI şifreleme ve çözme işleminde kullanılan ek entropi bayt dizisi.
	/// </summary>
	public static readonly byte[] ProtectionEntropy = Encoding.UTF8.GetBytes("EduMindX.SmartBoard.v1");

	private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	private readonly object _logLock = new object();

	/// <summary>
	/// Uygulama verilerinin saklandığı ana dizin (Varsayılan: %ProgramData%\EduMindX\SmartBoard).
	/// </summary>
	public string DataDirectory { get; }

	private string ConfigPath => Path.Combine(DataDirectory, "config.dat");
	private string PlanPath => Path.Combine(DataDirectory, "plan.dat");
	private string EventsPath => Path.Combine(DataDirectory, "events.json");
	private string CommandsPath => Path.Combine(DataDirectory, "commands.json");
	private string UsedCodesPath => Path.Combine(DataDirectory, "used-codes.json");
	private string LogPath => Path.Combine(DataDirectory, "board.log");
	private string BarPositionPath => Path.Combine(DataDirectory, "bar-position.json");

	/// <summary>
	/// Varsayılan sistem diziniyle depolama yöneticisini başlatır.
	/// </summary>
	public BoardStorage(string? dataDirectory = null)
	{
		DataDirectory = dataDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EduMindX", "SmartBoard");
	}

	/// <summary>
	/// DPAPI ile şifrelenmiş sunucu bağlantı ve kimlik yapılandırmasını yükler.
	/// </summary>
	public BoardConfig LoadConfig()
	{
		return ReadProtected<BoardConfig>(ConfigPath) ?? new BoardConfig();
	}

	/// <summary>
	/// DPAPI ile şifrelenmiş son geçerli imzalı ders planını yükler.
	/// </summary>
	public SignedPlan? LoadSignedPlan()
	{
		return ReadProtected<SignedPlan>(PlanPath);
	}

	/// <summary>
	/// İmzalı ders planını DPAPI ile şifreleyerek diske kaydeder.
	/// </summary>
	public void SaveSignedPlan(SignedPlan plan)
	{
		WriteProtected(PlanPath, plan);
	}

	/// <summary>
	/// Sunucuya henüz iletilmemiş bekleyen olayları (Events) yükler.
	/// </summary>
	public List<BoardEvent> LoadEvents()
	{
		return Read<List<BoardEvent>>(EventsPath) ?? new List<BoardEvent>();
	}

	/// <summary>
	/// Bekleyen olaylar kuyruğunu diske kaydeder.
	/// </summary>
	public void SaveEvents(List<BoardEvent> events)
	{
		Write(EventsPath, events);
	}

	/// <summary>
	/// İşlenmiş komutların kimlik geçmişini yükler.
	/// </summary>
	public HandledCommands LoadHandledCommands()
	{
		return new HandledCommands(Read<Dictionary<Guid, DateTime>>(CommandsPath));
	}

	/// <summary>
	/// İşlenmiş komut geçmişini diske kaydeder.
	/// </summary>
	public void SaveHandledCommands(HandledCommands commands)
	{
		Write(CommandsPath, commands.Entries.ToDictionary((KeyValuePair<Guid, DateTime> e) => e.Key, (KeyValuePair<Guid, DateTime> e) => e.Value));
	}

	/// <summary>
	/// Kullanılmış tek kullanımlık kodların kaydını yükler.
	/// </summary>
	public UsedUnlockCodes LoadUsedCodes()
	{
		return new UsedUnlockCodes(Read<Dictionary<string, DateTime>>(UsedCodesPath));
	}

	/// <summary>
	/// Kullanılmış tek kullanımlık kodların kaydını diske kaydeder.
	/// </summary>
	public void SaveUsedCodes(UsedUnlockCodes codes)
	{
		Write(UsedCodesPath, codes.Entries.ToDictionary((KeyValuePair<string, DateTime> e) => e.Key, (KeyValuePair<string, DateTime> e) => e.Value));
	}

	/// <summary>
	/// Yüzen çubuğun ekrandaki son konumunu yükler.
	/// </summary>
	public FloatingPosition LoadBarPosition()
	{
		return Read<FloatingPosition>(BarPositionPath)?.Normalized() ?? FloatingPosition.Default;
	}

	/// <summary>
	/// Yüzen çubuğun yeni konumunu diske kaydeder.
	/// </summary>
	public void SaveBarPosition(FloatingPosition position)
	{
		Write(BarPositionPath, position);
	}

	/// <summary>
	/// Tahta çalışma günlüğüne (board.log) zaman damgalı mesaj ekler.
	/// Dosya boyutu 1 MB'ı aşarsa otomatik olarak board.log.1 olarak arşivlenir (Rolling Log).
	/// </summary>
	public void Log(string message)
	{
		try
		{
			lock (_logLock)
			{
				Directory.CreateDirectory(DataDirectory);
				if (File.Exists(LogPath) && new FileInfo(LogPath).Length > MaxLogBytes)
				{
					File.Move(LogPath, LogPath + ".1", overwrite: true);
				}
				File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
			}
		}
		catch (Exception)
		{
			// Günlükleme hatası durumunda uygulamanın çökmesi engellenir
		}
	}

	/// <summary>
	/// Windows DPAPI (LocalMachine) ile şifrelenmiş dosyayı çözer ve JSON olarak deserialize eder.
	/// </summary>
	private T? ReadProtected<T>(string path) where T : class
	{
		try
		{
			if (!File.Exists(path))
			{
				return null;
			}
			byte[] encrypted = File.ReadAllBytes(path);
			byte[] decrypted = ProtectedData.Unprotect(encrypted, ProtectionEntropy, DataProtectionScope.LocalMachine);
			return JsonSerializer.Deserialize<T>(decrypted, Json);
		}
		catch (Exception ex)
		{
			Log(Path.GetFileName(path) + " okunamadi: " + ex.Message);
			return null;
		}
	}

	/// <summary>
	/// Nesneyi JSON'a çevirip Windows DPAPI (LocalMachine) ile şifreler ve atomik olarak diske yazar.
	/// </summary>
	private void WriteProtected<T>(string path, T value)
	{
		try
		{
			Directory.CreateDirectory(DataDirectory);
			byte[] plain = JsonSerializer.SerializeToUtf8Bytes(value, Json);
			byte[] encrypted = ProtectedData.Protect(plain, ProtectionEntropy, DataProtectionScope.LocalMachine);
			
			// Önce geçici dosyaya yaz, sonra atomik taşıma ile üzerine yaz (elektrik kesintisi koruması)
			string tempPath = path + ".tmp";
			File.WriteAllBytes(tempPath, encrypted);
			File.Move(tempPath, path, overwrite: true);
		}
		catch (Exception ex)
		{
			Log(Path.GetFileName(path) + " yazilamadi: " + ex.Message);
		}
	}

	private T? Read<T>(string path) where T : class
	{
		try
		{
			return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json) : null;
		}
		catch (Exception ex)
		{
			Log(Path.GetFileName(path) + " okunamadi: " + ex.Message);
			return null;
		}
	}

	private void Write<T>(string path, T value)
	{
		try
		{
			Directory.CreateDirectory(DataDirectory);
			string tempPath = path + ".tmp";
			File.WriteAllText(tempPath, JsonSerializer.Serialize(value, Json));
			File.Move(tempPath, path, overwrite: true);
		}
		catch (Exception ex)
		{
			Log(Path.GetFileName(path) + " yazilamadi: " + ex.Message);
		}
	}
}
