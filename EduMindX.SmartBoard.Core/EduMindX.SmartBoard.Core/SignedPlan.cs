namespace EduMindX.SmartBoard.Core;

/// <summary>
/// Sunucu tarafından dijital olarak imzalanmış ders ve kilit planını temsil eden model.
/// </summary>
public sealed class SignedPlan
{
	/// <summary>
	/// LockPlan modelinin JSON formatındaki ham veri yükü (Payload).
	/// </summary>
	public string Payload { get; set; } = string.Empty;

	/// <summary>
	/// Veri yükünün ECDSA algoritması ile üretilmiş Base64 formatındaki dijital imzası.
	/// </summary>
	public string Signature { get; set; } = string.Empty;
}
