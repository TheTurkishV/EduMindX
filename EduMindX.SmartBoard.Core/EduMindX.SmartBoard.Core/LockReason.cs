namespace EduMindX.SmartBoard.Core;

/// <summary>
/// Akıllı tahtanın kilitlenme gerekçeleri.
/// </summary>
public enum LockReason
{
	/// <summary>
	/// Öğretmen ekrandaki çubuktan "Kilitle" butonuna basarak kilitledi.
	/// </summary>
	Teacher,

	/// <summary>
	/// Merkezi yönetim panelinden uzaktan kilitleme emri gönderildi.
	/// </summary>
	Remote,

	/// <summary>
	/// Ders saati sona erdi ve plan gereği otomatik kilitlendi.
	/// </summary>
	LessonEnd,

	/// <summary>
	/// Ders dışı serbest kullanım süresi (örn. 10 dk) doldu.
	/// </summary>
	Timeout,

	/// <summary>
	/// Windows kullanıcı oturumu değişti veya kilitlendi.
	/// </summary>
	Session,

	/// <summary>
	/// Tahta okul idaresi tarafından hizmet dışına alındı.
	/// </summary>
	ServiceDisabled
}
