using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using EduMindX.SmartBoard.Core;
using QRCoder;

namespace EduMindX.SmartBoard;

/// <summary>
/// Akıllı tahtanın ana kilit ekranı.
/// Tüm bağlı monitörleri tam ekran kaplar, kenarlıksız ve en üstte (Topmost) durur.
/// Omuz gözetleme ve parmak izi analizine karşı sayısal tuş takımını her açılışta rastgele karıştırır (Shuffled Keypad).
/// QR kod ile mobil kilit açma arayüzü sunar.
/// </summary>
public class LockWindow : Window, IComponentConnector
{
	private const int PinLength = 8;
	private const string NoCodeMessage = "Bugün için kod yok. Merkezden acil şifre isteyin.";
	private static readonly TimeSpan KeypadIdleTimeout = TimeSpan.FromSeconds(30L);
	private static readonly TimeSpan HintDuration = TimeSpan.FromSeconds(8L);
	private static readonly CultureInfo Turkish = new CultureInfo("tr-TR");

	private readonly Func<string, PasswordResult> _submitPin;
	private readonly Func<Task<QrDisplay?>> _requestQr;
	private readonly Ellipse[] _dots = new Ellipse[PinLength];

	private string _pin = string.Empty;
	private DateTime _keypadActivity;
	private DateTime? _qrDeadline;
	private DateTime? _hintUntil;
	private LockScreenState? _state;
	private bool _lockoutShown;

	internal Grid Root;
	internal TextBlock LockGlyph;
	internal TextBlock ClockText;
	internal TextBlock DateText;
	internal TextBlock TitleText;
	internal TextBlock InfoText;
	internal TextBlock IdentityText;
	internal TextBlock ConnectionText;
	internal StackPanel OpenChoices;
	internal Button QrButton;
	internal TextBlock QrHint;
	internal Button AdminButton;
	internal Border QrPanel;
	internal Image QrImage;
	internal TextBlock QrCountdown;
	internal Border KeypadPanel;
	internal StackPanel Dots;
	internal TextBlock KeypadMessage;
	internal UniformGrid KeyGrid;
	private bool _contentLoaded;

	/// <summary>
	/// Pencerenin kapatılmasına izin verilip verilmediği.
	/// </summary>
	public bool AllowClose { get; set; }

	public LockWindow(Func<string, PasswordResult> submitPin, Func<Task<QrDisplay?>> requestQr)
	{
		_submitPin = submitPin;
		_requestQr = requestQr;
		InitializeComponent();

		// PIN giriş noktalarını oluştur (8 adet daire)
		for (int i = 0; i < PinLength; i++)
		{
			_dots[i] = new Ellipse
			{
				Width = 16.0,
				Height = 16.0,
				Margin = new Thickness(5.0),
				StrokeThickness = 2.0,
				Stroke = Brushes.SlateGray
			};
			Dots.Children.Add(_dots[i]);
		}

		Closing += (object? _, CancelEventArgs e) =>
		{
			// AllowClose true olmadığı sürece pencerenin kapatılmasını engelle
			e.Cancel = !AllowClose;
		};

		PreviewKeyDown += OnPreviewKeyDown;
		CoverAllScreens();
	}

	/// <summary>
	/// Sisteme bağlı tüm monitörlerin toplam alanını (VirtualScreen) kaplayacak şekilde boyutlandırır.
	/// </summary>
	public void CoverAllScreens()
	{
		Left = SystemParameters.VirtualScreenLeft;
		Top = SystemParameters.VirtualScreenTop;
		Width = SystemParameters.VirtualScreenWidth;
		Height = SystemParameters.VirtualScreenHeight;
	}

	/// <summary>
	/// Kilit penceresinin daima en üstte ve aktif olmasını sağlar.
	/// </summary>
	public void EnsureOnTop(bool activate = true)
	{
		Topmost = false;
		Topmost = true;
		if (activate && !IsActive)
		{
			Activate();
		}
	}

	public void ResetPanels()
	{
		CloseQr();
		CloseKeypad();
	}

