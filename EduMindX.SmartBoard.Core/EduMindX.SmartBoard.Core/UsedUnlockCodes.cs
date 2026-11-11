using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace EduMindX.SmartBoard.Core;

/// <summary>
/// Daha önce kullanılmış olan tek kullanımlık günlük kilit açma kodlarının takibini yapan sınıf.
/// Bir kod bir kez kullanıldıktan sonra tekrar kullanılamaz.
/// 7 günden eski kullanım kayıtları otomatik olarak temizlenir.
/// </summary>
public sealed class UsedUnlockCodes
{
	/// <summary>
	/// Kullanılmış kod kayıtlarının saklanma süresi (7 Gün).
	/// </summary>
	public static readonly TimeSpan Retention = TimeSpan.FromDays(7);

	// Kullanılan kod hash'leri ve kullanılma tarihleri
	private readonly Dictionary<string, DateTime> _used;

	/// <summary>
	/// Harcanmış kod kayıtlarının salt okunur sözlüğü.
	/// </summary>
	public IReadOnlyDictionary<string, DateTime> Entries => _used;

	/// <summary>
	/// Yeni bir kullanılmış kod takip örneği başlatır.
	/// </summary>
	/// <param name="saved">Diskten yüklenen önceki harcanmış kod kayıtları (varsa).</param>
	public UsedUnlockCodes(IDictionary<string, DateTime>? saved = null)
	{
		_used = ((saved != null) ? new Dictionary<string, DateTime>(saved, StringComparer.Ordinal) : new Dictionary<string, DateTime>(StringComparer.Ordinal));
	}

	/// <summary>
	/// Planda tanımlı olan ancak henüz kullanılmamış (aktif) bugünkü kod hash'lerini döndürür.
	/// </summary>
	/// <param name="plan">Yürürlükteki ders ve kilit planı.</param>
	/// <param name="nowLocal">Bugünün yerel tarihi.</param>
	/// <returns>Kullanılabilir kod hash'lerinin listesi.</returns>
	public IReadOnlyList<string> Available(LockPlan plan, DateTime nowLocal)
	{
		string today = nowLocal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		return (from h in plan.UnlockCodes.Where((UnlockCodeDay d) => d.Date == today).SelectMany((UnlockCodeDay d) => d.Hashes)
			where !_used.ContainsKey(h)
			select h).ToList();
	}

	/// <summary>
	/// Belirtilen kod hash'ini kullanılmış olarak işaretler ve eski kayıtları temizler.
	/// </summary>
	/// <param name="hash">Kullanılan kodun BCrypt hash'i.</param>
	/// <param name="nowLocal">Kullanıldığı anki yerel tarih.</param>
	public void MarkUsed(string hash, DateTime nowLocal)
	{
		// 7 günden eski kayıtları hafızadan düşür
		foreach (string item in (from e in _used
			where nowLocal.Date - e.Value > Retention
			select e.Key).ToList())
		{
			_used.Remove(item);
		}

		// Kodu bugünün tarihiyle kullanıldı olarak kaydet
		_used[hash] = nowLocal.Date;
	}
}
