using System;

namespace EduMindX.SmartBoard.Core;

/// <summary>
/// Haftalık ders programındaki tek bir ders dilimini (başlangıç, bitiş ve gün bilgisi) temsil eder.
/// </summary>
public sealed class LessonSlot
{
	/// <summary>
	/// Dersin gerçekleştiği gün (1: Pazartesi ... 7: Pazar, ISO-8601 standardı).
	/// </summary>
	public int DayOfWeek { get; set; }

	/// <summary>
	/// Dersin başlangıç saati dize formatında (Örn: "08:30" veya "08:30:00").
	/// </summary>
	public string Start { get; set; } = string.Empty;

	/// <summary>
	/// Dersin bitiş saati dize formatında (Örn: "09:10" veya "09:10:00").
	/// </summary>
	public string End { get; set; } = string.Empty;

	/// <summary>
	/// Başlangıç saatini TimeSpan türünde döndürür.
	/// </summary>
	public TimeSpan StartTime => TimeSpan.Parse(Start);

	/// <summary>
	/// Bitiş saatini TimeSpan türünde döndürür.
	/// </summary>
	public TimeSpan EndTime => TimeSpan.Parse(End);
}
