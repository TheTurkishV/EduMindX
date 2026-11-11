using System;

namespace EduMindX.SmartBoard;

/// <summary>
/// Ekranda gösterilecek QR kodun içeriğini ve kalan geçerlilik süresini temsil eder.
/// </summary>
/// <param name="Url">QR koda gömülecek web adresi.</param>
/// <param name="Remaining">QR kodun geçerliliğini yitirmesine kalan süre.</param>
public sealed record QrDisplay(string Url, TimeSpan Remaining);
