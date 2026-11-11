namespace EduMindX.SmartBoard.Core;

/// <summary>
/// Akıllı tahtanın kilit açılma yöntemleri.
/// </summary>
public enum UnlockReason
{
	/// <summary>
	/// Kurumun ana idare parolası girilerek açıldı.
	/// </summary>
	Password,

	/// <summary>
	/// Öğretmen mobil uygulamadan tahtadaki QR kodu okutarak açtı.
	/// </summary>
	Qr,

	/// <summary>
	/// Günün 8 haneli tek kullanımlık çevrimdışı kodu girilerek açıldı.
	/// </summary>
	Code,

	/// <summary>
	/// Merkezi yönetim panelinden uzaktan açma emri ile açıldı.
	/// </summary>
	Remote
}
