using System;
using System.IO;
using System.Security;
using Microsoft.Win32;

namespace EduMindX.SmartBoard;

/// <summary>
/// Dokunmatik ekranlarda öğrencilerin 3 veya 4 parmakla ekranı kaydırarak
/// Görev Görünümü'nü (Task View) açmasını veya masaüstü değiştirmesini Registry üzerinden engelleyen sınıf.
/// </summary>
internal static class TouchGestures
{
	private const string KeyPath = "Control Panel\\Desktop";
	private const string ValueName = "TouchGestureSetting";

	/// <summary>
	/// Çoklu parmak dokunmatik hareketlerini Windows Kayıt Defteri'nde sıfırlayarak devre dışı bırakır.
	/// </summary>
	public static void Disable(Action<string> log)
	{
		try
		{
			using RegistryKey? registryKey = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
			object? value = registryKey?.GetValue(ValueName);
			if (!(value is int) || (int)value != 0)
			{
				registryKey?.SetValue(ValueName, 0, RegistryValueKind.DWord);
				log("Uc/dort parmak dokunma hareketleri kapatildi (oturum yenilenince etkin)");
			}
		}
		catch (Exception ex) when (ex is UnauthorizedAccessException || ex is SecurityException || ex is IOException)
		{
			log("Dokunma hareketleri kapatilamadi: " + ex.Message);
		}
	}
}
