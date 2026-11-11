namespace EduMindX.SmartBoard.Core;

/// <summary>
/// Sunucudan indirilen sürüm manifestosu modeli.
/// Yeni sürüm numarası, paket dosya adı ve SHA-256 özetini taşır.
/// </summary>
/// <param name="Version">Sürüm numarası (Örn: "1.5.1").</param>
/// <param name="PackageFile">İndirilecek ZIP paketinin dosya adı (Örn: "EduMindX-AkilliTahta-1.5.1.zip").</param>
/// <param name="Sha256">Paketin küçük harfli 64 karakterlik SHA-256 özeti.</param>
public sealed record UpdateManifest(string Version, string PackageFile, string Sha256);
