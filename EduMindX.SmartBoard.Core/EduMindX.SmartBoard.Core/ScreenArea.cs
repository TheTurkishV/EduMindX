namespace EduMindX.SmartBoard.Core;

/// <summary>
/// Bir ekranın veya çalışma alanının (WorkArea) fiziksel koordinatlarını ve boyutlarını temsil eder.
/// </summary>
public readonly record struct ScreenArea(double Left, double Top, double Width, double Height);
