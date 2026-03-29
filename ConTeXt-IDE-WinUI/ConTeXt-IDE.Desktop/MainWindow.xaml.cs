using ConTeXt_IDE.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;

namespace ConTeXt_IDE;

public sealed partial class MainWindow : Window
{

 private ViewModel VM { get; } = App.VM;
 public AppWindow AW { get; set; }
 private DispatcherTimer _startupWatchdog;

 public IntPtr hWnd;
 public bool IsCustomizationSupported { get; set; } = false;
 public MainWindow()
 {
  InitializeComponent();
  StartStartupWatchdog();
  IsCustomizationSupported = AppWindowTitleBar.IsCustomizationSupported();
  AW = GetAppWindowForCurrentWindow();
  AW.Title = "ConTeXt IDE";
  AW.Closing += AW_Closing;

  string IconPath = Path.Combine(Package.Current.Installed­Location.Path, @"Assets", @"SquareLogo.ico");

  //var hwnd = WindowNative.GetWindowHandle(this);
  //WindowIconHelper.SetWindowIcon(hwnd, IconPath);

  //AW.SetIcon(IconPath);
  //AW.SetPresenter(VM.Default.LastPresenter);

  if (AW.Presenter is OverlappedPresenter OP)
  {
	int x = Math.Max(VM.Default.WindowSettingMain.LastSize.X, 0);
	int y = Math.Max(VM.Default.WindowSettingMain.LastSize.Y, 0);
	AW.Move(new(x, y));



	// OP.SetBorderAndTitleBar(false, true);
  }
  else
  {

  }

  if (IsCustomizationSupported)
  {
	AW.TitleBar.ExtendsContentIntoTitleBar = true;
	CustomDragRegion.Height = 0;
  }
  else
  {
	CustomDragRegion.BackgroundTransition = null;
	CustomDragRegion.Background = null;
	ExtendsContentIntoTitleBar = true;
	CustomDragRegion.Height = 28;
	SetTitleBar(CustomDragRegion);
	Title = "ConTeXt IDE";
  }
 }

 private void StartStartupWatchdog()
 {
  _startupWatchdog = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
  _startupWatchdog.Tick += (s, e) =>
  {
	try
	{
	 _startupWatchdog.Stop();
   if (RootFrame == null)
	 {
	  string msg = "ConTeXt IDE konnte die Startansicht nicht initialisieren (RootFrame ist null).\n\nBitte die App neu installieren oder Error-Log.txt prüfen.";
	  App.write("Startup watchdog: RootFrame is null after timeout.");
	  VM?.Log("Startup watchdog: RootFrame is null after timeout.");
	  Content = new ScrollViewer
	  {
		Content = new TextBlock
		{
		 Text = msg,
		 TextWrapping = TextWrapping.Wrap,
		 Margin = new Thickness(24)
		}
	  };
	 }
	 else if (RootFrame.Content == null)
	 {
	  string msg = "ConTeXt IDE konnte die Startseite nicht laden.\n\nBitte App neu installieren oder die Logs prüfen (Error-Log.txt).";
	  App.write("Startup watchdog: RootFrame.Content is null after timeout.");
	  VM?.Log("Startup watchdog triggered: RootFrame.Content is null after timeout.");
	  RootFrame.Content = new TextBlock
	  {
		Text = msg,
		TextWrapping = TextWrapping.Wrap,
		Margin = new Thickness(24)
	  };
	 }
	}
	catch (Exception ex)
	{
	 App.write("Startup watchdog exception: " + ex);
	}
  };
  _startupWatchdog.Start();
 }

 public void ResetTitleBar()
 {
  AW.TitleBar.ExtendsContentIntoTitleBar = true;
  AW.TitleBar.IconShowOptions = IconShowOptions.HideIconAndSystemMenu;
 }

 private AppWindow GetAppWindowForCurrentWindow()
 {
  hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
  WindowId myWndId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
  return AppWindow.GetFromWindowId(myWndId);
 }

 private async void AW_Closing(AppWindow sender, AppWindowClosingEventArgs args)
 {
  args.Cancel = true;
  bool canceled = false;
  int x = Math.Max(sender.Position.X, 0);
  int y = Math.Max(sender.Position.Y, 0);
  int w = Math.Max(sender.Size.Width, 1024);
  int h = Math.Max(sender.Size.Height, 576);
  bool ismax = false;
  if (sender.Presenter is OverlappedPresenter OP)
  {
	ismax = OP.State == OverlappedPresenterState.Maximized;
  }

  VM.Default.WindowSettingMain = new() { IsMaximized = ismax, LastPresenter = sender.Presenter.Kind, LastSize = new(x, y, w, h) };
  if (RootFrame.Content is MainPage MP)
  {
	canceled = await MP?.MainPage_CloseRequested();
  }
  if (!canceled)
  {
	//Application.Current.Exit();
	try
	{
	 Environment.Exit(0);
	 sender.Destroy();
	}
	catch { }
  }
 }



