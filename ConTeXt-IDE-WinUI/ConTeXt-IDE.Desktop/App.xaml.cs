using CodeEditorControl_WinUI;
using ConTeXt_IDE.Helpers;
using ConTeXt_IDE.Models;
using ConTeXt_IDE.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.System;
using Windows.UI.ViewManagement;

namespace ConTeXt_IDE
{
 public partial class App : Application

 {
  public static ViewModel VM { get; set; }
  public static MainWindow MainWindow { get; set; }
  private Exception _startupInitException;
  private const string MainInstanceKey = "MainAppInstance";
  private const string ProjectInstanceKeyPrefix = "ConTeXtIDE|project|";
  private const string NoProjectInstanceKeyPrefix = "ConTeXtIDE|noproj|";
  private static string _registeredInstanceKey;
  private const int SW_RESTORE = 9;

  [DllImport("user32.dll")]
  private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

  [DllImport("user32.dll")]
  private static extern bool SetForegroundWindow(IntPtr hWnd);

  [DllImport("user32.dll")]
  private static extern bool IsIconic(IntPtr hWnd);

  //private const string MutexName = "##||ConTeXt_IDE||##";
  //private Mutex _mutex;
  //bool createdNew;



  public App()
  {
	try
	{
	 this.InitializeComponent();
	 UnhandledException += App_UnhandledException;
	 var uiSettings = new UISettings();

	 var defaultthemecolor = uiSettings.GetColorValue(UIColorType.Background);

	 if (Settings.Default.Theme == "Light")
	 {
	  RequestedTheme = ApplicationTheme.Light;
	 }
	 else if (Settings.Default.Theme == "Dark")
	 {
	  RequestedTheme = ApplicationTheme.Dark;
	 }
	 else
	 {
	  //RequestedTheme = defaultthemecolor == Colors.White ? ApplicationTheme.Light : ApplicationTheme.Dark;
	 }
	}
	catch (Exception ex)
	{
	 _startupInitException = ex;
	 write("App ctor exception: " + ex);
	}
  }

  private async void AI_Activated(object sender, AppActivationArguments e)
  {
	switch (e.Data)
	{
	 case Windows.ApplicationModel.Activation.LaunchActivatedEventArgs args:
	  await HandleLaunchActivationAsync(args);
	  break;
	 case ProtocolActivatedEventArgs protocolArgs:
	  {
		string projectPath = TryGetProjectPathFromProtocolActivation(protocolArgs);
		if (!string.IsNullOrWhiteSpace(projectPath))
		 await HandleProjectLaunchPathAsync(projectPath);
		break;
	  }
	 case FileActivatedEventArgs fileArgs:
	  HandleFileActivation(fileArgs);
	  break;
	}
  }

  private Task RunOnUiThreadAsync(Func<Task> action)
  {
	if (action == null)
	 return Task.CompletedTask;

	if (MainWindow?.DispatcherQueue == null)
	 return action();

	var tcs = new TaskCompletionSource<object>();
	bool enqueued = MainWindow.DispatcherQueue.TryEnqueue(async () =>
	{
	 try
	 {
	  await action();
	  tcs.TrySetResult(null);
	 }
	 catch (Exception ex)
	 {
	  tcs.TrySetException(ex);
	 }
	});

	if (!enqueued)
	{
	 return action();
	}

	return tcs.Task;
  }

  private static string NormalizePath(string path)
  {
	if (string.IsNullOrWhiteSpace(path))
	 return string.Empty;

	return Path.GetFullPath(path)
	  .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
	  .ToLowerInvariant();
  }

  private static bool IsPathInProject(string filePath, string projectPath)
  {
	if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(projectPath))
	 return false;

	string normalizedFilePath = NormalizePath(filePath);
	string normalizedProjectPath = NormalizePath(projectPath);

	return normalizedFilePath == normalizedProjectPath || normalizedFilePath.StartsWith(normalizedProjectPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
  }

