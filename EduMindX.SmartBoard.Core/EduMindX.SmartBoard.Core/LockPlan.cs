using System;
using System.Collections.Generic;

namespace EduMindX.SmartBoard.Core;

/// <summary>
/// Sunucudan indirilen ve tahtanın tüm kilit davranışını, ders programını ve şifrelerini belirleyen plan modeli.
/// Bu model sunucu tarafından ECDSA algoritmasıyla imzalanır.
/// </summary>
public sealed class LockPlan
{
	/// <summary>
	/// Planın sunucu tarafından oluşturulduğu andaki UTC zamanı.
	/// </summary>
	public DateTime ServerUtc { get; set; }

	/// <summary>
	/// Yerel saat dilimi ofseti (dakika cinsinden). Varsayılan 180 (UTC+3, Türkiye).
	/// </summary>
	public int UtcOffsetMinutes { get; set; } = 180;

	/// <summary>
	/// Bu planın ait olduğu tahtanın benzersiz kimliği (Guid).
	/// Cihaz GUID'si tahtanın kendi kimliğiyle eşleşmezse plan reddedilir.
	/// </summary>
	public Guid DeviceGuid { get; set; }

	/// <summary>
	/// Tahtanın tanımlı adı (Örn: "Akıllı Tahta 1").
	/// </summary>
	public string DeviceName { get; set; } = string.Empty;

	/// <summary>
	/// Okul veya şube adı (Örn: "Ana Kampüs - A Blok").
	/// </summary>
	public string? BranchName { get; set; }

	/// <summary>
	/// Eğitim kademesi adı (Örn: "Lise", "Ortaokul").
	/// </summary>
	public string? EducationLevelName { get; set; }

	/// <summary>
	/// Tahta hizmetinin aktif olup olmadığını belirtir.
	/// False ise tahta "Hizmet Dışı" moduna geçer ve kilit açılmaz.
	/// </summary>
	public bool ServiceEnabled { get; set; } = true;

	/// <summary>
	/// Kilit açma ana şifresinin BCrypt formatındaki hash değeri.
	/// </summary>
	public string? UnlockPasswordHash { get; set; }

	/// <summary>
	/// Resmi ders saatleri dışında tahta açıldığında kaç dakika açık kalacağı.
	/// Varsayılan: 10 dakika.
	/// </summary>
	public int OutsideLessonUnlockMinutes { get; set; } = 10;

	/// <summary>
	/// Tahta kilitlenmeden kaç dakika önce kullanıcıya görsel sarı uyarı gösterileceği.
	/// Varsayılan: 2 dakika.
	/// </summary>
	public int LockWarningMinutes { get; set; } = 2;

	/// <summary>
	/// Haftalık resmi ders saatleri listesi.
	/// </summary>
	public List<LessonSlot> Lessons { get; set; } = new List<LessonSlot>();

	/// <summary>
	/// Çevrimdışı acil kilit açma için günlere ayrılmış tek kullanımlık kod hash'leri listesi.
	/// </summary>
	public List<UnlockCodeDay> UnlockCodes { get; set; } = new List<UnlockCodeDay>();
}
