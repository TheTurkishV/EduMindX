using System;
using System.Security.Cryptography;
using System.Text;

namespace EduMindX.SmartBoard.Core;

/// <summary>
/// Sunucudan gelen verilerin bütünlüğünü ve kaynağını doğrulamak için kullanılan
/// ECDSA (Elliptic Curve Digital Signature Algorithm) imza doğrulama yardımcısı.
/// </summary>
public static class Signatures
{
	/// <summary>
	/// Verilen veri yükünün (payload), sunucunun genel anahtarı ile imzalanıp imzalanmadığını SHA-256 ile doğrular.
	/// </summary>
	/// <param name="payload">Doğrulanacak ham veri yükü (JSON metni).</param>
	/// <param name="signatureBase64">Base64 formatında dijital imza.</param>
	/// <param name="publicKeyBase64">Sunucunun Base64 formatında SPKI genel anahtarı.</param>
	/// <returns>İmza geçerliyse true; geçersiz veya bozuksa false.</returns>
	public static bool IsValid(string payload, string signatureBase64, string? publicKeyBase64)
	{
		if (string.IsNullOrEmpty(publicKeyBase64) || string.IsNullOrEmpty(signatureBase64))
		{
			return false;
		}

		try
		{
			using ECDsa ecdsa = ECDsa.Create();
			// SPKI (SubjectPublicKeyInfo) formatındaki genel anahtarı içe aktar
			ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out var _);
			
			// Veriyi UTF-8 baytlarına çevirip SHA-256 ile imzayı doğrula
			return ecdsa.VerifyData(
				Encoding.UTF8.GetBytes(payload),
				Convert.FromBase64String(signatureBase64),
				HashAlgorithmName.SHA256
			);
		}
		catch (Exception ex) when (ex is FormatException || ex is CryptographicException)
		{
			return false;
		}
	}
}
