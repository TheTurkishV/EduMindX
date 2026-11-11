using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace EduMindX.SmartBoard;

/// <summary>
/// Windows kabuk yöneticisi (Shell Guard).
/// Tahta kilitlendiğinde Windows Gezgini'ni (explorer.exe) ntdll!NtSuspendProcess ile dondurur,
/// başlat menüsü ve arama süreçlerini öldürür; kilit açıldığında kabuğu yeniden uyandırır (Thaw).
/// </summary>
internal sealed class ShellGuard
{
	private const string Explorer = "explorer";
	private const int ProcessSuspendResume = 2048; // PROCESS_SUSPEND_RESUME erişim yetkisi bayrağı
	private const int MaxResumeAttempts = 8;

	// Kilit modunda sonlandırılacak UWP kabuk host süreçleri
	private static readonly string[] ShellHosts = new string[6]
	{
		"StartMenuExperienceHost",
		"ShellExperienceHost",
		"ShellHost",
		"SearchHost",
		"SearchApp",
		"SearchUI"
	};

	private static readonly TimeSpan RestartGrace = TimeSpan.FromSeconds(20L);
	private static readonly TimeSpan RestartCooldown = TimeSpan.FromMinutes(1L);

	private readonly int _session = Process.GetCurrentProcess().SessionId;
	private readonly Action<string> _log;
	private readonly HashSet<string> _reportedForeground = new HashSet<string>();

	private DateTime? _missingSinceUtc;
	private DateTime _lastRestartUtc = DateTime.MinValue;

	/// <summary>
	/// Windows kabuğunun şu an dondurulmuş (askıda) olup olmadığını belirtir.
	/// </summary>
	public bool Frozen { get; private set; } = true;

	public ShellGuard(Action<string> log)
	{
		_log = log;
	}

	/// <summary>
	/// Windows Gezgini'ni dondurur ve arayüz UWP süreçlerini öldürür.
	/// </summary>
	public void Freeze()
	{
		bool stateChanged = !Frozen;
		Frozen = true;

		// Mevcut oturuma ait explorer.exe süreçlerini askıya al
		foreach (Process item in SessionProcesses(Explorer))
		{
			using (item)
			{
				if (!IsSuspended(item) && Suspend(item))
				{
					stateChanged = true;
					_log($"Kilitli: Windows kabugu donduruldu (pid {item.Id})");
				}
			}
		}

		if (!stateChanged)
		{
			return;
		}

		// Başlat menüsü ve Arama host süreçlerini kapat
		string[] shellHosts = ShellHosts;
		foreach (string hostName in shellHosts)
		{
			foreach (Process item2 in SessionProcesses(hostName))
			{
				if (Kill(item2))
				{
					_log("Kilitli: " + hostName + " kapatildi");
				}
			}
		}
	}

	/// <summary>
	/// Dondurulan Windows Gezgini'ni çözer (uyandırır).
	/// Eğer explorer.exe kapandıysa yeni bir tane başlatır.
	/// </summary>
	public void Thaw()
	{
		_reportedForeground.Clear();
		List<Process> list = SessionProcesses(Explorer);
		bool hasExplorer = list.Count > 0;

		if (Frozen)
		{
			bool allResumed = true;
			foreach (Process item in list)
			{
				allResumed &= ResumeFully(item);
			}
			Frozen = !allResumed;
		}

		foreach (Process item2 in list)
		{
			item2.Dispose();
		}

		if (hasExplorer)
		{
			_missingSinceUtc = null;
			return;
		}

		// Eğer explorer.exe çalışmıyorsa 20 saniyelik tolerans sonrası yeniden başlat
		DateTime utcNow = DateTime.UtcNow;
		if (!_missingSinceUtc.HasValue)
		{
			_missingSinceUtc = utcNow;
		}

		if (utcNow - _missingSinceUtc < RestartGrace)
		{
			return;
		}

		_missingSinceUtc = null;
		try
		{
			Process.Start(new ProcessStartInfo("explorer.exe")
			{
				UseShellExecute = false
			})?.Dispose();
			_log("Windows kabugu calismiyordu; baslatildi");
		}
		catch (Win32Exception ex)
		{
			_log("Windows kabugu baslatilamadi: " + ex.Message);
		}
	}

