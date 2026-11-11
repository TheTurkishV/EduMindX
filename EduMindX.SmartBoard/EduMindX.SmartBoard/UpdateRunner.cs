using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EduMindX.SmartBoard.Core;

namespace EduMindX.SmartBoard;

/// <summary>
/// Otomatik güncelleme çalıştırma motoru.
/// Yeni sürüm manifestosunu indirir, ECDSA imzasını doğrular, paketi SHA-256 ile kontrol eder,
/// izole ortamda kendi kendini test eder (--self-test) ve atomik takas ile güncellemeyi uygular.
/// </summary>
internal static class UpdateRunner
{
	/// <summary>
	/// Varsayılan güncelleme manifestosu adresi.
	/// Not: Canlı ortamda BoardConfig.UpdateUrl üzerinden yapılandırılır.
	/// </summary>
	public const string DefaultManifestUrl = "https://api.example.com/update/version.json";

	private const string ExeName = "EduMindX.SmartBoard.exe";
	private const string BoardTaskName = "EduMindX Akilli Tahta";
	private static readonly JsonSerializerOptions Json = new JsonSerializerOptions(JsonSerializerDefaults.Web);

	/// <summary>
	/// Güncelleme sürecini yürütür.
	/// </summary>
	public static async Task<int> RunAsync()
	{
		// Aynı anda birden fazla güncelleme çalışmasını Global Mutex ile engelle
		using Mutex mutex = new Mutex(initiallyOwned: false, "Global\\EduMindX.SmartBoard.Update");
		if (!mutex.WaitOne(0))
		{
			return 0;
		}

		BoardStorage storage = new BoardStorage();
		string installDir = Path.GetDirectoryName(Environment.ProcessPath)!;
		string workDir = Path.Combine(installDir, ".update");

		try
		{
			DeleteLeftovers(installDir);
			BoardConfig boardConfig = storage.LoadConfig();
			string manifestUrl = string.IsNullOrWhiteSpace(boardConfig.UpdateUrl) ? DefaultManifestUrl : boardConfig.UpdateUrl;

			using HttpClient http = new HttpClient(new WafSafeHeadersHandler(new HttpClientHandler()))
			{
				Timeout = TimeSpan.FromMinutes(15L)
			};
			http.DefaultRequestHeaders.UserAgent.ParseAdd("EduMindX-SmartBoard-Updater/" + AppInfo.Version);

			// 1. Manifestoyu indir ve ECDSA imzasını doğrula
			SignedDocument? signed = JsonSerializer.Deserialize<SignedDocument>(await GetManifestAsync(http, manifestUrl, storage), Json);
			UpdateManifest? manifest = UpdateVerifier.Verify(signed, UpdateVerifier.ReleasePublicKey);
			if (manifest == null)
			{
				storage.Log("Guncelleme: surum dosyasinin imzasi gecersiz; kurulmadi");
				return 1;
			}

			// 2. Sürüm kontrolü yap
			if (!UpdateVerifier.IsNewer(manifest.Version, AppInfo.Version))
			{
				storage.Log($"Guncelleme: guncel (kurulu {AppInfo.Version}, yayinda {manifest.Version})");
				return 0;
			}

			storage.Log($"Guncelleme: {AppInfo.Version} -> {manifest.Version} indiriliyor");

			if (Directory.Exists(workDir))
			{
				Directory.Delete(workDir, recursive: true);
			}
			Directory.CreateDirectory(workDir);

			// 3. Paketi indir ve SHA-256 bütünlüğünü kontrol et
			string zipPath = Path.Combine(workDir, "paket.zip");
			await using (FileStream file = File.Create(zipPath))
			{
				await using Stream body = await http.GetStreamAsync(new Uri(new Uri(manifestUrl), manifest.PackageFile));
				await body.CopyToAsync(file);
			}

			string downloadedHash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(zipPath))).ToLowerInvariant();
			if (downloadedHash != manifest.Sha256)
			{
				storage.Log("Guncelleme: paketin ozeti surum dosyasiyla uyusmuyor; kurulmadi");
				return 1;
			}

			// 4. Paketi aç
			string extractPath = Path.Combine(workDir, "paket");
			ZipFile.ExtractToDirectory(zipPath, extractPath);
			string appPath = Path.Combine(extractPath, "app");
			string newExePath = Path.Combine(appPath, ExeName);

			// 5. Kritik Adım: Yeni sürümü izole ortamda Self-Test ile test et
			if (!File.Exists(newExePath) || !SelfTestPasses(newExePath, workDir, manifest.Version, storage))
			{
				return 1;
			}

			// 6. Atomik Takas (Swap) uygula: Eski dosyaları .old yap, yenileri kopyala
			Swap(appPath, installDir);

