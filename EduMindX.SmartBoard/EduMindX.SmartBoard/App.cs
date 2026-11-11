using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace EduMindX.SmartBoard;

/// <summary>
/// WPF Uygulama Başlangıç ve Yaşam Döngüsü Yöneticisi (App.xaml.cs).
/// CLI parametrelerini işler (--check, --update, --self-test, --register-update-task),
/// tekil örnek (Single-Instance Mutex) denetimi yapar ve yakalanmayan hataları günlüğe kaydeder.
/// </summary>
public class App : Application
{
	private Mutex? _singleInstance;
	private BoardController? _controller;
	private bool _contentLoaded;

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		// CLI Komutu: Bağlantı ve anahtar teşhisi (--check <apiBaseUrl> <resultPath>)
		if (e.Args.Length == 3 && e.Args[0] == "--check")
		{
			Shutdown(Task.Run(() => ConnectionCheck.RunAsync(e.Args[1], e.Args[2])).GetAwaiter().GetResult());
			return;
		}

		// CLI Komutu: Arka planda güncelleme motorunu çalıştır (--update)
		if (e.Args.Length == 1 && e.Args[0] == "--update")
		{
			Shutdown(Task.Run((Func<Task<int>>)UpdateRunner.RunAsync).GetAwaiter().GetResult());
			return;
		}

		// CLI Komutu: Staged güncelleme binary'si testi (--self-test <resultPath>)
		if (e.Args.Length == 2 && e.Args[0] == "--self-test")
		{
			Shutdown(UpdateRunner.WriteSelfTest(e.Args[1]));
			return;
		}

		// CLI Komutu: Windows Görev Zamanlayıcısı'na güncelleme görevini kaydet
		if (e.Args.Length == 1 && e.Args[0] == "--register-update-task")
		{
			Shutdown(RegisterUpdateTask());
			return;
		}

		// Tek örnek (Single-Instance) kontrolü: Aynı anda yalnızca bir tahta arayüzü çalışabilir
		_singleInstance = new Mutex(initiallyOwned: true, "Local\\EduMindX.SmartBoard", out var createdNew);
		if (!createdNew)
		{
			Shutdown();
			return;
		}

		BoardStorage storage = new BoardStorage();
		DispatcherUnhandledException += (object _, DispatcherUnhandledExceptionEventArgs args) =>
		{
			storage.Log($"Beklenmeyen hata: {args.Exception}");
			args.Handled = true;
		};

		// Ana kontrolcüyü başlat
		_controller = new BoardController();
		_controller.Start();
	}

	private static int RegisterUpdateTask()
	{
		try
		{
			UpdateTask.Register(Environment.ProcessPath!);
			return 0;
		}
		catch (Exception ex)
		{
			new BoardStorage().Log("Guncelleme gorevi kaydedilemedi: " + ex.Message);
			return 1;
		}
	}

	protected override void OnExit(ExitEventArgs e)
	{
		_controller?.Dispose();
		_singleInstance?.Dispose();
		base.OnExit(e);
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.12.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/EduMindX.SmartBoard;component/app.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[STAThread]
	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.12.0")]
	public static void Main()
	{
		App app = new App();
		app.InitializeComponent();
		app.Run();
	}
}
