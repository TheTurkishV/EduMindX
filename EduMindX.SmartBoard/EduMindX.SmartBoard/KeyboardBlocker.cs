using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace EduMindX.SmartBoard;

/// <summary>
/// Windows Low-Level Klavye Kancası (WH_KEYBOARD_LL) kurarak
/// kilit modunda öğrencilerin sistem kısayollarını (Win, Alt+Tab, Alt+F4 vb.)
/// kullanmasını engelleyen donanımsal engelleyici.
/// </summary>
public sealed class KeyboardBlocker : IDisposable
{
	private delegate nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam);

	[StructLayout(LayoutKind.Sequential)]
	private struct KbdLlHookStruct
	{
		public int VkCode;
		public int ScanCode;
		public int Flags;
		public int Time;
		public nint ExtraInfo;
	}

	// Win32 Klavye Kanca Sabitleri
	private const int WhKeyboardLl = 13;
	private const int LlkhfAltDown = 32;

	// Sanal Tuş Kodları (Virtual Key Codes)
	private const int VkTab = 9;
	private const int VkEscape = 27;
	private const int VkF4 = 115;
	private const int VkLWin = 91;
	private const int VkRWin = 92;
	private const int VkApps = 93;
	private const int VkControl = 17;

	private readonly LowLevelKeyboardProc _proc;
	private nint _hook;

	/// <summary>
	/// Tuş engellemenin aktif olup olmadığını belirtir.
	/// Kilit açıldığında false yapılarak tuşlar serbest bırakılır.
	/// </summary>
	public bool Enabled { get; set; }

	/// <summary>
	/// Kancanın sisteme başarıyla kurulup kurulmadığını belirtir.
	/// </summary>
	public bool IsInstalled => _hook != IntPtr.Zero;

	public KeyboardBlocker()
	{
		_proc = HookCallback;
	}

	/// <summary>
	/// Düşük seviyeli klavye kancasını Windows sistemine enjekte eder.
	/// </summary>
	/// <returns>Hata durumunda Win32 hata kodu; başarılı ise null.</returns>
	public int? Install()
	{
		if (_hook != IntPtr.Zero)
		{
			return null;
		}

		using ProcessModule? processModule = Process.GetCurrentProcess().MainModule;
		if (processModule == null) return -1;

		_hook = SetWindowsHookEx(WhKeyboardLl, _proc, GetModuleHandle(processModule.ModuleName), 0u);
		return (_hook == IntPtr.Zero) ? new int?(Marshal.GetLastWin32Error()) : ((int?)null);
	}

	/// <summary>
	/// Kancayı sistemden temizler ve bellek sızıntısını önler.
	/// </summary>
	public void Dispose()
	{
		if (_hook != IntPtr.Zero)
		{
			UnhookWindowsHookEx(_hook);
			_hook = IntPtr.Zero;
		}
	}

	/// <summary>
	/// Her klavye tuş vuruşunda Windows tarafından çağrılan geri çağırma (callback) fonksiyonu.
	/// </summary>
	private nint HookCallback(int nCode, nint wParam, nint lParam)
	{
		if (nCode >= 0 && Enabled && ShouldBlock(Marshal.PtrToStructure<KbdLlHookStruct>(lParam)))
		{
			// 1 dönerek tuş vuruşunu yut ve Windows'a veya uygulamalara ulaşmasını engelle
			return 1;
		}
		return CallNextHookEx(_hook, nCode, wParam, lParam);
	}

	/// <summary>
	/// Basılan tuşun kilitli modda engellenmesi gereken bir kısayol olup olmadığını denetler.
	/// </summary>
	private static bool ShouldBlock(KbdLlHookStruct key)
	{
		bool altDown = (key.Flags & LlkhfAltDown) != 0;
		bool ctrlDown = (GetAsyncKeyState(VkControl) & 0x8000) != 0;

		switch (key.VkCode)
		{
			// Windows Tuşları ve Uygulama/Menü Tuşunu daima engelle
			case VkLWin:
			case VkRWin:
			case VkApps:
				return true;

			// Alt + Tab (Uygulama değiştiriciyi engelle)
			case VkTab:
				return altDown;

			// Alt + Escape ve Ctrl + Escape (Başlat menüsü açılışını engelle)
			case VkEscape:
				return altDown || ctrlDown;

			// Alt + F4 (Pencere kapatmayı engelle)
			case VkF4:
				return altDown;

			default:
				return false;
		}
	}

	[DllImport("user32.dll", SetLastError = true)]
	private static extern nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool UnhookWindowsHookEx(nint hhk);

	[DllImport("user32.dll")]
	private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

	[DllImport("user32.dll")]
	private static extern short GetAsyncKeyState(int vKey);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern nint GetModuleHandle(string lpModuleName);
}
