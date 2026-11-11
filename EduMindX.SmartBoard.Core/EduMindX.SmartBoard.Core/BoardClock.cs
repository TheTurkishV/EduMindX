using System;
using System.Diagnostics;

namespace EduMindX.SmartBoard.Core;

/// <summary>
/// Akıllı tahta için zaman manipülasyonlarına (hilelerine) karşı korumalı saat mekanizması.
/// Kullanıcıların veya öğrencilerin Windows sistem saatini geri/ileri alarak kilit sistemini
/// atlatmasını engellemek amacıyla donanımsal yüksek çözünürlüklü sayaç (Stopwatch) kullanır.
/// </summary>
public sealed class BoardClock
{
	// İşlemcinin donanımsal tick sayacını döndüren fonksiyon (Varsayılan: Stopwatch.GetTimestamp)
	private readonly Func<long> _monotonicTicks;

	// İşletim sisteminin UTC saatini döndüren yedek fonksiyon (Varsayılan: DateTime.UtcNow)
	private readonly Func<DateTime> _systemUtc;

	// Sunucu ile eşitlenmiş son UTC zamanı
	private DateTime? _syncedServerUtc;

	// Eşitlemenin yapıldığı andaki donanımsal sayaç değeri
	private long _syncedAtTicks;

	/// <summary>
	/// Saatin merkezi sunucu ile en az bir kez eşitlenip eşitlenmediğini belirtir.
	/// </summary>
	public bool IsSynced => _syncedServerUtc.HasValue;

	/// <summary>
	/// Hile korumalı güncel UTC zamanını döndürür.
	/// Eğer sunucu saati eşitlenmişse, işletim sistemi saatine güvenmek yerine
	/// donanımsal sayaç üzerinden geçen süreyi hesaplar.
	/// </summary>
	public DateTime UtcNow
	{
		get
		{
			DateTime? syncedServerUtc = _syncedServerUtc;
			if (syncedServerUtc.HasValue)
			{
				DateTime valueOrDefault = syncedServerUtc.GetValueOrDefault();
				// Sunucu saatine, o andan itibaren donanım sayacından geçen süreyi ekle
				TimeSpan elapsedTime = Stopwatch.GetElapsedTime(_syncedAtTicks, _monotonicTicks());
				return valueOrDefault + elapsedTime;
			}

			// Henüz sunucu ile eşitlenmemişse mecburen sistem saatini kullan
			return _systemUtc();
		}
	}

	/// <summary>
	/// Varsayılan sistem fonksiyonları ile yeni bir saat örneği başlatır.
	/// </summary>
	public BoardClock()
		: this(Stopwatch.GetTimestamp, () => DateTime.UtcNow)
	{
	}

	/// <summary>
	/// Test edilebilirliği sağlamak için özel zaman fonksiyonları ile başlatıcı.
	/// </summary>
	public BoardClock(Func<long> monotonicTicks, Func<DateTime> systemUtc)
	{
		_monotonicTicks = monotonicTicks;
		_systemUtc = systemUtc;
	}

	/// <summary>
	/// Merkezi sunucudan gelen güvenilir UTC saatini referans alarak saati eşitler.
	/// </summary>
	/// <param name="serverUtc">Sunucunun UTC zamanı.</param>
	public void Sync(DateTime serverUtc)
	{
		_syncedServerUtc = DateTime.SpecifyKind(serverUtc, DateTimeKind.Utc);
		_syncedAtTicks = _monotonicTicks();
	}

	/// <summary>
	/// Belirtilen saat dilimi ofsetine (dakika cinsinden) göre yerel saati hesaplar.
	/// Örneğin Türkiye için +180 dakika (UTC+3).
	/// </summary>
	/// <param name="utcOffsetMinutes">UTC'den dakika cinsinden sapma değeri.</param>
	/// <returns>Hesaplanan yerel saat.</returns>
	public DateTime LocalNow(int utcOffsetMinutes)
	{
		return DateTime.SpecifyKind(UtcNow.AddMinutes(utcOffsetMinutes), DateTimeKind.Unspecified);
	}
}
