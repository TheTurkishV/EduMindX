using System;

namespace EduMindX.SmartBoard;

/// <summary>
/// Akıllı tahtanın ilk kurulumunda girilen ve yerel diskte (config.dat) şifreli saklanan bağlantı ayarları.
/// </summary>
public sealed class BoardConfig
{
	/// <summary>
	/// Merkezi sunucunun temel API adresi (Örn: "https://api.example.com").
	/// </summary>
	public string ApiBaseUrl { get; set; } = string.Empty;

	/// <summary>
	/// Tahtaya tahsis edilmiş gizli kimlik doğrulama anahtarı (JWT / Device Token).
	/// </summary>
	public string DeviceToken { get; set; } = string.Empty;

	/// <summary>
	/// Bu tahtanın veritabanındaki benzersiz kimliği (Guid).
	/// </summary>
	public Guid DeviceGuid { get; set; }

	/// <summary>
	/// Sunucunun ders planlarını imzaladığı resmi ECDSA genel anahtarı.
	/// </summary>
	public string PlanPublicKey { get; set; } = string.Empty;

	/// <summary>
	/// Güncelleme manifestosunun indirileceği özel URL adresi (boş bırakılırsa varsayılan kullanılır).
	/// </summary>
	public string UpdateUrl { get; set; } = string.Empty;

	/// <summary>
	/// Yapılandırmanın sunucu bağlantısı için eksiksiz olup olmadığını doğrular.
	/// </summary>
	public bool IsComplete
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(ApiBaseUrl) && !string.IsNullOrWhiteSpace(DeviceToken) && DeviceGuid != Guid.Empty)
			{
				return !string.IsNullOrWhiteSpace(PlanPublicKey);
			}
			return false;
		}
	}
}
