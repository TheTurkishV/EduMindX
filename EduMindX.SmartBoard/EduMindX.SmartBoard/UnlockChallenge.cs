using System;

namespace EduMindX.SmartBoard;

/// <summary>
/// QR ile kilit açma talebinde sunucunun döndürdüğü oturum meydan okuma modeli.
/// </summary>
public sealed class UnlockChallenge
{
	/// <summary>
	/// Öğretmenin telefonundaki kamerayla okutacağı web bağlantı adresi.
	/// </summary>
	public string Url { get; set; } = string.Empty;

	/// <summary>
	/// Bu meydan okumanın geçerliliğini yitireceği UTC zamanı.
	/// </summary>
	public DateTime ExpiresAt { get; set; }
}
