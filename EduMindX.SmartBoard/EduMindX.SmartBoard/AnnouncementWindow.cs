using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Threading;

namespace EduMindX.SmartBoard;

/// <summary>
/// Okul idaresi veya merkezi yönetim tarafından gönderilen acil durum ve genel duyuruların
/// tahtada tam ekran modal kart olarak gösterilmesini sağlayan duyuru penceresi.
/// Kilit ekranının da üzerinde (Topmost) görüntülenir.
/// </summary>
public class AnnouncementWindow : Window, IComponentConnector
{
	/// <summary>
	/// Hiçbir etkileşim olmazsa duyurunun otomatik kapanacağı süre (10 Dakika).
	/// </summary>
	public static readonly TimeSpan AutoCloseAfter = TimeSpan.FromMinutes(10L);

	/// <summary>
	/// Ekranda gösterilecek maksimum mesaj uzunluğu (1000 Karakter).
	/// </summary>
	public const int MaxLength = 1000;

	private readonly DispatcherTimer _autoClose = new DispatcherTimer
	{
		Interval = AutoCloseAfter
	};

	internal Border Card;
	internal TextBlock MessageText;
	internal TextBlock SentAtText;
	private bool _contentLoaded;

	/// <summary>
	/// Pencerenin işletim sistemi tarafından kapatılmasına izin verilip verilmediği.
	/// </summary>
	public bool AllowClose { get; set; }

	public AnnouncementWindow()
	{
		InitializeComponent();
		_autoClose.Tick += (object? _, EventArgs _) =>
		{
			HideAnnouncement();
		};
	}

	/// <summary>
	/// Belirtilen metni duyuru penceresinde gösterir ve zamanlayıcıyı başlatır.
	/// </summary>
	/// <param name="message">Duyuru metni.</param>
	/// <param name="sentAtLocal">Gönderildiği yerel saat.</param>
	public void ShowMessage(string message, DateTime sentAtLocal)
	{
		MessageText.Text = ((message.Length > MaxLength) ? (message.Substring(0, MaxLength) + "…") : message);
		SentAtText.Text = $"Gönderilme: {sentAtLocal:HH:mm}";
		CoverPrimaryScreen();
		
		if (!IsVisible)
		{
			Show();
		}
		EnsureOnTop();
		
		_autoClose.Stop();
		_autoClose.Start();
	}

	/// <summary>
	/// Duyuru penceresini ana ekranın tamamını kaplayacak şekilde boyutlandırır.
	/// </summary>
	public void CoverPrimaryScreen()
	{
		Left = 0.0;
		Top = 0.0;
		Width = SystemParameters.PrimaryScreenWidth;
		Height = SystemParameters.PrimaryScreenHeight;
		Card.MaxHeight = Math.Max(200.0, Height - Card.Margin.Top - Card.Margin.Bottom);
	}

	/// <summary>
	/// Pencerenin en üstte (Topmost) ve aktif kalmasını garanti eder.
	/// </summary>
	public void EnsureOnTop()
	{
		if (IsVisible)
		{
			Topmost = false;
			Topmost = true;
			if (!IsActive)
			{
				Activate();
			}
		}
	}

	protected override void OnClosing(CancelEventArgs e)
	{
		if (!AllowClose)
		{
			e.Cancel = true;
			HideAnnouncement();
		}
		base.OnClosing(e);
	}

	private void HideAnnouncement()
	{
		_autoClose.Stop();
		Hide();
	}

	private void Ok_Click(object sender, RoutedEventArgs e)
	{
		HideAnnouncement();
	}

	private void Window_KeyDown(object sender, KeyEventArgs e)
	{
		Key key = e.Key;
		if (key == Key.Return || key == Key.Escape)
		{
			HideAnnouncement();
			e.Handled = true;
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.12.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/EduMindX.SmartBoard;component/announcementwindow.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.12.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
			case 1:
				((AnnouncementWindow)target).KeyDown += Window_KeyDown;
				break;
			case 2:
				Card = (Border)target;
				break;
			case 3:
				MessageText = (TextBlock)target;
				break;
			case 4:
				SentAtText = (TextBlock)target;
				break;
			case 5:
				((Button)target).Click += Ok_Click;
				break;
			default:
				_contentLoaded = true;
				break;
		}
	}
}
