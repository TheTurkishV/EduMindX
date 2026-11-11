using System;

namespace EduMindX.SmartBoard.Core;

/// <summary>
/// Yüzen çubuğun (UnlockedBar) ekran üzerindeki orantısal (normalize edilmiş 0.0 - 1.0) konumunu temsil eder.
/// Ekran çözünürlüğü veya boyutu değiştiğinde çubuğun ekrandan taşmasını engeller.
/// </summary>
/// <param name="X">Yatay eksendeki normalize konum (0.0: Sol kenar, 1.0: Sağ kenar).</param>
/// <param name="Y">Dikey eksendeki normalize konum (0.0: Üst kenar, 1.0: Alt kenar).</param>
public sealed record FloatingPosition(double X, double Y)
{
	/// <summary>
	/// Varsayılan başlangıç konumu: Ekranın sağ kenarı, üstten %25 oranında aşağıda.
	/// </summary>
	public static FloatingPosition Default { get; } = new FloatingPosition(1.0, 0.25);

	/// <summary>
	/// Çubuğun ekranın sağ yarısında olup olmadığını kontrol eder.
	/// Bu bilgiye göre açılan detay paneli sola veya sağa doğru hizalanır.
	/// </summary>
	public bool IsOnRightHalf => X >= 0.5;

	/// <summary>
	/// Ekran kenarlarından bırakılacak emniyet payı (piksel cinsinden).
	/// </summary>
	public const double Margin = 16.0;

	/// <summary>
	/// X ve Y değerlerini 0.0 ile 1.0 aralığına sınırlar (sonsuz veya geçersiz sayıları düzeltir).
	/// </summary>
	public FloatingPosition Normalized()
	{
		if (!double.IsFinite(X) || !double.IsFinite(Y))
		{
			return Default;
		}
		return new FloatingPosition(Math.Clamp(X, 0.0, 1.0), Math.Clamp(Y, 0.0, 1.0));
	}

	/// <summary>
	/// Verilen ekran alanında çubuğun mutlak piksel koordinatlarını (Sol, Üst) hesaplar.
	/// </summary>
	public (double Left, double Top) Place(ScreenArea area, double width, double height)
	{
		FloatingPosition floatingPosition = Normalized();
		return (
			Left: area.Left + 16.0 + floatingPosition.X * Free(area.Width, width),
			Top: area.Top + 16.0 + floatingPosition.Y * Free(area.Height, height)
		);
	}

	/// <summary>
	/// Sürükleme sonrası elde edilen mutlak piksel koordinatlarını tekrar normalize oranlara dönüştürür.
	/// </summary>
	public static FloatingPosition FromPlacement(ScreenArea area, double width, double height, double left, double top)
	{
		return new FloatingPosition(
			Fraction(left - area.Left - 16.0, Free(area.Width, width)),
			Fraction(top - area.Top - 16.0, Free(area.Height, height))
		);
	}

	// Kullanılabilir serbest hareket alanını hesaplar
	private static double Free(double total, double size)
	{
		return Math.Max(0.0, total - size - 32.0);
	}

	// Oranı 0.0 ile 1.0 arasında sınırlar
	private static double Fraction(double offset, double free)
	{
		if (!(free <= 0.0))
		{
			return Math.Clamp(offset / free, 0.0, 1.0);
		}
		return 0.0;
	}
}
