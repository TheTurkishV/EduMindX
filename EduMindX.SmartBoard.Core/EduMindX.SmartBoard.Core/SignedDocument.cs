namespace EduMindX.SmartBoard.Core;

/// <summary>
/// Sunucu tarafından dijital olarak imzalanmış genel bir belgeyi temsil eder.
/// </summary>
public sealed class SignedDocument
{
	/// <summary>
	/// Belgenin ham JSON veri yükü (Payload).
	/// </summary>
	public string Payload { get; set; } = string.Empty;

	/// <summary>
	/// Veri yükünün ECDSA dijital imzası (Base64).
	/// </summary>
	public string Signature { get; set; } = string.Empty;
}
