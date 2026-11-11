using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BCrypt.Net;

namespace EduMindX.SmartBoard.Core;

/// <summary>
/// Kaba kuvvet (Brute-Force) şifre denemelerine karşı koruma sağlayan güvenlik kapısı.
/// Hatalı deneme sayısını takip eder, 5 hatalı denemede sistemi geçici olarak kilitler
/// ve bekleme süresini kademeli olarak (1 dk'dan 10 dk'ya kadar) artırır.
/// Zaman hesaplamasında sistem saati hilelerine karşı Stopwatch monotonic tick sayacını kullanır.
/// </summary>
public sealed class PasswordGate
{
	/// <summary>
	/// Kilitlenmeye yol açacak maksimum ardışık hatalı deneme sayısı.
	/// </summary>
	public const int MaxAttempts = 5;

	// Kilitlenme süresi artış adımı (Her seferinde 1 dakika artar)
	private static readonly TimeSpan LockoutStep = TimeSpan.FromMinutes(1L);

	// Maksimum kilitlenme bekleme süresi (10 Dakika)
	private static readonly TimeSpan LockoutMax = TimeSpan.FromMinutes(10L);

	// Monotonic tick sayacı fonksiyonu
	private readonly Func<long> _monotonicTicks;

	// Mevcut ardışık başarısız deneme sayısı
	private int _failures;

	// Kaç kez kilitlenmeye düşüldüğü sayacı (kademeli süreyi hesaplamak için)
	private int _lockouts;

	// Son kilitlenmenin başladığı tick değeri
	private long? _lockoutStartTicks;

	// Aktif kilitlenmenin toplam süresi
	private TimeSpan _lockoutDuration;

	/// <summary>
	/// Eğer sistem hatalı denemelerden ötürü kilitlendiyse, kalan bekleme süresini döndürür.
	/// Kilitli değilse veya süre dolmuşsa null döner.
	/// </summary>
	public TimeSpan? LockoutRemaining
	{
		get
		{
			long? lockoutStartTicks = _lockoutStartTicks;
			if (lockoutStartTicks.HasValue)
			{
				long valueOrDefault = lockoutStartTicks.GetValueOrDefault();
				TimeSpan timeSpan = _lockoutDuration - Stopwatch.GetElapsedTime(valueOrDefault, _monotonicTicks());
				if (timeSpan > TimeSpan.Zero)
				{
					return timeSpan;
				}
				_lockoutStartTicks = null;
				return null;
			}
			return null;
		}
	}

	/// <summary>
	/// Varsayılan Stopwatch zamanlayıcısı ile güvenlik kapısını başlatır.
	/// </summary>
	public PasswordGate()
		: this(Stopwatch.GetTimestamp)
	{
	}

	/// <summary>
	/// Test edilebilirliği sağlamak için özel zamanlayıcı ile başlatıcı.
	/// </summary>
	public PasswordGate(Func<long> monotonicTicks)
	{
		_monotonicTicks = monotonicTicks;
	}

	/// <summary>
	/// Girilen PIN'i hem günlük tek kullanımlık kodlarla hem de tahtanın ana şifresiyle BCrypt kullanarak karşılaştırır.
	/// </summary>
	/// <param name="pin">Kullanıcının girdiği 8 haneli kod/şifre.</param>
	/// <param name="codeHashes">Bugün için geçerli olan tek kullanımlık kod hash'leri.</param>
	/// <param name="passwordHash">Tahtanın ana açılış şifresi hash'i.</param>
	/// <returns>Kontrol sonucu ve eşleşen kod hash'i.</returns>
	public PinCheck CheckPin(string pin, IReadOnlyList<string> codeHashes, string? passwordHash)
	{
		// Eğer geçici kilitlenme (lockout) süresi dolmamışsa reddet
		if (LockoutRemaining.HasValue)
		{
			return new PinCheck(PasswordResult.LockedOut, null);
		}

		// Kontrol edilecek tüm hash listesini hazırla (günlük kodlar + ana şifre)
		List<(string, bool)> list = codeHashes.Select((string h) => (Hash: h, IsCode: true))
			.Append((passwordHash ?? string.Empty, false))
			.Where(((string Hash, bool IsCode) c) => !string.IsNullOrEmpty(c.Hash))
			.ToList();

		if (list.Count == 0)
		{
			return new PinCheck(PasswordResult.NoPassword, null);
		}

		int verifiedCount = 0;
		foreach (var item in list)
		{
			bool flag;
			try
			{
				// BCrypt ile güvenli hash doğrulaması
				flag = BCrypt.Net.BCrypt.Verify(pin, item.Item1);
				verifiedCount++;
			}
			catch (Exception)
			{
				continue;
			}

			if (flag)
			{
				// Doğrulama başarılı: Başarısızlık sayaçlarını sıfırla
				_failures = 0;
				_lockouts = 0;
				string? matchedCodeHash = item.Item2 ? item.Item1 : null;
				return new PinCheck(PasswordResult.Success, matchedCodeHash);
			}
		}

		if (verifiedCount == 0)
		{
			return new PinCheck(PasswordResult.NoPassword, null);
		}

		// Hatalı deneme sayısını artır ve gerekirse kilitlenmeye sok
		return new PinCheck(CountFailure(), null);
	}

	// Başarısız denemeyi sayar ve 5 hataya ulaşıldığında kilitlenme başlatır
	private PasswordResult CountFailure()
	{
		_failures++;
		if (_failures < 5)
		{
			return PasswordResult.Wrong;
		}

		// 5 hatalı denemede kilitlenmeye geç
		_failures = 0;
		_lockouts++;
		_lockoutDuration = TimeSpan.FromTicks(Math.Min(LockoutStep.Ticks * _lockouts, LockoutMax.Ticks));
		_lockoutStartTicks = _monotonicTicks();
		return PasswordResult.LockedOut;
	}
}
