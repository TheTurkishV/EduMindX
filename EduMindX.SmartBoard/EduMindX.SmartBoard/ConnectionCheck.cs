using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EduMindX.SmartBoard.Core;

namespace EduMindX.SmartBoard;

/// <summary>
/// CLI üzerinden tahtanın sunucu bağlantısını, token geçerliliğini ve plan imzasını
/// test etmek için kullanılan teşhis (diagnostic) aracı.
/// Örn: EduMindX.SmartBoard.exe --check "https://api.example.com" "result.json"
/// </summary>
internal static class ConnectionCheck
{
	public const string TokenVariable = "EDUMINDX_CHECK_TOKEN";
	private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20L);

	private static Guid ReadDeviceGuid(SignedPlan signed)
	{
		using JsonDocument jsonDocument = JsonDocument.Parse(signed.Payload);
		return (jsonDocument.RootElement.TryGetProperty("deviceGuid", out JsonElement val) && val.TryGetGuid(out Guid guid)) ? guid : Guid.Empty;
	}

	public static async Task<int> RunAsync(string apiBaseUrl, string resultPath)
	{
		bool ok = false;
		object result;

		try
		{
			// Ortam değişkeninden test token'ını oku
			string? token = Environment.GetEnvironmentVariable(TokenVariable);
			if (string.IsNullOrWhiteSpace(token))
			{
				throw new InvalidOperationException("Token ortam degiskeninde verilmedi (EDUMINDX_CHECK_TOKEN)");
			}

			BoardStorage storage = new BoardStorage(Path.Combine(Path.GetTempPath(), "EduMindX-SmartBoard-check"));
			await using DeviceConnection connection = new DeviceConnection(new BoardConfig
			{
				ApiBaseUrl = apiBaseUrl,
				DeviceToken = token
			}, storage);

			using CancellationTokenSource timeout = new CancellationTokenSource(Timeout);
			await connection.ConnectOnceAsync(timeout.Token);

			// Sunucudan planı ve genel imza anahtarını çekip doğrula
			SignedPlan signed = (await connection.GetLockPlanAsync()) ?? throw new InvalidOperationException("Tahta kaydi bulunamadi ya da iptal edilmis");
			string signingKey = await connection.GetPlanSigningKeyAsync();
			LockPlan lockPlan = PlanVerifier.Verify(signed, signingKey, ReadDeviceGuid(signed)) ?? throw new InvalidOperationException("Sunucunun plan imzasi dogrulanamadi");

			ok = true;
			result = new
			{
				ok = ok,
				deviceGuid = lockPlan.DeviceGuid,
				planPublicKey = signingKey,
				deviceName = lockPlan.DeviceName,
				branchName = lockPlan.BranchName,
				educationLevelName = lockPlan.EducationLevelName,
				hasPassword = !string.IsNullOrEmpty(lockPlan.UnlockPasswordHash),
				serviceEnabled = lockPlan.ServiceEnabled,
				lessonCount = lockPlan.Lessons.Count
			};
		}
		catch (Exception ex)
		{
			result = new
			{
				ok = ok,
				error = ex.Message
			};
		}

		await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(result));
		return (!ok) ? 1 : 0;
	}
}
