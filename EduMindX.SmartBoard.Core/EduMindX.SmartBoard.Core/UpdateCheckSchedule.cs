using System;

namespace EduMindX.SmartBoard.Core;

/// <summary>
/// Otomatik güncelleme denetiminin ne zaman çalışacağını belirleyen zamanlama mantığı.
/// Güncelleme kontrolünün dersi bölmemesi için tahta yalnızca kilitliyken ve ders dışındayken çalışır.
/// </summary>
public sealed class UpdateCheckSchedule
{
	/// <summary>
	/// Bilgisayar açıldıktan sonra ilk denetimin yapılacağı gecikme süresi (2 Dakika).
	/// </summary>
	public static readonly TimeSpan FirstDelay = TimeSpan.FromMinutes(2L);

	/// <summary>
	/// Güncelleme denetimleri arasındaki periyodik aralık (6 Saat).
	/// </summary>
	public static readonly TimeSpan Interval = TimeSpan.FromHours(6);

	// Bir sonraki denetim zamanı
	private TimeSpan _next = FirstDelay;

	/// <summary>
	/// Güncelleme denetimi yapılmasının uygun olup olmadığını kontrol eder.
	/// </summary>
	/// <param name="uptime">Uygulamanın çalıştığı toplam süre.</param>
	/// <param name="locked">Tahtanın o an kilitli olup olmadığı.</param>
	/// <param name="inLesson">Şu an resmi bir ders saati içinde olunup olunmadığı.</param>
	/// <param name="online">Sunucuya internet/ağ bağlantısının aktif olup olmadığı.</param>
	/// <returns>Tüm şartlar uygunsa (uptime doldu, kilitli, ders dışı ve online) true döner.</returns>
	public bool ShouldCheck(TimeSpan uptime, bool locked, bool inLesson, bool online)
	{
		// Zamanı gelmemişse, kilitli değilse, dersteyse veya internet yoksa güncelleme yapma
		if (((uptime < _next || !locked) | inLesson) || !online)
		{
			return false;
		}

		// Bir sonraki denetim zamanını 6 saat sonraya ötele
		_next = uptime + Interval;
		return true;
	}
}
