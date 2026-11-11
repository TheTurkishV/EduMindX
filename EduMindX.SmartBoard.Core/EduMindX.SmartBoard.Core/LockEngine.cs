using System;
using System.Collections.Generic;
using System.Linq;

namespace EduMindX.SmartBoard.Core;

/// <summary>
/// Akıllı tahtanın kilit durum makinesini (State Machine) yöneten çekirdek motor.
/// Açılma süresi hesaplamaları, zaman aşımı kontrolleri ve kilit geçişleri burada gerçekleşir.
/// </summary>
public sealed class LockEngine
{
	// Açılmanın bir ders bitimine kadar mı yoksa serbest zaman aşımına kadar mı olduğunu tutar
	private bool _untilIsLessonEnd;

	/// <summary>
	/// Tek seferde talep edilebilecek maksimum açık kalma süresi (14 Saat).
	/// </summary>
	public static readonly TimeSpan MaxRequestedUnlock = TimeSpan.FromHours(14);

	/// <summary>
	/// Yürürlükteki ders ve kilit planı.
	/// </summary>
	public LockPlan Plan { get; private set; }

	/// <summary>
	/// Tahtanın şu anda kilitli olup olmadığını belirtir. Başlangıçta daima kilitlidir.
	/// </summary>
	public bool IsLocked { get; private set; } = true;

	/// <summary>
	/// Tahtanın ne zamana kadar (yerel saat cinsinden) açık kalacağını gösterir.
	/// Kilitliyken null değerindedir.
	/// </summary>
	public DateTime? UnlockedUntil { get; private set; }

	/// <summary>
	/// Tahtanın hizmete açık olup olmadığı.
	/// </summary>
	public bool ServiceEnabled => Plan.ServiceEnabled;

	/// <summary>
	/// Belirtilen plan ile yeni bir kilit motoru başlatır.
	/// </summary>
	/// <param name="plan">Ders ve kilit planı modeli.</param>
	public LockEngine(LockPlan plan)
	{
		Plan = plan;
	}

	/// <summary>
	/// Sunucudan yeni bir ders planı geldiğinde planı günceller.
	/// Eğer yeni planda tahta hizmet dışı bırakılmışsa otomatik kilitleme olayı üretir.
	/// </summary>
	public BoardEvent? UpdatePlan(LockPlan plan, DateTime nowLocal, DateTime nowUtc)
	{
		bool serviceEnabled = Plan.ServiceEnabled;
		Plan = plan;
		if (!plan.ServiceEnabled && serviceEnabled)
		{
			return Lock(LockReason.ServiceDisabled, nowLocal, nowUtc);
		}
		return null;
	}

	/// <summary>
	/// Tahtanın hizmet durumunu (aktif/pasif) değiştirir.
	/// </summary>
	public BoardEvent? SetServiceEnabled(bool enabled, DateTime nowLocal, DateTime nowUtc)
	{
		bool serviceEnabled = Plan.ServiceEnabled;
		Plan.ServiceEnabled = enabled;
		if (!enabled && serviceEnabled)
		{
			return Lock(LockReason.ServiceDisabled, nowLocal, nowUtc);
		}
		return null;
	}

	/// <summary>
	/// Tahtanın kilidini açar ve geçerlilik süresini hesaplar.
	/// </summary>
	/// <param name="reason">Kilit açılma yöntemi (QR, Kod, Şifre, Uzaktan).</param>
	/// <param name="nowLocal">Yerel saat.</param>
	/// <param name="nowUtc">UTC saat.</param>
	/// <param name="requestedUntilLocal">İdarenin özel talep ettiği bitiş saati (varsa).</param>
	/// <returns>Oluşan kilit açılma olayı (BoardEvent).</returns>
	public BoardEvent? Unlock(UnlockReason reason, DateTime nowLocal, DateTime nowUtc, DateTime? requestedUntilLocal = null)
	{
		// Tahta hizmet dışıysa yalnızca doğrudan şifre ile açılabilir
		if (!ServiceEnabled && reason != UnlockReason.Password)
		{
			return null;
		}

		bool untilIsLessonEnd;
		DateTime value;

		// Eğer uzaktan özel bir bitiş saati talep edilmişse ve 14 saati aşmıyorsa onu kullan
		if (requestedUntilLocal.HasValue)
		{
			DateTime valueOrDefault = requestedUntilLocal.GetValueOrDefault();
			if (valueOrDefault > nowLocal && valueOrDefault - nowLocal <= MaxRequestedUnlock)
			{
				untilIsLessonEnd = true;
				value = valueOrDefault;
				goto ApplyUnlock;
			}
		}

		// Ders planına göre otomatik açılma süresini hesapla
		(value, untilIsLessonEnd) = ComputeUnlockUntil(nowLocal);

	ApplyUnlock:
		IsLocked = false;
		UnlockedUntil = value;
		_untilIsLessonEnd = untilIsLessonEnd;

		return new BoardEvent("UNLOCK", ToCode(reason), CurrentLesson(nowLocal) != null, nowUtc);
	}

	/// <summary>
	/// Tahtayı kilitler.
	/// </summary>
	/// <param name="reason">Kilitlenme gerekçesi.</param>
	/// <param name="nowLocal">Yerel saat.</param>
	/// <param name="nowUtc">UTC saat.</param>
	/// <returns>Oluşan kilitleme olayı (BoardEvent).</returns>
	public BoardEvent? Lock(LockReason reason, DateTime nowLocal, DateTime nowUtc)
	{
		if (IsLocked)
		{
			return null;
		}

		IsLocked = true;
		UnlockedUntil = null;

		return new BoardEvent("LOCK", ToCode(reason), CurrentLesson(nowLocal) != null, nowUtc);
	}

