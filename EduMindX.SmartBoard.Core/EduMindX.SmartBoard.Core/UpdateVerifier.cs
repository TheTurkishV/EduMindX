using System;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EduMindX.SmartBoard.Core;

/// <summary>
/// İndirilen güncelleme manifestosunun dijital imzasını, sürüm numarasını ve
/// dosya güvenliğini (Directory Traversal engelleme) doğrulayan yardımcı sınıf.
/// </summary>
public static class UpdateVerifier
{
	/// <summary>
	/// Güncelleme paketlerini imzalamak için kullanılan resmi ECDSA genel anahtarı.
	/// </summary>
	public const string ReleasePublicKey = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEk8w3LBrpmdHfk5iRYZiD9hpbuy/puUj8qJ/qc2+7kyl0XERYW+CFasVFz+iaDqKwxFjfZZjrUFaC12boz6zOWw==";

	private static readonly JsonSerializerOptions Json = new JsonSerializerOptions(JsonSerializerDefaults.Web);

	/// <summary>
	/// İmzalı güncelleme belgesini doğrular ve geçerliyse UpdateManifest olarak deserialize eder.
	/// </summary>
	public static UpdateManifest? Verify(SignedDocument? signed, string? publicKeyBase64)
	{
		if (signed == null || !Signatures.IsValid(signed.Payload, signed.Signature, publicKeyBase64))
		{
			return null;
		}

		try
		{
			UpdateManifest? updateManifest = JsonSerializer.Deserialize<UpdateManifest>(signed.Payload, Json);
			return (updateManifest != null && IsWellFormed(updateManifest)) ? updateManifest : null;
		}
		catch (JsonException)
		{
			return null;
		}
	}

	/// <summary>
	/// Manifestodaki sürüm, dosya adı ve sha256 özetinin beklenen güvenlik standartlarına
	/// uyup uymadığını (Dizin atlatma ".." açığı olmamasını) doğrular.
	/// </summary>
	public static bool IsWellFormed(UpdateManifest manifest)
	{
		// Dosya adı regex'e uymalı, ".." içermemeli ve SHA-256 64 haneli hex olmalı
		if (IsCanonicalVersion(manifest.Version) && PackageName().IsMatch(manifest.PackageFile) && !manifest.PackageFile.Contains(".."))
		{
			return Sha256Hex().IsMatch(manifest.Sha256);
		}
		return false;
	}

	/// <summary>
	/// Sürüm numarasının semantik formatta (X.Y.Z) olup olmadığını kontrol eder.
	/// </summary>
	public static bool IsCanonicalVersion(string? version)
	{
		if (version != null)
		{
			return CanonicalVersion().IsMatch(version);
		}
		return false;
	}

	/// <summary>
	/// Aday sürümün mevcut kurulu sürümden daha yeni olup olmadığını karşılaştırır.
	/// </summary>
	public static bool IsNewer(string candidate, string current)
	{
		if (Version.TryParse(candidate, out Version? result) && Version.TryParse(current, out Version? result2))
		{
			return Normalize(result) > Normalize(result2);
		}
		return false;
	}

	private static Version Normalize(Version v)
	{
		return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
	}

	// Yalnızca alfanümerik, nokta, tire ve altçizgili zip/apk paket isimlerine izin ver
	private static readonly Regex _packageName = new Regex("^[A-Za-z0-9._-]+\\.(zip|apk)$", RegexOptions.Compiled);
	private static Regex PackageName() => _packageName;

	// 64 karakterlik SHA-256 hex doğrulaması
	private static readonly Regex _sha256Hex = new Regex("^[0-9a-f]{64}$", RegexOptions.Compiled);
	private static Regex Sha256Hex() => _sha256Hex;

	// Standart semantik sürüm doğrulaması (Örn: 1.5.1)
	private static readonly Regex _canonicalVersion = new Regex("^(0|[1-9][0-9]{0,4})\\.(0|[1-9][0-9]{0,4})\\.(0|[1-9][0-9]{0,4})$", RegexOptions.Compiled);
	private static Regex CanonicalVersion() => _canonicalVersion;
}
