using System;

namespace EduMindX.SmartBoard.Core;

/// <summary>
/// Akıllı tahta üzerinde gerçekleşen önemli güvenlik ve durum olaylarını temsil eden veri kaydı.
/// Bu olaylar sunucuya telemetri ve denetim (audit) amacıyla raporlanır.
/// </summary>
/// <param name="EventType">Olay türü: "LOCK" (kilitlendi), "UNLOCK" (açıldı), "FAILED_ATTEMPT" (hatalı şifre girişi).</param>
/// <param name="Reason">Olayın sebebi veya kilit açma/kapama yöntemi (Örn: "QR", "CODE", "PASSWORD", "LESSON_END", "TIMEOUT").</param>
/// <param name="InLesson">Olay anında resmi bir ders saatinde olunup olunmadığı.</param>
/// <param name="OccurredAtUtc">Olayın gerçekleştiği UTC zaman damgası.</param>
/// <param name="CommandGuid">Eğer olay uzaktan bir sunucu komutuyla tetiklendiyse ilgili komutun kimlik numarası.</param>
public sealed record BoardEvent(string EventType, string? Reason, bool InLesson, DateTime OccurredAtUtc, Guid? CommandGuid = null);
