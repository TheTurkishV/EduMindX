using System;
using EduMindX.SmartBoard.Core;

namespace EduMindX.SmartBoard;

/// <summary>
/// Kilit ekranının (LockWindow) anlık görsel durumunu temsil eden durum modeli.
/// </summary>
/// <param name="NowLocal">Hesaplanan hile korumalı yerel saat.</param>
/// <param name="Online">Sunucuya internet/ağ bağlantısının açık olup olmadığı.</param>
/// <param name="ServiceEnabled">Tahtanın hizmette olup olmadığı.</param>
/// <param name="HasPassword">Tahtada geçerli bir şifre veya günlük kod tanımlı olup olmadığı.</param>
/// <param name="CurrentLesson">Şu anda sürmekte olan ders dilimi (varsa).</param>
/// <param name="NextLesson">Bir sonraki başlayacak ders dilimi (varsa).</param>
/// <param name="Identity">Tahtanın şube, kademe ve cihaz adı bilgisi (Örn: "A Blok · Lise · Tahta 1").</param>
/// <param name="ConnectionStatus">Bağlantı durum metni ("Çevrimiçi", "Çevrimdışı" vb.).</param>
/// <param name="LockoutRemaining">Hatalı şifre denemelerinden dolayı geçici kilitlenme varsa kalan süre.</param>
public sealed record LockScreenState(
	DateTime NowLocal,
	bool Online,
	bool ServiceEnabled,
	bool HasPassword,
	LessonSlot? CurrentLesson,
	LessonSlot? NextLesson,
	string Identity,
	string ConnectionStatus,
	TimeSpan? LockoutRemaining
);