 public static bool CheckForInternetConnection(int timeoutMs = 5000, string url = "https://www.google.com/")
 {
  try
  {

	var request = (HttpWebRequest)WebRequest.Create(url);
	request.KeepAlive = false;
	request.Timeout = timeoutMs;
	using var response = (HttpWebResponse)request.GetResponse();
	return true;
  }
  catch
  {
	return false;
  }
 }


 private async void InstallEvergreen()
 {
  EnsureStartupStatusContent("WebView2 Runtime wird vorbereitet...");

  VM.InfoMessage("Installing...", "Downloading the Evergreen WebView2 Runtime.", InfoBarSeverity.Informational);

  if (!File.Exists(Path.Combine(ApplicationData.Current.LocalFolder.Path, "evergreen.exe")))
  {
	if (CheckForInternetConnection())
	{
	 if (!await DownloadEvergreen())
	 {
	  VM.InfoMessage("Error", "Something went wrong. Please try again later.", InfoBarSeverity.Error);
	 }
	}
	else
	{
	 await Task.Delay(1000);
	 VM.InfoMessage("No Internet", "Please enable your internet connection and restart this app! This app needs the Evergreen WebView2 Runtime to get installed.", InfoBarSeverity.Error);
	 EnsureStartupStatusContent("Kein Internet. Die App konnte die benötigte WebView2-Laufzeit nicht herunterladen.");
	}
  }
  else
  {
	Install();
  }
 }

 private async Task<bool> DownloadEvergreen()
 {
  try
  {
	WebClient wc = new WebClient();

	wc.DownloadProgressChanged += (a, b) => { VM.ProgressValue = (double)b.ProgressPercentage; };
	wc.DownloadFileCompleted += (a, b) =>
	{
	 Install();
	};
	wc.DownloadFileAsync(new System.Uri("https://go.microsoft.com/fwlink/p/?LinkId=2124703"), Path.Combine(ApplicationData.Current.LocalFolder.Path, "evergreen.exe"));
	return true;
  }
  catch
  {
	return false;
  }


 }

 private async void Install()
 {
  EnsureStartupStatusContent("WebView2 Runtime wird installiert...");
  VM.IsIndeterminate = true;
  VM.InfoMessage("Installing...", "Please wait up to 2 minutes for the Evergreen WebView2 Runtime to install.", InfoBarSeverity.Informational);
  bool InstallSuccessful = false;
  await Task.Run(async () => { InstallSuccessful = await InstallTask(); });
  if (InstallSuccessful)
  {
	VM.Default.EvergreenInstalled = true;
	VM.InfoMessage("Success!", "The editor and viewer controls are now fully operational.", InfoBarSeverity.Success);
	VM.IsLoadingBarVisible = false;
	await Task.Delay(2500);
	VM.IsIndeterminate = true;
	if (!RootFrame.Navigate(typeof(MainPage)))
	{
	 await ShowStartupErrorAsync("Navigation failed", new InvalidOperationException("Navigation to MainPage returned false."));
	}
	try { _startupWatchdog?.Stop(); } catch { }
  }
  else
  {
	VM.InfoMessage("Error", "Something went wrong. Please try again later.", InfoBarSeverity.Error);
	VM.IsLoadingBarVisible = false;
	EnsureStartupStatusContent("Installation der benötigten WebView2-Laufzeit ist fehlgeschlagen.");
  }
 }