	/// <summary>
	/// Her saniye çağrılarak sürenin dolup dolmadığını denetler.
	/// Süre dolmuşsa otomatik kilitleme uygular.
	/// </summary>
	public BoardEvent? Tick(DateTime nowLocal, DateTime nowUtc)
	{
		if (!IsLocked)
		{
			DateTime? unlockedUntil = UnlockedUntil;
			if (unlockedUntil.HasValue)
			{
				DateTime valueOrDefault = unlockedUntil.GetValueOrDefault();
				if (!(nowLocal < valueOrDefault))
				{
					// Süre doldu: Ders bitimi mi yoksa zaman aşımı mı olduğuna göre kilitle
					return Lock(_untilIsLessonEnd ? LockReason.LessonEnd : LockReason.Timeout, nowLocal, nowUtc);
				}
			}
		}
		return null;
	}

	/// <summary>
	/// Tahtanın kilitlenmesine az süre kalıp kalmadığını (sarı uyarı durumu) kontrol eder.
	/// </summary>
	public bool IsWarning(DateTime nowLocal)
	{
		if (!IsLocked)
		{
			DateTime? unlockedUntil = UnlockedUntil;
			if (unlockedUntil.HasValue)
			{
				DateTime valueOrDefault = unlockedUntil.GetValueOrDefault();
				return valueOrDefault - nowLocal <= TimeSpan.FromMinutes(Plan.LockWarningMinutes);
			}
		}
		return false;
	}

	/// <summary>
	/// Şu anda aktif olan ders dilimini döndürür (varsa).
	/// </summary>
	public LessonSlot? CurrentLesson(DateTime nowLocal)
	{
		TimeSpan t = nowLocal.TimeOfDay;
		return LessonsOn(nowLocal).FirstOrDefault((LessonSlot l) => l.StartTime <= t && t < l.EndTime);
	}

	/// <summary>
	/// Gün içindeki bir sonraki ders dilimini döndürür (varsa).
	/// </summary>
	public LessonSlot? NextLesson(DateTime nowLocal)
	{
		TimeSpan t = nowLocal.TimeOfDay;
		return LessonsOn(nowLocal).FirstOrDefault((LessonSlot l) => l.StartTime > t);
	}

	/// <summary>
	/// Ders programına göre tahtanın ne zamana kadar açık kalacağını hesaplar.
	/// Bir dersin içindeyse veya ders başlamak üzereyse dersin sonuna (ve ardışık blok derslerin sonuna) kadar açar.
	/// Ders dışındaysa OutsideLessonUnlockMinutes (10 dk) kadar açar.
	/// </summary>
	private (DateTime Until, bool IsLessonEnd) ComputeUnlockUntil(DateTime nowLocal)
	{
		TimeSpan t = nowLocal.TimeOfDay;
		DateTime defaultUntil = nowLocal.AddMinutes(Plan.OutsideLessonUnlockMinutes);
		TimeSpan horizon = t + TimeSpan.FromMinutes(Plan.OutsideLessonUnlockMinutes);
		TimeSpan? endOfLessons = null;

		// Şu anki zamana denk gelen veya yaklaşan ardışık dersleri tara
		using (IEnumerator<LessonSlot> enumerator = (from l in LessonsOn(nowLocal)
			where l.EndTime > t && l.StartTime <= horizon
			select l).GetEnumerator())
		{
			TimeSpan value;
			for (; enumerator.MoveNext(); endOfLessons = value)
			{
				LessonSlot current = enumerator.Current;
				if (endOfLessons.HasValue)
				{
					TimeSpan valueOrDefault = endOfLessons.GetValueOrDefault();
					if (current.StartTime >= valueOrDefault)
					{
						break; // Ardışık ders zinciri koptu
					}
				}
				if (endOfLessons.HasValue)
				{
					TimeSpan valueOrDefault2 = endOfLessons.GetValueOrDefault();
					if (valueOrDefault2 > current.EndTime)
					{
						value = valueOrDefault2;
						continue;
					}
				}
				value = current.EndTime;
			}
		}

		if (endOfLessons.HasValue)
		{
			TimeSpan valueOrDefault3 = endOfLessons.GetValueOrDefault();
			return (Until: nowLocal.Date + valueOrDefault3, IsLessonEnd: true);
		}

		return (Until: defaultUntil, IsLessonEnd: false);
	}

	/// <summary>
	/// Belirtilen tarihe ait günün derslerini sıralı olarak getirir.
	/// </summary>
	private IEnumerable<LessonSlot> LessonsOn(DateTime nowLocal)
	{
		int day = IsoDayOfWeek(nowLocal);
		return from l in Plan.Lessons
			where l.DayOfWeek == day
			orderby l.StartTime
			select l;
	}

	/// <summary>
	/// ISO-8601 standart haftanın günü (1: Pazartesi ... 7: Pazar).
	/// </summary>
	public static int IsoDayOfWeek(DateTime date)
	{
		return (int)(date.DayOfWeek + 6) % 7 + 1;
	}

	// Kilit gerekçesi metin kodları
	private static string ToCode(LockReason reason)
	{
		return reason switch
		{
			LockReason.Teacher => "TEACHER", 
			LockReason.Remote => "REMOTE", 
			LockReason.LessonEnd => "LESSON_END", 
			LockReason.Timeout => "TIMEOUT", 
			LockReason.Session => "SESSION", 
			_ => "SERVICE_DISABLED", 
		};
	}

	// Kilit açma yöntemi metin kodları
	private static string ToCode(UnlockReason reason)
	{
		return reason switch
		{
			UnlockReason.Password => "PASSWORD", 
			UnlockReason.Qr => "QR", 
			UnlockReason.Code => "CODE", 
			_ => "REMOTE", 
		};
	}
}