			// 7. Eski tahta süreçlerini kapat ve yenisini Görev Zamanlayıcı ile başlat
			RestartBoards();
			storage.Log("Guncelleme: " + manifest.Version + " kuruldu, tahta uygulamasi yeniden baslatildi");
			return 0;
		}
		catch (Exception ex)
		{
			storage.Log("Guncelleme basarisiz (eski surumle devam): " + ex.Message);
			return 1;
		}
		finally
		{
			try
			{
				if (Directory.Exists(workDir))
				{
					Directory.Delete(workDir, recursive: true);
				}
			}
			catch (IOException)
			{
			}
		}
	}

	private static async Task<string> GetManifestAsync(HttpClient http, string manifestUrl, BoardStorage storage)
	{
		int attempt = 1;
		while (true)
		{
			try
			{
				return await http.GetStringAsync(manifestUrl);
			}
			catch (HttpRequestException ex) when (attempt < 5)
			{
				storage.Log("Guncelleme: surum dosyasi alinamadi (" + ex.Message + "); 1 dk sonra yeniden denenecek");
				await Task.Delay(TimeSpan.FromMinutes(1L));
			}
			attempt++;
		}
	}

	/// <summary>
	/// --self-test parametresiyle çağrıldığında çalışır; uygulamanın açılıp yapılandırmayı okuyabildiğini teyit eder.
	/// </summary>
	public static int WriteSelfTest(string resultPath)
	{
		BoardStorage boardStorage = new BoardStorage();
		RegisterInstalledTask(boardStorage);
		bool isComplete = boardStorage.LoadConfig().IsComplete;

		File.WriteAllText(resultPath, JsonSerializer.Serialize(new
		{
			version = AppInfo.Version,
			configComplete = isComplete
		}, Json));

		return (!isComplete) ? 1 : 0;
	}

	private static void RegisterInstalledTask(BoardStorage storage)
	{
		string installedExePath = UpdateTask.InstalledExePath;
		if (!File.Exists(installedExePath))
		{
			return;
		}

		try
		{
			UpdateTask.Register(installedExePath);
		}
		catch (Exception ex)
		{
			storage.Log("Guncelleme gorevi yenilenemedi (eski tanimla devam): " + ex.Message);
		}
	}

	/// <summary>
	/// Staged edilen yeni EXE'yi arka planda --self-test ile çalıştırıp sorunsuz çalıştığını doğrular.
	/// </summary>
	private static bool SelfTestPasses(string stagedExe, string workDir, string expectedVersion, BoardStorage storage)
	{
		string resultJsonPath = Path.Combine(workDir, "self-test.json");
		using Process? process = Process.Start(new ProcessStartInfo(stagedExe, $"--self-test \"{resultJsonPath}\"")
		{
			UseShellExecute = false
		});

		if (process == null || !WaitOrKill(process, 60000) || process.ExitCode != 0 || !File.Exists(resultJsonPath))
		{
			storage.Log("Guncelleme: yeni surum denemede calismadi; kurulmadi");
			return false;
		}

		using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(resultJsonPath));
		string? version = jsonDocument.RootElement.GetProperty("version").GetString();
		if (version == expectedVersion)
		{
			return true;
		}

		storage.Log($"Guncelleme: paketteki surum ({version}) bildirimle ({expectedVersion}) uyusmuyor; kurulmadi");
		return false;
	}

	/// <summary>
	/// Eski dosyaları .old uzantısına taşır, yenileri yerleştirir. Hata olursa rollback yapar.
	/// </summary>
	private static void Swap(string stagedApp, string installDir)
	{
		List<string> movedFiles = new List<string>();
		try
		{
			string[] files = Directory.GetFiles(stagedApp);
			foreach (string filePath in files)
			{
				string targetFile = Path.Combine(installDir, Path.GetFileName(filePath));
				if (File.Exists(targetFile))
				{
					File.Move(targetFile, targetFile + ".old", overwrite: true);
					movedFiles.Add(targetFile);
				}
				File.Copy(filePath, targetFile);
			}
		}
		catch
		{
			// Hata durumunda rollback (eski dosyaları geri al)
			foreach (string oldTarget in movedFiles)
			{
				if (File.Exists(oldTarget))
				{
					File.Delete(oldTarget);
				}
				File.Move(oldTarget + ".old", oldTarget);
			}
			throw;
		}
	}

	private static void RestartBoards()
	{
		Process[] processesByName = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ExeName));
		foreach (Process proc in processesByName)
		{
			if (proc.Id != Environment.ProcessId)
			{
				try
				{
					proc.Kill();
				}
				catch (InvalidOperationException)
				{
				}
				catch (Win32Exception)
				{
				}
			}
		}

		using Process? process = Process.Start(new ProcessStartInfo("schtasks.exe", $"/Run /TN \"{BoardTaskName}\"")
		{
			UseShellExecute = false,
			CreateNoWindow = true
		});

		if (process != null)
		{
			WaitOrKill(process, 15000);
		}
	}

	private static bool WaitOrKill(Process process, int milliseconds)
	{
		if (process.WaitForExit(milliseconds))
		{
			return true;
		}

		try
		{
			process.Kill(entireProcessTree: true);
			process.WaitForExit(10000);
		}
		catch (InvalidOperationException)
		{
		}
		catch (Win32Exception)
		{
		}
		return false;
	}

	private static void DeleteLeftovers(string installDir)
	{
		string[] files = Directory.GetFiles(installDir, "*.old");
		foreach (string path in files)
		{
			try
			{
				File.Delete(path);
			}
			catch (IOException)
			{
			}
			catch (UnauthorizedAccessException)
			{
			}
		}
	}
}