	/// <summary>
	/// Kilit ekranı metinlerini ve durumunu günceller.
	/// </summary>
	public void Render(LockScreenState state)
	{
		_state = state;
		ClockText.Text = state.NowLocal.ToString("HH:mm", Turkish);
		DateText.Text = state.NowLocal.ToString("d MMMM yyyy dddd", Turkish);
		IdentityText.Text = state.Identity;
		ConnectionText.Text = state.ConnectionStatus;

		// Hizmet dışı durumu kontrolü
		if (!state.ServiceEnabled)
		{
			TitleText.Text = "Tahta hizmet dışı";
			InfoText.Text = "Kampüs yönetimiyle görüşün";
			QrPanel.Visibility = Visibility.Collapsed;
			QrButton.Visibility = Visibility.Collapsed;
			QrHint.Visibility = Visibility.Collapsed;
			if (KeypadPanel.Visibility == Visibility.Visible)
			{
				if (DateTime.UtcNow - _keypadActivity > KeypadIdleTimeout)
				{
					CloseKeypad();
				}
				else
				{
					RenderLockout(state.LockoutRemaining);
				}
			}
			else
			{
				OpenChoices.Visibility = Visibility.Visible;
			}
			return;
		}

		QrButton.Visibility = Visibility.Visible;
		TitleText.Text = "Tahta kilitli";

		// Ders durumuna göre açıklama metni
		LessonSlot? currentLesson = state.CurrentLesson;
		if (currentLesson == null)
		{
			LessonSlot? nextLesson = state.NextLesson;
			InfoText.Text = (nextLesson != null) 
				? $"Sıradaki ders {nextLesson.Start} · Telefonla QR okutarak açın" 
				: "Bugün başka ders yok · Telefonla QR okutarak açın";
		}
		else
		{
			InfoText.Text = $"Ders saati {currentLesson.Start} – {currentLesson.End} · Telefonla QR okutarak açın";
		}

		RenderQr();
		RenderChoices(state);

		if (KeypadPanel.Visibility == Visibility.Visible)
		{
			if (DateTime.UtcNow - _keypadActivity > KeypadIdleTimeout)
			{
				CloseKeypad();
			}
			else
			{
				RenderLockout(state.LockoutRemaining);
			}
		}
	}

	private void RenderChoices(LockScreenState state)
	{
		bool panelOpen = QrPanel.Visibility == Visibility.Visible || KeypadPanel.Visibility == Visibility.Visible;
		OpenChoices.Visibility = (panelOpen ? Visibility.Collapsed : Visibility.Visible);
		QrButton.IsEnabled = state.Online;

		if (!state.Online)
		{
			ShowHint("Bağlantı yok — idare tek kullanımlık kodla açar", persistent: true);
			return;
		}

		DateTime? hintUntil = _hintUntil;
		if (hintUntil.HasValue && DateTime.UtcNow <= hintUntil.GetValueOrDefault())
		{
			return;
		}
		QrHint.Visibility = Visibility.Collapsed;
	}

	private void ShowHint(string text, bool persistent = false)
	{
		QrHint.Text = text;
		QrHint.Visibility = Visibility.Visible;
		_hintUntil = (persistent ? null : DateTime.UtcNow + HintDuration);
	}

	private async void QrButton_Click(object sender, RoutedEventArgs e)
	{
		QrButton.IsEnabled = false;
		try
		{
			QrDisplay? qrDisplay = await _requestQr();
			if (qrDisplay == null)
			{
				ShowHint("QR şu an alınamadı — tekrar deneyin ya da idareden kod isteyin");
			}
			else
			{
				ShowQr(qrDisplay);
			}
		}
		finally
		{
			QrButton.IsEnabled = _state?.Online ?? true;
		}
	}

	/// <summary>
	/// QRCoder kütüphanesiyle dinamik QR kod görseli oluşturur ve ekrana basar.
	/// </summary>
	private void ShowQr(QrDisplay qr)
	{
		using QRCodeGenerator qRCodeGenerator = new QRCodeGenerator();
		using QRCodeData data = qRCodeGenerator.CreateQrCode(qr.Url, QRCodeGenerator.ECCLevel.M);
		byte[] graphic = new PngByteQRCode(data).GetGraphic(10);

		BitmapImage bitmapImage = new BitmapImage();
		bitmapImage.BeginInit();
		bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
		bitmapImage.StreamSource = new MemoryStream(graphic);
		bitmapImage.EndInit();
		bitmapImage.Freeze();

		QrImage.Source = bitmapImage;
		AutomationProperties.SetHelpText(QrImage, qr.Url);
		_qrDeadline = DateTime.UtcNow + qr.Remaining;

		CloseKeypad();
		QrPanel.Visibility = Visibility.Visible;
		OpenChoices.Visibility = Visibility.Collapsed;
		RenderQr();
	}