  private static string TryGetProjectPathFromProtocolActivation(ProtocolActivatedEventArgs protocolArgs)
  {
	if (protocolArgs?.Uri == null)
	 return null;

	try
	{
	 string query = protocolArgs.Uri.Query;
	 if (string.IsNullOrWhiteSpace(query))
	  return null;

	 foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
	 {
	  var kvp = part.Split('=', 2);
	  if (kvp.Length != 2)
		continue;

	  if (!string.Equals(kvp[0], "project", StringComparison.OrdinalIgnoreCase))
		continue;

	  string value = Uri.UnescapeDataString(kvp[1]);
	  return string.IsNullOrWhiteSpace(value) ? null : value;
	 }
	}
	catch
	{
	}

	return null;
  }

  public static async Task<bool> TryLaunchProjectInNewInstanceAsync(string projectPath)
  {
	try
	{
	 if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath))
	  return false;

	 string uri = $"contextide://open?project={Uri.EscapeDataString(projectPath)}";
	 return await Launcher.LaunchUriAsync(new Uri(uri));
	}
	catch
	{
	 return false;
	}
  }



  private static bool TryGetProjectPathFromInstanceKey(string key, out string projectPath)
  {
	projectPath = null;
	if (string.IsNullOrWhiteSpace(key) || !key.StartsWith(ProjectInstanceKeyPrefix, StringComparison.OrdinalIgnoreCase))
	 return false;

	string remaining = key.Substring(ProjectInstanceKeyPrefix.Length);
	int separatorIndex = remaining.LastIndexOf('|');
	if (separatorIndex <= 0)
	 return false;

	projectPath = remaining[..separatorIndex];
	return !string.IsNullOrWhiteSpace(projectPath);
  }

  private static bool TryGetProjectInstanceInfo(string key, out string projectPath, out int processId)
  {
	projectPath = null;
	processId = 0;
	if (string.IsNullOrWhiteSpace(key) || !key.StartsWith(ProjectInstanceKeyPrefix, StringComparison.OrdinalIgnoreCase))
	 return false;

	string remaining = key.Substring(ProjectInstanceKeyPrefix.Length);
	int separatorIndex = remaining.LastIndexOf('|');
	if (separatorIndex <= 0 || separatorIndex >= remaining.Length - 1)
	 return false;

	projectPath = remaining[..separatorIndex];
	if (!int.TryParse(remaining[(separatorIndex + 1)..], out processId))
	 return false;

	return !string.IsNullOrWhiteSpace(projectPath) && processId > 0;
  }

  private static bool ActivateProcessMainWindow(int processId)
  {
	try
	{
	 var process = Process.GetProcessById(processId);
	 IntPtr hWnd = process.MainWindowHandle;
	 if (hWnd == IntPtr.Zero)
	  return false;

	 if (IsIconic(hWnd))
	  ShowWindowAsync(hWnd, SW_RESTORE);

	 return SetForegroundWindow(hWnd);
	}
	catch
	{
	 return false;
	}
  }

  public static bool IsProjectOpenedInAnotherInstance(string projectPath)
  {
	if (string.IsNullOrWhiteSpace(projectPath))
	 return false;

	string normalizedProjectPath = NormalizePath(projectPath);
	foreach (var instance in AppInstance.GetInstances())
	{
	 if (instance.IsCurrent)
	  continue;

	 if (TryGetProjectPathFromInstanceKey(instance.Key, out string openedProjectPath) && string.Equals(NormalizePath(openedProjectPath), normalizedProjectPath, StringComparison.OrdinalIgnoreCase))
	  return true;
	}

	return false;
  }

  public static Task<bool> TryRedirectProjectLaunchToOpenedInstanceAsync(string projectPath)
  {
	if (string.IsNullOrWhiteSpace(projectPath))
	 return Task.FromResult(false);

	string normalizedProjectPath = NormalizePath(projectPath);
	foreach (var instance in AppInstance.GetInstances())
	{
	 if (instance.IsCurrent)
	  continue;

	 if (!TryGetProjectInstanceInfo(instance.Key, out string openedProjectPath, out int processId))
	  continue;

	 if (!string.Equals(NormalizePath(openedProjectPath), normalizedProjectPath, StringComparison.OrdinalIgnoreCase))
	  continue;

	 ActivateProcessMainWindow(processId);
	 return Task.FromResult(true);
	}

	return Task.FromResult(false);
  }

  private async Task<bool> TryRedirectFileActivationToProjectInstanceAsync(AppActivationArguments activation, AppInstance currentInstance)
  {
	if (activation?.Data is not FileActivatedEventArgs fileArgs)
	 return false;

	string activatedPath = fileArgs.Files?.OfType<StorageFile>()?.FirstOrDefault()?.Path;
	if (string.IsNullOrWhiteSpace(activatedPath))
	 return false;

	foreach (var instance in AppInstance.GetInstances())
	{
	 if (instance.IsCurrent)
	  continue;

	 if (TryGetProjectPathFromInstanceKey(instance.Key, out string projectPath) && IsPathInProject(activatedPath, projectPath))
	 {
	  await instance.RedirectActivationToAsync(activation);
	  return true;
	 }
	}

	return false;
  }

  public static void UpdateCurrentInstanceProjectKey(string projectPath)
  {
	try
	{
	 if (Settings.Default?.MultiInstance != true)
	  return;

	 string key = string.IsNullOrWhiteSpace(projectPath)
		 ? $"{NoProjectInstanceKeyPrefix}{Environment.ProcessId}"
		 : $"{ProjectInstanceKeyPrefix}{NormalizePath(projectPath)}|{Environment.ProcessId}";

	 if (string.Equals(_registeredInstanceKey, key, StringComparison.OrdinalIgnoreCase))
	  return;

	 AppInstance.FindOrRegisterForKey(key);
	 _registeredInstanceKey = key;
	}
	catch
	{
	}
  }

  private async Task HandleLaunchActivationAsync(Windows.ApplicationModel.Activation.LaunchActivatedEventArgs args)
  {
	if (args == null || VM == null)
	 return;

	await HandleProjectLaunchPathAsync(args.Arguments);
  }

  private async Task HandleProjectLaunchPathAsync(string projectPath)
  {
	if (string.IsNullOrWhiteSpace(projectPath) || VM == null)
	 return;

	await RunOnUiThreadAsync(async () =>
	{
	 VM.LaunchArguments = projectPath;
	 if (VM.MainPage != null && VM.Started)
	 {
	  await VM.MainPage.TryOpenProjectFromLaunchArgumentAsync(projectPath);
	 }
	 else if (VM.Default.ProjectList.FirstOrDefault(x => x.Path == projectPath) is Project proj)
	 {
	  VM.CurrentProject = proj;
	 }
	 else if (Directory.Exists(projectPath))
	 {
	  VM.CurrentProject = new Project(projectPath, await StorageFolder.GetFolderFromPathAsync(projectPath));
	 }
	});
  }

  private void HandleFileActivation(FileActivatedEventArgs fileArgs)
  {
	if (fileArgs?.Files == null || VM == null)
	 return;

	if (!VM.Started || VM.MainPage == null)
	{
	 VM.FileActivatedEvents.Add(fileArgs);
	 return;
	}

	_ = RunOnUiThreadAsync(() =>
	  {
		foreach (var file in fileArgs.Files.OfType<StorageFile>())
		{
		 VM.OpenActivatedStorageFile(file);
		}
		return Task.CompletedTask;
	  });
  }

  public static async void write(string stringtowrite)
  {
	StorageFolder sf = ApplicationData.Current.LocalFolder;
	var file = await sf.CreateFileAsync("Error-Log.txt", CreationCollisionOption.OpenIfExists);
	await FileIO.WriteTextAsync(file, stringtowrite);
  }

  private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
  {
	write("Unhandled app exception: " + e.Message + " - " + e.Exception.StackTrace);
	VM?.Log("Unhandled app exception:" + e.Message + " - " + e.Exception.StackTrace);
  }

  protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
  {
	try
	{
	 if (_startupInitException != null)
	 {
	  throw new InvalidOperationException("Application initialization failed before launch.", _startupInitException);
	 }

	 AppInstance CurrentAI = AppInstance.GetCurrent();
	 var activation = CurrentAI.GetActivatedEventArgs();
	 CurrentAI.Activated += AI_Activated;

	 StartUp();
	 if (VM == null)
	  throw new InvalidOperationException("ViewModel initialization failed.");

	 if (!VM.Default.MultiInstance)
	 {
	  AppInstance DefaultAI = AppInstance.FindOrRegisterForKey(MainInstanceKey);
	  if (!DefaultAI.IsCurrent)
	  {
		await DefaultAI.RedirectActivationToAsync(activation);
		Exit();
		Environment.Exit(0);
		return;
	  }
	 }
	 else if (await TryRedirectFileActivationToProjectInstanceAsync(activation, CurrentAI))
	 {
	  Exit();
	  Environment.Exit(0);
	  return;
	 }

	 string launchProjectPath = (activation.Data as Windows.ApplicationModel.Activation.LaunchActivatedEventArgs)?.Arguments;
	 if (string.IsNullOrWhiteSpace(launchProjectPath) && activation.Data is ProtocolActivatedEventArgs protocolArgs)
	  launchProjectPath = TryGetProjectPathFromProtocolActivation(protocolArgs);
	 if (VM.Default.MultiInstance
 && !string.IsNullOrWhiteSpace(launchProjectPath)
 && Directory.Exists(launchProjectPath)
 && await TryRedirectProjectLaunchToOpenedInstanceAsync(launchProjectPath))
	 {
	  Exit();
	  Environment.Exit(0);
	  return;
	 }

	 UpdateCurrentInstanceProjectKey(launchProjectPath);

	 switch (activation.Data)
	 {
	  case Windows.ApplicationModel.Activation.LaunchActivatedEventArgs Args:
		if (!string.IsNullOrEmpty(Args.Arguments))
		{
		 VM.LaunchArguments = Args.Arguments;
		 VM.Log("Started with Launch Argument: " + Args.Arguments);
		}
		break;
	  case ProtocolActivatedEventArgs protocolActivationArgs:
		if (!string.IsNullOrWhiteSpace(launchProjectPath))
		{
		 VM.LaunchArguments = launchProjectPath;
		 VM.Log("Started with Protocol Argument: " + launchProjectPath);
		}
		break;
	  case FileActivatedEventArgs fileArgs:
		VM.FileActivatedEvents.Add(fileArgs);
		VM.Log("Started with File Activation.");
		break;
	 }

	 MainWindow = new MainWindow();
	 MainWindow.Activate();
	}
	catch (Exception ex)
	{
	 ShowFatalStartupWindow(ex);
	}

	// It seems we don't need the old Mutex stuff anymore.
	//_mutex = new Mutex(true, MutexName, out createdNew);
	//if (!createdNew && !VM.Default.MultiInstance)
	//{
	//	Application.Current.Exit();
	//	Environment.Exit(0);

	//	return;
	//}
  }

  private void StartUp()
  {
	try
	{
	 Resources.TryGetValue("VM", out object Vm);
	 if (Vm != null)
	 {
	  VM = Vm as ViewModel;
	 }
	 else VM = new ViewModel();

	 var setting = ((AccentColorSetting)Application.Current.Resources["AccentColorSetting"]);
	 setting.Theme = VM.Default.Theme == "Light" ? ElementTheme.Light : ElementTheme.Dark;
	 setting.AccentColor = VM.AccentColor.Color;
	 var accentColor = VM.AccentColor;

	 if (accentColor != null)
	 {
	  setting.Theme = (ElementTheme)Enum.Parse(typeof(ElementTheme), Settings.Default.Theme);
	  setting.AccentColor = accentColor.Color;
	  Application.Current.Resources["SystemAccentColor"] = accentColor.Color;
	  Application.Current.Resources["SystemAccentColorLight2"] = setting.AccentColorLow;
	  Application.Current.Resources["SystemAccentColorDark1"] = setting.AccentColorLow.ChangeColorBrightness(0.1f);
	  Application.Current.Resources["WindowCaptionBackground"] = setting.AccentColorLow;
	  Application.Current.Resources["WindowCaptionBackgroundDisabled"] = setting.AccentColorLow;
	 }

	}
	catch (Exception ex)
	{
	 write("Exception on Startup: " + ex);
	 VM?.Log("Exception on Startup: " + ex.Message);
	}
  }

  private void ShowFatalStartupWindow(Exception ex)
  {
	write("Fatal startup error: " + ex);
	try
	{
	 var errorWindow = new Window();
	 errorWindow.Content = new ScrollViewer
	 {
	  Content = new TextBlock
	  {
		Text = "ConTeXt IDE konnte nicht starten.\n\n" + ex.Message + "\n\nDetails: \n" + ex,
		TextWrapping = TextWrapping.Wrap,
		Margin = new Thickness(24)
	  }
	 };
	 errorWindow.Activate();
	}
	catch
	{
	 Exit();
	}
  }
 }
}