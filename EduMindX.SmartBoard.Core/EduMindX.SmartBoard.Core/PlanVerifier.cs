using System;
using System.Text.Json;

namespace EduMindX.SmartBoard.Core;

/// <summary>
/// İmzalı ders ve kilit planını (SignedPlan) doğrulayan ve deserialize eden doğrulayıcı sınıf.
/// </summary>
public static class PlanVerifier
{
	private static readonly JsonSerializerOptions Json = new JsonSerializerOptions(JsonSerializerDefaults.Web);

	/// <summary>
	/// İmzalı planın dijital imzasını ve hedef tahta kimliğini (DeviceGuid) doğrular.
	/// </summary>
	/// <param name="signed">Sunucudan veya diskten gelen imzalı plan nesnesi.</param>
	/// <param name="publicKeyBase64">Sunucunun ECDSA genel anahtarı.</param>
	/// <param name="expectedDeviceGuid">Bu tahtanın beklenen benzersiz kimliği.</param>
	/// <returns>İmza ve kimlik geçerliyse doğrulanmış LockPlan; aksi halde null.</returns>
	public static LockPlan? Verify(SignedPlan? signed, string? publicKeyBase64, Guid expectedDeviceGuid)
	{
		if (signed == null || expectedDeviceGuid == Guid.Empty)
		{
			return null;
		}

		// 1. Kriptografik imza doğrulaması (ECDSA-SHA256)
		if (!Signatures.IsValid(signed.Payload, signed.Signature, publicKeyBase64))
		{
			return null;
		}

		// 2. JSON veri yükünün çözümlenmesi ve cihaz kimlik kontrolü
		try
		{
			LockPlan? lockPlan = JsonSerializer.Deserialize<LockPlan>(signed.Payload, Json);
			
			// Planın bu cihaza ait olduğunu doğrula (başka cihazın planı kullanılamaz)
			return (lockPlan != null && lockPlan.DeviceGuid == expectedDeviceGuid) ? lockPlan : null;
		}
		catch (JsonException)
		{
			return null;
		}
	}
}