	private void RenderQr()
	{
		if (QrPanel.Visibility != Visibility.Visible)
		{
			return;
		}

		DateTime? qrDeadline = _qrDeadline;
		if (qrDeadline.HasValue)
		{
			TimeSpan timeSpan = qrDeadline.GetValueOrDefault() - DateTime.UtcNow;
			if (timeSpan <= TimeSpan.Zero)
			{
				CloseQr();
				return;
			}
			QrCountdown.Text = $"Kodun süresi: {timeSpan:m\\:ss}";
		}
	}

	private void CloseQr()
	{
		QrPanel.Visibility = Visibility.Collapsed;
		QrImage.Source = null;
		AutomationProperties.SetHelpText(QrImage, string.Empty);
		_qrDeadline = null;
		OpenChoices.Visibility = Visibility.Visible;
	}

	private void QrClose_Click(object sender, RoutedEventArgs e)
	{
		CloseQr();
	}

	private void AdminButton_Click(object sender, RoutedEventArgs e)
	{
		OpenKeypad();
	}

	/// <summary>
	/// Sayısal PIN tuş takımını açar.
	/// Omuz gözetleme koruması için tuşları rastgele karıştırır.
	/// </summary>
	private void OpenKeypad()
	{
		CloseQr();
		_pin = string.Empty;
		_keypadActivity = DateTime.UtcNow;
		OpenChoices.Visibility = Visibility.Collapsed;
		KeypadPanel.Visibility = Visibility.Visible;
		
		KeypadMessage.Text = (_state != null && !_state.HasPassword) ? NoCodeMessage : string.Empty;
		BuildKeys();
		RenderDots();
		RenderLockout(_state?.LockoutRemaining);
	}

	private void CloseKeypad()
	{
		_pin = string.Empty;
		KeypadPanel.Visibility = Visibility.Collapsed;
		OpenChoices.Visibility = Visibility.Visible;
	}

	/// <summary>
	/// 0-9 rakamlarını rastgele sıralayarak tuş takımını dinamik olarak inşa eder (Anti-Shoulder Surfing).
	/// </summary>
	private void BuildKeys()
	{
		// 0'dan 9'a kadar olan rakamları rastgele karıştır
		int[] shuffled = Enumerable.Range(0, 10).OrderBy(_ => Random.Shared.Next()).ToArray();
		KeyGrid.Children.Clear();

		for (int i = 0; i < 9; i++)
		{
			KeyGrid.Children.Add(DigitKey(shuffled[i]));
		}

		KeyGrid.Children.Add(ActionKey("Kapat", CloseKeypad));
		KeyGrid.Children.Add(DigitKey(shuffled[9]));
		KeyGrid.Children.Add(ActionKey("Sil", RemoveDigit));
	}

	private Button DigitKey(int digit)
	{
		Button button = new Button();
		button.Content = digit.ToString(CultureInfo.InvariantCulture);
		button.Style = (Style)Resources["KeypadKey"];
		button.Click += (object _, RoutedEventArgs _) =>
		{
			AddDigit(digit);
		};
		return button;
	}

	private Button ActionKey(string text, Action action)
	{
		Button button = new Button();
		button.Content = text;
		button.Style = (Style)Resources["KeypadKey"];
		button.FontSize = 20.0;
		button.Click += (object _, RoutedEventArgs _) =>
		{
			action();
		};
		return button;
	}

	private void AddDigit(int digit)
	{
		_keypadActivity = DateTime.UtcNow;
		if ((_state == null || !_state.LockoutRemaining.HasValue) && _pin.Length < PinLength)
		{
			_pin += digit.ToString(CultureInfo.InvariantCulture);
			RenderDots();
			if (_pin.Length == PinLength)
			{
				Submit();
			}
		}
	}

