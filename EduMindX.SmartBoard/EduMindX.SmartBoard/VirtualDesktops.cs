using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace EduMindX.SmartBoard;

/// <summary>
/// Windows 10/11 Sanal Masaüstü (Virtual Desktop) kaçışlarını engelleyen sınıf.
/// Windows COM arabirimini (IVirtualDesktopManager) kullanarak, kullanıcının yeni bir sanal masaüstü
/// oluşturması durumunda kilit ekranını veya yüzen çubuğu zorla o masaüstüne taşır.
/// </summary>
internal sealed class VirtualDesktops
{
	[ComImport]
	[Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IVirtualDesktopManager
	{
		[PreserveSig]
		int IsWindowOnCurrentVirtualDesktop(nint topLevelWindow, [MarshalAs(UnmanagedType.Bool)] out bool onCurrentDesktop);

		[PreserveSig]
		int GetWindowDesktopId(nint topLevelWindow, out Guid desktopId);

		[PreserveSig]
		int MoveWindowToDesktop(nint topLevelWindow, ref Guid desktopId);
	}

	[ComImport]
	[Guid("aa509086-5ca9-4c25-8f95-589d3c07b48a")]
	private class VirtualDesktopManagerClass
	{
	}

	private static readonly TimeSpan LogInterval = TimeSpan.FromMinutes(1L);
	private readonly Action<string> _log;
	private IVirtualDesktopManager? _manager;
	private bool _reconnect;
	private DateTime _lastLogUtc = DateTime.MinValue;

	public VirtualDesktops(Action<string> log)
	{
		_log = log;
		_manager = Create(log);
	}

	private static IVirtualDesktopManager? Create(Action<string> log)
	{
		try
		{
			return (IVirtualDesktopManager)new VirtualDesktopManagerClass();
		}
		catch (Exception ex) when (ex is COMException || ex is InvalidCastException)
		{
			log("Sanal masaustu yoneticisi yok: " + ex.Message);
			return null;
		}
	}

	/// <summary>
	/// Belirtilen pencerenin şu an aktif olarak görünen sanal masaüstünde kalmasını garanti eder.
	/// Kullanıcı başka bir masaüstüne geçtiyse pencereyi o masaüstüne çeker.
	/// </summary>
	public void KeepOnCurrent(Window window)
	{
		if (!window.IsVisible)
		{
			return;
		}

		if (_reconnect && (_manager = Create(LogThrottled)) != null)
		{
			_reconnect = false;
		}

		if (_manager == null)
		{
			return;
		}

		nint handle = new WindowInteropHelper(window).Handle;
		if (handle == IntPtr.Zero)
		{
			return;
		}

		try
		{
			// Pencere şu an görünen masaüstünde mi?
			if (_manager.IsWindowOnCurrentVirtualDesktop(handle, out var onCurrentDesktop) != 0)
			{
				_manager = null;
				_reconnect = true;
			}
			else
			{
				if (onCurrentDesktop)
				{
					return; // Zaten aktif masaüstünde, işlem gerekmez
				}

				// Aktif masaüstünün kimliğini al ve pencereyi oraya taşı
				Guid? guid = CurrentDesktopId();
				if (guid.HasValue)
				{
					Guid desktopId = guid.GetValueOrDefault();
					if (_manager.MoveWindowToDesktop(handle, ref desktopId) == 0)
					{
						LogThrottled(window.GetType().Name + " gorunen masaustune tasindi");
						return;
					}
				}

				// Taşıma API'si başarısız olduysa pencereyi gizleyip aktif ekranda yeniden aç
				window.Hide();
				window.Show();
				LogThrottled(window.GetType().Name + " gorunen masaustunde yeniden acildi");
			}
		}
		catch (COMException ex)
		{
			LogThrottled("Sanal masaustu kontrolu basarisiz: " + ex.Message);
		}
	}

	private Guid? CurrentDesktopId()
	{
		nint foregroundWindow = GetForegroundWindow();
		if (foregroundWindow == IntPtr.Zero)
		{
			return null;
		}

		if (_manager == null || _manager.IsWindowOnCurrentVirtualDesktop(foregroundWindow, out var onCurrentDesktop) != 0 || !onCurrentDesktop)
		{
			return null;
		}

		if (_manager.GetWindowDesktopId(foregroundWindow, out var desktopId) != 0 || !(desktopId != Guid.Empty))
		{
			return null;
		}

		return desktopId;
	}

	private void LogThrottled(string message)
	{
		DateTime utcNow = DateTime.UtcNow;
		if (!(utcNow - _lastLogUtc < LogInterval))
		{
			_lastLogUtc = utcNow;
			_log(message);
		}
	}

	[DllImport("user32.dll")]
	private static extern nint GetForegroundWindow();
}
