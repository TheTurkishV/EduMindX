namespace EduMindX.SmartBoard.Core;

/// <summary>
/// Parola veya PIN doğrulama denemesinin sonucu.
/// </summary>
public enum PasswordResult
{
	/// <summary>
	/// Parola veya PIN doğru, kilit açılabilir.
	/// </summary>
	Success,

	/// <summary>
	/// Parola veya PIN yanlış.
	/// </summary>
	Wrong,

	/// <summary>
	/// Çok fazla hatalı deneme yapıldığı için sistem geçici olarak kilitlendi (bekleme süresi aktif).
	/// </summary>
	LockedOut,

	/// <summary>
	/// Tahtada tanımlı bir parola veya geçerli günlük kod bulunamadı.
	/// </summary>
	NoPassword
}