	private void RemoveDigit()
	{
		_keypadActivity = DateTime.UtcNow;
		if (_pin.Length > 0)
		{
			_pin = _pin.Substring(0, _pin.Length - 1);
		}
		RenderDots();
	}

	/// <summary>
	/// Girilen 8 haneli PIN'i doğrulamaya gönderir.
	/// </summary>
	private void Submit()
	{
		PasswordResult passwordResult = _submitPin(_pin);
		_pin = string.Empty;

		switch (passwordResult)
		{
			case PasswordResult.Success:
				CloseKeypad();
				return;
			case PasswordResult.Wrong:
				KeypadMessage.Text = "Kod ya da şifre yanlış";
				break;
			case PasswordResult.NoPassword:
				KeypadMessage.Text = NoCodeMessage;
				break;
			case PasswordResult.LockedOut:
				KeypadMessage.Text = "Çok fazla yanlış deneme.";
				break;
		}

		// Her denemeden sonra tuşların yerini tekrar karıştır
		BuildKeys();
		RenderDots();
	}

	private void RenderDots()
	{
		for (int i = 0; i < PinLength; i++)
		{
			_dots[i].Fill = ((i < _pin.Length) ? Brushes.White : Brushes.Transparent);
		}
	}

	private void RenderLockout(TimeSpan? remaining)
	{
		KeyGrid.IsEnabled = !remaining.HasValue;
		KeyGrid.Opacity = (!remaining.HasValue) ? 1.0 : 0.35;

		if (remaining.HasValue)
		{
			TimeSpan valueOrDefault = remaining.GetValueOrDefault();
			KeypadMessage.Text = $"Çok fazla yanlış deneme. {valueOrDefault:m\\:ss} sonra tekrar deneyin.";
			_lockoutShown = true;
		}
		else if (_lockoutShown)
		{
			KeypadMessage.Text = string.Empty;
			_lockoutShown = false;
		}
	}

	private void OnPreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (KeypadPanel.Visibility != Visibility.Visible)
		{
			return;
		}

		Key key = e.Key;
		if (key >= Key.D0 && key <= Key.D9)
		{
			AddDigit((int)(e.Key - Key.D0));
		}
		else if (key >= Key.NumPad0 && key <= Key.NumPad9)
		{
			AddDigit((int)(e.Key - Key.NumPad0));
		}
		else if (e.Key == Key.Back)
		{
			RemoveDigit();
		}
		else if (e.Key == Key.Escape)
		{
			CloseKeypad();
		}
		else
		{
			return;
		}
		e.Handled = true;
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.12.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/EduMindX.SmartBoard;component/lockwindow.xaml", UriKind.Relative);
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
				Root = (Grid)target;
				break;
			case 2:
				LockGlyph = (TextBlock)target;
				break;
			case 3:
				ClockText = (TextBlock)target;
				break;
			case 4:
				DateText = (TextBlock)target;
				break;
			case 5:
				TitleText = (TextBlock)target;
				break;
			case 6:
				InfoText = (TextBlock)target;
				break;
			case 7:
				IdentityText = (TextBlock)target;
				break;
			case 8:
				ConnectionText = (TextBlock)target;
				break;
			case 9:
				OpenChoices = (StackPanel)target;
				break;
			case 10:
				QrButton = (Button)target;
				QrButton.Click += QrButton_Click;
				break;
			case 11:
				QrHint = (TextBlock)target;
				break;
			case 12:
				AdminButton = (Button)target;
				AdminButton.Click += AdminButton_Click;
				break;
			case 13:
				QrPanel = (Border)target;
				break;
			case 14:
				QrImage = (Image)target;
				break;
			case 15:
				QrCountdown = (TextBlock)target;
				break;
			case 16:
				((Button)target).Click += QrClose_Click;
				break;
			case 17:
				KeypadPanel = (Border)target;
				break;
			case 18:
				Dots = (StackPanel)target;
				break;
			case 19:
				KeypadMessage = (TextBlock)target;
				break;
			case 20:
				KeyGrid = (UniformGrid)target;
				break;
			default:
				_contentLoaded = true;
				break;
		}
	}
}
