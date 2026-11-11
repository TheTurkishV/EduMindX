using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Threading.Tasks;

namespace EduMindX.SmartBoard;

/// <summary>
/// Windows Görev Zamanlayıcısı (Task Scheduler) ile entegrasyonu sağlayan yardımcı sınıf.
/// Güncelleme görevini SYSTEM yetkisiyle kaydeder ve gerektiğinde arka planda tetikler.
/// </summary>
internal static class UpdateTask
{
	/// <summary>
	/// Windows Görev Zamanlayıcısı'ndaki görev adı.
	/// </summary>
	public const string Name = "EduMindX Akilli Tahta Guncelleme";

	private const string Sddl = "D:(A;;FA;;;SY)(A;;FA;;;BA)(A;;GRGX;;;AU)";
	private const int CreateOrUpdate = 6;
	private const int ServiceAccountLogon = 5;

	public static string InstalledExePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "EduMindX", "SmartBoard", "EduMindX.SmartBoard.exe");

	/// <summary>
	/// Windows Schedule.Service COM arayüzü ile görevi Windows Görev Zamanlayıcısı'na kaydeder.
	/// </summary>
	public static void Register(string exePath)
	{
		dynamic scheduler = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service", throwOnError: true)!);
		scheduler.Connect();
		dynamic folder = scheduler.GetFolder("\\");
		folder.RegisterTask(Name, BuildXml(exePath), CreateOrUpdate, null, null, ServiceAccountLogon, Sddl);
	}

	/// <summary>
	/// schtasks.exe komutu ile güncelleme görevini talep üzerine (on-demand) arka planda başlatır.
	/// </summary>
	public static void TryStart(Action<string> log)
	{
		Task.Run(() =>
		{
			try
			{
				using Process? process = Process.Start(new ProcessStartInfo("schtasks.exe", $"/Run /TN \"{Name}\"")
				{
					UseShellExecute = false,
					CreateNoWindow = true
				});

				if (process != null)
				{
					if (!process.WaitForExit(15000))
					{
						process.Kill();
						log("Guncelleme gorevi baslatilamadi: schtasks yanit vermedi");
					}
					else if (process.ExitCode != 0)
					{
						log($"Guncelleme gorevi baslatilamadi (cikis {process.ExitCode}; gorev tanimi eski olabilir)");
					}
					else
					{
						log("Guncelleme denetimi baslatildi (kilitli, ders disi)");
					}
				}
			}
			catch (Exception ex)
			{
				log("Guncelleme gorevi baslatilamadi: " + ex.Message);
			}
		});
	}

	/// <summary>
	/// Görev Zamanlayıcı için XML tanımını oluşturur (Açılışta 2 dk sonra ve her gece 03:00'te tetiklenir).
	/// </summary>
	private static string BuildXml(string exePath)
	{
		string startBoundary = DateTime.Today.AddDays(1.0).AddHours(3.0).ToString("s");
		return $"<?xml version=\"1.0\" encoding=\"UTF-16\"?>\n<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">\n  <RegistrationInfo>\n    <Description>EduMindX akilli tahta otomatik guncelleme: acilista, her gece 03:00'te ve tahta kilitliyken ders disinda imzali yeni surumu kurar.</Description>\n  </RegistrationInfo>\n  <Triggers>\n    <BootTrigger>\n      <Enabled>true</Enabled>\n      <Delay>PT2M</Delay>\n    </BootTrigger>\n    <CalendarTrigger>\n      <StartBoundary>{startBoundary}</StartBoundary>\n      <Enabled>true</Enabled>\n      <ScheduleByDay>\n        <DaysInterval>1</DaysInterval>\n      </ScheduleByDay>\n    </CalendarTrigger>\n  </Triggers>\n  <Principals>\n    <Principal id=\"Author\">\n      <UserId>S-1-5-18</UserId>\n      <RunLevel>HighestAvailable</RunLevel>\n    </Principal>\n  </Principals>\n  <Settings>\n    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>\n    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>\n    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>\n    <AllowHardTerminate>true</AllowHardTerminate>\n    <StartWhenAvailable>false</StartWhenAvailable>\n    <IdleSettings>\n      <StopOnIdleEnd>false</StopOnIdleEnd>\n      <RestartOnIdle>false</RestartOnIdle>\n    </IdleSettings>\n    <AllowStartOnDemand>true</AllowStartOnDemand>\n    <Enabled>true</Enabled>\n    <Hidden>false</Hidden>\n    <ExecutionTimeLimit>PT30M</ExecutionTimeLimit>\n    <Priority>7</Priority>\n  </Settings>\n  <Actions Context=\"Author\">\n    <Exec>\n      <Command>\"{SecurityElement.Escape(exePath)}\"</Command>\n      <Arguments>--update</Arguments>\n    </Exec>\n  </Actions>\n</Task>";
	}
}