 private async Task<bool> InstallTask()
 {
  Process p = new Process();
  ProcessStartInfo info = new ProcessStartInfo(Path.Combine(ApplicationData.Current.LocalFolder.Path, "evergreen.exe"))
  {
	RedirectStandardInput = false,
	RedirectStandardOutput = false,
	RedirectStandardError = false,
	CreateNoWindow = false,
	WindowStyle = ProcessWindowStyle.Normal,
	UseShellExecute = false,

	Verb = "runas",
  };
  p.OutputDataReceived += (e, f) =>
  { //VM.Log(f.Data.);
  };
  //p.ErrorDataReceived += (e, f) => {Log(f.Data); };
  p.StartInfo = info;


  p.Start();
  p.WaitForExit();

  int exit = p.ExitCode;

  p.Close();

  return exit == 0;
 }
 private async void RootFrame_Loaded(object sender, RoutedEventArgs e)
 {
  EnsureStartupStatusContent("Initialisiere ConTeXt IDE...");
  if (IsCustomizationSupported) // Evergreen is preinstalled in W11
  {
	VM.Default.EvergreenInstalled = true;
  }



  try
  {
	if (!VM.Default.EvergreenInstalled)
	{
	 string RegKey = Environment.Is64BitOperatingSystem ? @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" : @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";
	 var version = Registry.GetValue(RegKey, "pv", null);
	 VM.Default.EvergreenInstalled = version != null && !string.IsNullOrWhiteSpace(version?.ToString());
	}
  }
  catch (Exception ex)
  {
	App.write("Registry check failed: " + ex);
	VM?.Log("Registry check failed: " + ex.Message);
  }

  try
  {
	if (VM?.Default?.EvergreenInstalled == true)
	{
	 VM.IsIndeterminate = true;
	 if (!RootFrame.Navigate(typeof(MainPage)))
	 {
	  throw new InvalidOperationException("Navigation to MainPage returned false.");
	 }
	 try { _startupWatchdog?.Stop(); } catch { }

	 if (AW.Presenter is OverlappedPresenter OP)
	 {
	  if (VM?.Default.WindowSettingMain.IsMaximized == true)
	  {
		OP.Maximize();
	  }
	  else
	  {
		AW.Resize(new(Math.Max(VM.Default.WindowSettingMain.LastSize.Width, 1024), Math.Max(VM.Default.WindowSettingMain.LastSize.Height, 576)));
	  }
	 }
	}
	else
	{
	 await Task.Delay(1000);
	 InstallEvergreen();
	}
  }
  catch (Exception ex)
  {
	await ShowStartupErrorAsync("Startup error", ex);
  }
 }

 private void EnsureStartupStatusContent(string message)
 {
  try
  {
   if (RootFrame == null)
	{
	 Content = new ScrollViewer
	 {
	  Content = new TextBlock
	  {
		Text = message,
		TextWrapping = TextWrapping.Wrap,
		Margin = new Thickness(24)
	  }
	 };
	 return;
	}
	if (RootFrame.Content is TextBlock tb)
	{
	 tb.Text = message;
	 return;
	}
	if (RootFrame.Content == null || RootFrame.Content is not Page)
	{
	 RootFrame.Content = new TextBlock
	 {
	  Text = message,
	  TextWrapping = TextWrapping.Wrap,
	  Margin = new Thickness(24)
	 };
	}
  }
  catch { }
 }

 private async Task ShowStartupErrorAsync(string title, Exception ex)
 {
  string details = ex?.ToString() ?? "Unknown startup error.";
  App.write(details);
  VM?.Log(details);

  string message = $"ConTeXt IDE konnte nicht vollständig starten.\n\n{ex?.Message}\n\nDetails wurden in Error-Log.txt geschrieben.";
  try
  {
	ContentDialog dialog = new()
	{
	 Title = title,
	 Content = message,
	 CloseButtonText = "Schließen",
	 XamlRoot = RootFrame?.XamlRoot
	};
	await dialog.ShowAsync();
  }
  catch
  {
	try
	{
	 RootFrame.Content = new TextBlock
	 {
	  Text = message,
	  TextWrapping = TextWrapping.Wrap,
	  Margin = new Thickness(24)
	 };
	}
	catch { }
  }
 }

}


public static class WindowIconHelper
{
 private const int WM_SETICON = 0x80;
 private const int IMAGE_ICON = 1;
 private const int LR_LOADFROMFILE = 0x00000010;

 private static readonly IntPtr ICON_SMALL = new IntPtr(0); // Titelleiste

 [DllImport("user32.dll", SetLastError = true)]
 private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

 [DllImport("user32.dll", SetLastError = true)]
 private static extern IntPtr LoadImage(IntPtr hInst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

 public static void SetWindowIcon(IntPtr hwnd, string iconPath)
 {
  IntPtr hIcon = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 0, 0, LR_LOADFROMFILE);
  if (hIcon != IntPtr.Zero)
  {
	SendMessage(hwnd, WM_SETICON, ICON_SMALL, hIcon);
  }
 }
}

