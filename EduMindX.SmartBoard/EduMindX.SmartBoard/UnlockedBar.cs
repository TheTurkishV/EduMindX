using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using EduMindX.SmartBoard.Core;

namespace EduMindX.SmartBoard;

/// <summary>
/// Tahta kilitli değilken (ders esnasında) ekranın kenarında duran yüzen durum çubuğu.
/// Win32 WS_EX_NOACTIVATE bayrağı sayesinde tıklandığında veya sürüklendiğinde
/// tahtada açık olan ders yazılımlarının (PowerPoint, PDF vb.) odağını (focus) çalmaz.
/// </summary>
public class UnlockedBar : Window, IComponentConnector
{
	private const int GwlExStyle = -20;
	private const int WsExNoActivate = 134217728; // 0x08000000: Odak almayan pencere
	private const int WsExToolWindow = 128;        // 0x00000080: Araç penceresi

	private const double HandleSize = 72.0;
	private const double DragThreshold = 10.0;
	private static readonly TimeSpan PanelOpenTime = TimeSpan.FromSeconds(6L);

	// Normal durum renkleri (koyu gri/lacivert)
	private static readonly Brush NormalBackground = new SolidColorBrush(Color.FromArgb(230, 17, 24, 39));
	private static readonly Brush NormalBorder = new SolidColorBrush(Color.FromRgb(51, 65, 85));

	// Süre bitimi yaklaşınca sarı/turuncu uyarı renkleri
	private static readonly Brush WarningBackground = new SolidColorBrush(Color.FromArgb(242, 120, 53, 15));
	private static readonly Brush WarningBorder = new SolidColorBrush(Color.FromRgb(245, 158, 11));

	private readonly Action _lockRequested;
	private readonly Action<FloatingPosition> _positionChanged;

	// Detay panelinin açık kalma süresi (6 saniye dokunulmazsa otomatik kapanır)
	private readonly DispatcherTimer _closePanel = new DispatcherTimer
	{
		Interval = PanelOpenTime
	};

	private FloatingPosition _position;
	private Point? _pressScreen;
	private Point _grab;
	private bool _dragging;

	internal DockPanel Root;
	internal Border Handle;
	internal TextBlock HandleText;
	internal Border Panel;
	internal TextBlock TitleText;
	internal TextBlock DetailText;
	internal Button LockButton;
	private bool _contentLoaded;

	private bool IsPanelOpen => Panel.Visibility == Visibility.Visible;

	public UnlockedBar(Action lockRequested, FloatingPosition position, Action<FloatingPosition> positionChanged)
	{
		_lockRequested = lockRequested;
		_positionChanged = positionChanged;
		_position = position.Normalized();
		InitializeComponent();

		_closePanel.Tick += (object? _, EventArgs _) =>
		{
			ClosePanel();
		};
		SourceInitialized += (object? _, EventArgs _) =>
		{
			MakeNonActivating();
		};
		SizeChanged += (object _, SizeChangedEventArgs _) =>
		{
			Reposition();
		};
		IsVisibleChanged += (object _, DependencyPropertyChangedEventArgs _) =>
		{
			if (!IsVisible)
			{
				ClosePanel();
			}
		};
		ApplySide();
	}

	/// <summary>
	/// Kalan süreye göre çubuğun metinlerini ve uyarı renklerini günceller.
	/// </summary>
	public void Render(DateTime nowLocal, DateTime untilLocal, bool warning)
	{
		TimeSpan timeSpan = untilLocal - nowLocal;
		if (timeSpan < TimeSpan.Zero)
		{
			timeSpan = TimeSpan.Zero;
		}

		HandleText.Text = (warning ? $"{timeSpan:m\\:ss}" : $"{untilLocal:HH:mm}");
		TitleText.Text = (warning ? $"Kilitlenmesine {timeSpan:m\\:ss} kaldı" : "Tahta açık");
		DetailText.Text = $"Kilitlenme: {untilLocal:HH:mm}";

		Brush backgroundBrush = (warning ? WarningBackground : NormalBackground);
		Brush borderBrush = (warning ? WarningBorder : NormalBorder);

		Handle.Background = Panel.Background = backgroundBrush;
		Handle.BorderBrush = Panel.BorderBrush = borderBrush;
	}

	public void EnsureOnTop()
	{
		Topmost = false;
		Topmost = true;
	}

	/// <summary>
	/// Çubuğu çalışma alanı (WorkArea) sınırları içinde doğru pozisyona yerleştirir.
	/// </summary>
	public void Reposition()
	{
		if (!_dragging)
		{
			ScreenArea area = WorkArea();
			(double Left, double Top) tuple = _position.Place(area, HandleSize, HandleSize);
			Point point = HandleOffset();
			Left = Math.Clamp(tuple.Left - point.X, area.Left, Math.Max(area.Left, area.Left + area.Width - ActualWidth));
			Top = Math.Clamp(tuple.Top - point.Y, area.Top, Math.Max(area.Top, area.Top + area.Height - ActualHeight));
		}
	}

