using System;
using System.IO;
using System.Security;
using Microsoft.Win32;

namespace EduMindX.SmartBoard;

/// <summary>
/// Akıllı tahtalarda Windows Tablet Modu davranışlarını yöneten ve otomatik geçişi engelleyen sınıf.
/// Tablet modu açıldığında pencereler zorla tam ekrana alınabildiği için kilit aşımı riskini önler.
/// </summary>
internal static class TabletMode
{
	private const string KeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\ImmersiveShell";
	private const string ModeValue = "TabletMode";

	private static readonly (string Name, int Value)[] Preferences = new (string, int)[2]
	{
		("SignInMode", 1), // 1: Oturum açıldığında masaüstü modunu kullan
		("ConvertibleSlateModePromptPreference", 0) // 0: Tablet moduna geçişi sorma/otomatik geçme
	};

	/// <summary>
	/// Windows'un otomatik tablet moduna geçiş tercihini devre dışı bırakır.
	/// </summary>
	public static void PreventAutoSwitch(Action<string> log)
	{
		try
		{
			using RegistryKey? registryKey = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
			if (registryKey == null) return;

			foreach (var (name, num) in Preferences)
			{
				if (!(registryKey.GetValue(name) is int val) || val != num)
				{
					registryKey.SetValue(name, num, RegistryValueKind.DWord);
				}
			}
		}
		catch (Exception ex) when (ex is UnauthorizedAccessException || ex is SecurityException || ex is IOException)
		{
			log("Tablet modu ayarlari yazilamadi: " + ex.Message);
		}
	}

	/// <summary>
	/// Eğer Tablet Modu o an açıksa zorla kapatır.
	/// </summary>
	/// <returns>Açık bulunup kapatıldıysa true; zaten kapalıysa false.</returns>
	public static bool TurnOffIfOn(Action<string> log)
	{
		try
		{
			using RegistryKey? registryKey = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
			object? obj = registryKey?.GetValue(ModeValue);
			if (!(obj is int) || (int)obj == 0)
			{
				return false;
			}

			registryKey?.SetValue(ModeValue, 0, RegistryValueKind.DWord);
			return true;
		}
		catch (Exception ex) when (ex is UnauthorizedAccessException || ex is SecurityException || ex is IOException)
		{
			log("Tablet modu kapatilamadi: " + ex.Message);
			return false;
		}
	}
}
