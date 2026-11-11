using System;
using System.Collections.Generic;
using System.Linq;

namespace EduMindX.SmartBoard.Core;

/// <summary>
/// Sunucudan gelen uzaktan komutların tekrarlanmasını (Replay Attack) veya mükerrer çalıştırılmasını
/// engelleyen tekillik ve geçmiş takip yöneticisi (Idempotency Manager).
/// </summary>
public sealed class HandledCommands
{
	/// <summary>
	/// İşlenmiş komutların hafızada tutulacağı maksimum süre (1 Saat).
	/// Bu süreden eski kayıtlar bellekten temizlenir.
	/// </summary>
	public static readonly TimeSpan Retention = TimeSpan.FromHours(1);

	// İşlenen komut kimlikleri ve işlenme zamanları
	private readonly Dictionary<Guid, DateTime> _handled;

	/// <summary>
	/// İşlenmiş komut kayıtlarının salt okunur sözlüğü.
	/// </summary>
	public IReadOnlyDictionary<Guid, DateTime> Entries => _handled;

	/// <summary>
	/// Yeni bir komut takip örneği başlatır.
	/// </summary>
	/// <param name="saved">Diskten yüklenen önceki komut kayıtları (varsa).</param>
	public HandledCommands(IDictionary<Guid, DateTime>? saved = null)
	{
		_handled = ((saved != null) ? new Dictionary<Guid, DateTime>(saved) : new Dictionary<Guid, DateTime>());
	}

	/// <summary>
	/// Belirtilen komutun daha önce işlenip işlenmediğini kontrol eder.
	/// Eğer işlenmemişse listeye ekler ve komutun yürütülmesine izin verir.
	/// </summary>
	/// <param name="commandGuid">Komutun benzersiz kimliği.</param>
	/// <param name="nowUtc">Şu anki UTC zamanı.</param>
	/// <returns>Komut ilk defa işleniyorsa true; daha önce işlendiyse false.</returns>
	public bool TryBegin(Guid commandGuid, DateTime nowUtc)
	{
		// 1 saatten eski kayıtları temizle
		foreach (Guid item in (from e in _handled
			where nowUtc - e.Value > Retention
			select e.Key).ToList())
		{
			_handled.Remove(item);
		}

		// Komut zaten varsa false, ilk defa ekleniyorsa true döner
		return _handled.TryAdd(commandGuid, nowUtc);
	}
}