	private Point HandleOffset()
	{
		UpdateLayout();
		return Handle.TranslatePoint(new Point(0.0, 0.0), this);
	}

	private void LockButton_Click(object sender, RoutedEventArgs e)
	{
		_lockRequested();
	}

	private void Handle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		_grab = e.GetPosition(Handle);
		_pressScreen = ToScreen(e.GetPosition(this));
		_dragging = false;
		Handle.CaptureMouse();
		e.Handled = true;
	}

	private void Handle_MouseMove(object sender, MouseEventArgs e)
	{
		Point? pressScreen = _pressScreen;
		if (!pressScreen.HasValue)
		{
			return;
		}

		Point valueOrDefault = pressScreen.GetValueOrDefault();
		Point point = ToScreen(e.GetPosition(this));
		if (!_dragging)
		{
			if ((point - valueOrDefault).Length < DragThreshold)
			{
				return;
			}
			_dragging = true;
			ClosePanel();
		}

		Point point2 = HandleOffset();
		Left = point.X - _grab.X - point2.X;
		Top = point.Y - _grab.Y - point2.Y;
	}

	private void Handle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (_pressScreen.HasValue)
		{
			bool dragging = _dragging;
			EndPress();
			if (dragging)
			{
				SavePosition();
			}
			else if (IsPanelOpen)
			{
				ClosePanel();
			}
			else
			{
				OpenPanel();
			}
			e.Handled = true;
		}
	}

	private void Handle_LostMouseCapture(object sender, MouseEventArgs e)
	{
		if (_pressScreen.HasValue)
		{
			bool dragging = _dragging;
			EndPress();
			if (dragging)
			{
				SavePosition();
			}
		}
	}

	private void EndPress()
	{
		_pressScreen = null;
		_dragging = false;
		if (Handle.IsMouseCaptured)
		{
			Handle.ReleaseMouseCapture();
		}
	}

	private void SavePosition()
	{
		Point point = HandleOffset();
		_position = FloatingPosition.FromPlacement(WorkArea(), HandleSize, HandleSize, Left + point.X, Top + point.Y);
		ApplySide();
		Reposition();
		_positionChanged(_position);
	}

	private void OpenPanel()
	{
		Panel.Visibility = Visibility.Visible;
		_closePanel.Stop();
		_closePanel.Start();
	}

	private void ClosePanel()
	{
		_closePanel.Stop();
		Panel.Visibility = Visibility.Collapsed;
	}

	private void ApplySide()
	{
		Dock dock = (_position.IsOnRightHalf ? Dock.Right : Dock.Left);
		DockPanel.SetDock(Handle, dock);
		DockPanel.SetDock(Panel, dock);
		Panel.Margin = (_position.IsOnRightHalf ? new Thickness(0.0, 0.0, 8.0, 0.0) : new Thickness(8.0, 0.0, 0.0, 0.0));
	}

	private static ScreenArea WorkArea()
	{
		Rect workArea = SystemParameters.WorkArea;
		return new ScreenArea(workArea.Left, workArea.Top, workArea.Width, workArea.Height);
	}

	private Point ToScreen(Point relative)
	{
		Point point = PointToScreen(relative);
		return PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice.Transform(point) ?? point;
	}

	/// <summary>
	/// Win32 API ile pencereye WS_EX_NOACTIVATE bayrağı ekleyerek odak çalmasını engeller.
	/// </summary>
	private void MakeNonActivating()
	{
		nint handle = new WindowInteropHelper(this).Handle;
		SetWindowLong(handle, GwlExStyle, GetWindowLong(handle, GwlExStyle) | WsExNoActivate | WsExToolWindow);
	}

	[DllImport("user32.dll")]
	private static extern int GetWindowLong(nint hWnd, int nIndex);

	[DllImport("user32.dll")]
	private static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.12.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/EduMindX.SmartBoard;component/unlockedbar.xaml", UriKind.Relative);
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
				Root = (DockPanel)target;
				break;
			case 2:
				Handle = (Border)target;
				Handle.MouseLeftButtonDown += Handle_MouseLeftButtonDown;
				Handle.MouseMove += Handle_MouseMove;
				Handle.MouseLeftButtonUp += Handle_MouseLeftButtonUp;
				Handle.LostMouseCapture += Handle_LostMouseCapture;
				break;
			case 3:
				HandleText = (TextBlock)target;
				break;
			case 4:
				Panel = (Border)target;
				break;
			case 5:
				TitleText = (TextBlock)target;
				break;
			case 6:
				DetailText = (TextBlock)target;
				break;
			case 7:
				LockButton = (Button)target;
				LockButton.Click += LockButton_Click;
				break;
			default:
				_contentLoaded = true;
				break;
		}
	}
}
