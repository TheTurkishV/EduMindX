using System;

namespace EduMindX.SmartBoard;

/// <summary>
/// Merkezi yönetim panelinden SignalR üzerinden tahtaya gönderilen uzaktan komut modeli.
/// </summary>
public sealed class BoardCommand
{
	/// <summary>
	/// Komutun veritabanındaki benzersiz kimliği (Idempotency için kullanılır).
	/// </summary>
	public Guid RowGuid { get; set; }

	/// <summary>
	/// Komut türü: "Lock", "Unlock", "RefreshSchedule", "ShowAnnouncement", "SetServiceState", "Reboot", "Shutdown".
	/// </summary>
	public string CommandType { get; set; } = string.Empty;

	/// <summary>
	/// Komuta ait ek parametrelerin JSON formatındaki veri yükü (varsa).
	/// </summary>
	public string? PayloadJson { get; set; }
}
