using System.Collections.Generic;

namespace EduMindX.SmartBoard.Core;

/// <summary>
/// Belirli bir takvim gününe ait tek kullanımlık çevrimdışı kilit açma kod hash'lerini temsil eder.
/// </summary>
public sealed class UnlockCodeDay
{
	/// <summary>
	/// Kodların geçerli olduğu tarih dize formatında (Örn: "2026-11-11").
	/// </summary>
	public string Date { get; set; } = string.Empty;

	/// <summary>
	/// O gün için üretilmiş 8 haneli tek kullanımlık kodların BCrypt hash listesi.
	/// </summary>
	public List<string> Hashes { get; set; } = new List<string>();
}