	/// <summary>
	/// Belirli bir sebeple explorer.exe'yi zorla sonlandırıp yeniden başlatır.
	/// </summary>
	public void Restart(string reason)
	{
		DateTime utcNow = DateTime.UtcNow;
		if (utcNow - _lastRestartUtc < RestartCooldown)
		{
			return;
		}

		_lastRestartUtc = utcNow;
		foreach (Process item in SessionProcesses(Explorer))
		{
			Kill(item);
		}
		_log("Windows kabugu yeniden baslatiliyor: " + reason);
	}

	/// <summary>
	/// Kilit ekranı aktifken öne başka bir harici pencerenin geçip geçmediğini denetler ve loglar.
	/// </summary>
	public void ReportForeignForeground()
	{
		nint foregroundWindow = GetForegroundWindow();
		if (foregroundWindow == IntPtr.Zero)
		{
			return;
		}

		GetWindowThreadProcessId(foregroundWindow, out var processId);
		if (processId == 0 || processId == Environment.ProcessId)
		{
			return;
		}

		string processName;
		try
		{
			using Process process = Process.GetProcessById((int)processId);
			processName = process.ProcessName;
		}
		catch (ArgumentException)
		{
			return;
		}

		if (_reportedForeground.Add(processName))
		{
			_log("Kilitliyken onde baska pencere: " + processName);
		}
	}

	// Yalnızca geçerli oturum kimliğine (SessionId) ait süreçleri filtreler
	private List<Process> SessionProcesses(string name)
	{
		List<Process> list = new List<Process>();
		Process[] processesByName = Process.GetProcessesByName(name);
		foreach (Process process in processesByName)
		{
			if (process.SessionId == _session)
			{
				list.Add(process);
			}
			else
			{
				process.Dispose();
			}
		}
		return list;
	}

	// Sürecin tüm iş parçacıklarının askıda olup olmadığını kontrol eder
	private static bool IsSuspended(Process process)
	{
		try
		{
			process.Refresh();
			ProcessThreadCollection threads = process.Threads;
			if (threads.Count == 0)
			{
				return false;
			}

			foreach (ProcessThread item in threads)
			{
				if (item.ThreadState != ThreadState.Wait || item.WaitReason != ThreadWaitReason.Suspended)
				{
					return false;
				}
			}
			return true;
		}
		catch (Exception ex) when (ex is InvalidOperationException || ex is Win32Exception)
		{
			return false;
		}
	}

	// Native NtSuspendProcess çağrısı
	private bool Suspend(Process process)
	{
		return WithSuspendHandle(process, (nint handle) => NtSuspendProcess(handle) == 0);
	}

	// Native NtResumeProcess çağrısı (iç içe suspend sayaçlarını eritmek için 8 defaya kadar dener)
	private bool ResumeFully(Process process)
	{
		if (!IsSuspended(process))
		{
			return true;
		}

		bool flag = WithSuspendHandle(process, (nint handle) =>
		{
			for (int i = 0; i < MaxResumeAttempts; i++)
			{
				if (!IsSuspended(process))
				{
					break;
				}
				if (NtResumeProcess(handle) != 0)
				{
					return false;
				}
			}
			return !IsSuspended(process);
		});

		if (flag)
		{
			_log($"Windows kabugu geri acildi (pid {process.Id})");
		}
		return flag;
	}

	private bool WithSuspendHandle(Process process, Func<nint, bool> action)
	{
		nint handle = OpenProcess(ProcessSuspendResume, inheritHandle: false, process.Id);
		if (handle == IntPtr.Zero)
		{
			_log($"Windows kabuguna erisilemedi (Win32 hata {Marshal.GetLastWin32Error()})");
			return false;
		}

		try
		{
			return action(handle);
		}
		finally
		{
			CloseHandle(handle);
		}
	}

	private static bool Kill(Process process)
	{
		try
		{
			process.Kill();
			return true;
		}
		catch (Exception ex) when (ex is InvalidOperationException || ex is Win32Exception)
		{
			return false;
		}
		finally
		{
			process.Dispose();
		}
	}

	[DllImport("ntdll.dll")]
	private static extern int NtSuspendProcess(nint processHandle);

	[DllImport("ntdll.dll")]
	private static extern int NtResumeProcess(nint processHandle);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern nint OpenProcess(int desiredAccess, bool inheritHandle, int processId);

	[DllImport("kernel32.dll")]
	private static extern bool CloseHandle(nint handle);

	[DllImport("user32.dll")]
	private static extern nint GetForegroundWindow();

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
}
