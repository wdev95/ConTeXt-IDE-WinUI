using CodeEditorControl_WinUI;
using ConTeXt_IDE.Helpers;
using ConTeXt_IDE.Shared.Models;
using Microsoft.UI.Xaml;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.System.Diagnostics;
using Windows.UI;

namespace ConTeXt_IDE.Models
{
 public class Settings : Helpers.Bindable
 {
  private static FileSystemWatcher watcher;
  private static readonly object _writeLock = new();
  private static readonly SemaphoreSlim _reloadSemaphore = new(1, 1);
  private static bool _isApplyingExternalSettings;
  private static bool _isWatcherInitialized;
  private static long _lastLocalWriteTicks;
  private static ObservableCollection<Project> _trackedProjectList;
  private static ObservableCollection<CommandFavorite> _trackedCommandFavorites;
  [JsonIgnore]
  public static bool IsApplyingExternalSettings => _isApplyingExternalSettings;
  public Settings()
  {

  }

  public static Settings FromJson(string json) => JsonConvert.DeserializeObject<Settings>(json);

  [JsonIgnore]
  public static Settings Default { get; internal set; } = GetSettings();

  public static Settings RestoreSettings()
  {
	try
	{
	 string file = "settings.json";
	 var storageFolder = ApplicationData.Current.LocalFolder;
	 string settingsPath = Path.Combine(storageFolder.Path, file);
	 if (File.Exists(settingsPath))
	 {
	  File.Delete(settingsPath);
	 }
	 return GetSettings();
	}
	catch
	{
	 return null;
	}
  }

  private static Settings GetSettings()
  {
	try
	{
	 string file = "settings.json";
	 var storageFolder = ApplicationData.Current.LocalFolder;
	 string settingsPath = Path.Combine(storageFolder.Path, file);
	 Settings settings;

	 if (!File.Exists(settingsPath))
	 {
	  settings = new Settings();
	  string json = settings.ToJson();
	  WriteSettings(settings.ToJson());
	 }
	 else
	 {
	  string json = File.ReadAllText(settingsPath);
	  settings = FromJson(json);
	 }

	 if (settings.CommandFavorites.Count == 0)
	 {
	  settings.CommandFavorites =
	  new ObservableCollection<CommandFavorite>()
	  {
					new(@"\startsection"), new(@"\startalignment"), new(@"\setuplayout"), new(@"\setuppapersize"), new(@"\starttext"), new(@"\startfrontmatter"), new(@"\startbodymatter"), new(@"\startappendices"), new(@"\startbackmatter"),
					new(@"\startframed"), new(@"\setupbodyfont"), new(@"\setupfooter"), new(@"\setupheader"),  new(@"\setuphead"),  new(@"\setupcaptions"), new(@"\setupcombinations"), new(@"\setupinteraction"), new(@"\placebookmarks"), new(@"\setuplist"), new(@"\environment"),  new(@"\startenvironment"), new(@"\product"),  new(@"\startproduct"), new(@"\component"),  new(@"\startcomponent"), new(@"\cite"), new(@"\setupTABLE"),
	  };
	 }

	 settings.CommandFavorites.CollectionChanged += (o, a) =>
  {
	if (!_isApplyingExternalSettings)
	{
	 settings.CurrentWindowID = ProcessDiagnosticInfo.GetForCurrentProcess().ProcessId;
	 WriteSettings(settings.ToJson());
	}
  };

	 if (settings.HelpItemList.Count == 0)
	 {
	  settings.HelpItemList =
	  new ObservableCollection<HelpItem>()
		  {
							new HelpItem() { ID = "Modes", Title = "ConTeXt Modes", Text = "Select any number of modes. They will activate the corresponding \n'\\startmode[<ModeName>] ... \\stopmode'\n environments.", Shown = false },
							new HelpItem() { ID = "Environments", Title = "ConTeXt Environments", Text = "Select any number of environments (usually one). Use this compiler parameter *instead* of the corresponding \n'\\environment[<EnvironmentName>]'\n commands.", Shown = false },
							new HelpItem() { ID = "AddProject", Title = "Add a Project", Text = "Click this button to open an existing project folder or to create a new project folder from a template.", Shown = false },
		  };
	 }
	 if (settings.PDFViewerList.Count == 0)
	 {
	  settings.CurrentPDFViewer = new("Default");
	  settings.PDFViewerList.Add(settings.CurrentPDFViewer);
	 }
	 settings.CurrentPDFViewer = settings.PDFViewerList.FirstOrDefault(x => x.Name == settings.CurrentPDFViewer.Name); // Ensure that the selected object is not a new object and is actually from within the collection

	 if (settings.ContextModules.Count == 0)
	 {
	  settings.ContextModules = new ObservableCollection<ContextModule>() {
									new ContextModule() {  Name = "filter", Description = "Process contents of a start-stop environment through an external program (Installed Pandoc needs to be in PATH!)", URL = @"https://modules.contextgarden.net/dl/t-filter.zip", Type = ContextModuleType.TDSArchive},

									new ContextModule() {  Name = "vim", Description = "This module uses Vim editor's syntax files to syntax highlight verbatim code in ConTeXt (Module filter needs to be installed! Installed vim needs to be in PATH!)", URL = @"https://modules.contextgarden.net/dl/t-vim.zip", Type = ContextModuleType.TDSArchive},
									new ContextModule() {  Name = "annotation", Description = "Lets you create your own commands and environments to mark text blocks.", URL = @"https://modules.contextgarden.net/dl/t-annotation.zip", Type = ContextModuleType.TDSArchive},
									new ContextModule() {  Name = "simpleslides", Description = "A module for creating presentations in ConTeXt.", URL = @"https://modules.contextgarden.net/dl/t-simpleslides.zip", Type = ContextModuleType.TDSArchive},
									new ContextModule() {  Name = "gnuplot", Description = "Inclusion of Gnuplot graphs in ConTeXt (Installed Gnuplot needs to be in PATH!)", URL = @"https://mirrors.ctan.org/macros/context/contrib/context-gnuplot.zip", Type = ContextModuleType.Archive, ArchiveFolderPath = @"context-gnuplot\"},
									new ContextModule() {  Name = "letter", Description = "Package for writing letters", URL = @"https://modules.contextgarden.net/dl/t-letter.zip", Type = ContextModuleType.TDSArchive },
									new ContextModule() { Name = "pgf", Description = "Create PostScript and PDF graphics in TeX", URL = @"http://mirrors.ctan.org/install/graphics/pgf/base/pgf.tds.zip", Type = ContextModuleType.TDSArchive},
									new ContextModule() { Name = "pgfplots", Description = "Create normal/logarithmic plots in two and three dimensions", URL = @"http://mirrors.ctan.org/install/graphics/pgf/contrib/pgfplots.tds.zip", Type = ContextModuleType.TDSArchive},
					};
	 }
	 if (settings.TokenColorDefinitions.Count == 0)
	 {
	  settings.TokenColorDefinitions = new() {
						new() { Token = Token.Normal, Color = Color.FromArgb(255, 220, 220, 220) },
						new() { Token = Token.Comment, Color = Color.FromArgb(255, 30, 180, 40) },

						new() { Token = Token.Command, Color = Color.FromArgb(255, 40, 120, 240) },
						new() { Token = Token.Function, Color = Color.FromArgb(255, 120, 110, 220) },
						new() { Token = Token.Special, Color = Color.FromArgb(255, 120, 110, 220) },
						new() { Token = Token.Environment, Color = Color.FromArgb(255, 50, 190, 150) },
						new() { Token = Token.Primitive, Color = Color.FromArgb(255, 230, 60, 30) },
						new() { Token = Token.Style, Color = Color.FromArgb(255, 220, 50, 150) },
						new() { Token = Token.Array, Color = Color.FromArgb(255, 200, 100, 80) },

						new() { Token = Token.Key, Color = Color.FromArgb(255, 140, 210, 150) },


						new() { Token = Token.Reference, Color = Color.FromArgb(255, 180, 140, 40) },
						new() { Token = Token.Math, Color = Color.FromArgb(255, 220, 160, 60) },

						new() { Token = Token.Symbol, Color = Color.FromArgb(255, 140, 200, 240) },
						new() { Token = Token.Bracket, Color = Color.FromArgb(255, 120, 200, 220) },
						new() { Token = Token.Number, Color = Color.FromArgb(255, 180, 220, 180) },

						new() { Token = Token.Keyword, Color = Color.FromArgb(255, 40, 120, 240) },
						new() { Token = Token.String, Color = Color.FromArgb(255, 235, 120, 70) },

					};
	 }

	 AttachPersistenceHandlers(settings);

	 ProcessDiagnosticInfo diagnosticInfo = ProcessDiagnosticInfo.GetForCurrentProcess();
	 settings.CurrentWindowID = diagnosticInfo.ProcessId;
	 WriteSettings(settings.ToJson());
	 StartWatchingSettingsFile();
	 return settings;
	}
	catch (Exception ex)
	{
	 //App.VM.Log("Exception on getting Settings: "+ex.Message);
	 return RestoreSettings();
	}


  }

  public static void WriteSettings(string json)
  {
	string file = "settings.json";
	var storageFolder = ApplicationData.Current.LocalFolder;
	string settingsPath = Path.Combine(storageFolder.Path, file);
	App.VM?.Log("Writing settings...");
	lock (_writeLock)
	{
	 Interlocked.Exchange(ref _lastLocalWriteTicks, DateTime.UtcNow.Ticks);
	 File.WriteAllText(settingsPath, json);
	}

  }

  private static void AttachPersistenceHandlers(Settings settings)
  {
	settings.PropertyChanged -= Settings_PropertyChanged;
	settings.PropertyChanged += Settings_PropertyChanged;
	RewireCollectionHandlers(settings);
  }

  private static void Settings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
  {
	if (_isApplyingExternalSettings || sender is not Settings settings)
	 return;

	if (e.PropertyName == nameof(ProjectList) || e.PropertyName == nameof(CommandFavorites))
	 RewireCollectionHandlers(settings);

	settings.CurrentWindowID = ProcessDiagnosticInfo.GetForCurrentProcess().ProcessId;
	WriteSettings(settings.ToJson());
  }

  private static void RewireCollectionHandlers(Settings settings)
  {
	if (_trackedProjectList != null)
	 _trackedProjectList.CollectionChanged -= ProjectList_CollectionChanged;
	_trackedProjectList = settings.ProjectList;
	if (_trackedProjectList != null)
	 _trackedProjectList.CollectionChanged += ProjectList_CollectionChanged;

	if (_trackedCommandFavorites != null)
	 _trackedCommandFavorites.CollectionChanged -= CommandFavorites_CollectionChanged;
	_trackedCommandFavorites = settings.CommandFavorites;
	if (_trackedCommandFavorites != null)
	 _trackedCommandFavorites.CollectionChanged += CommandFavorites_CollectionChanged;
  }

  private static void ProjectList_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
  {
	if (!_isApplyingExternalSettings && Default != null)
	{
	 Default.CurrentWindowID = ProcessDiagnosticInfo.GetForCurrentProcess().ProcessId;
	 WriteSettings(Default.ToJson());
	}
  }

  private static void CommandFavorites_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
  {
	if (!_isApplyingExternalSettings && Default != null)
	{
	 Default.CurrentWindowID = ProcessDiagnosticInfo.GetForCurrentProcess().ProcessId;
	 WriteSettings(Default.ToJson());
	}
  }

  private static void StartWatchingSettingsFile()
  {
	if (_isWatcherInitialized)
	 return;

	watcher?.Dispose();
	watcher = new FileSystemWatcher(ApplicationData.Current.LocalFolder.Path)
	{
	 IncludeSubdirectories = false,
	 EnableRaisingEvents = true,
	 Filter = "settings.json",
	 NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime
	};

	watcher.Changed += Watcher_Changed;
	watcher.Created += Watcher_Changed;
	watcher.Renamed += Watcher_Changed;
	_isWatcherInitialized = true;
  }

  private static void Watcher_Changed(object sender, FileSystemEventArgs e)
  {
	_ = ReloadSettingsFromFileAsync(e.FullPath);
  }

  private static async Task ReloadSettingsFromFileAsync(string fullPath)
  {
	await _reloadSemaphore.WaitAsync();
	try
	{
	 if (DateTime.UtcNow.Ticks - Interlocked.Read(ref _lastLocalWriteTicks) < TimeSpan.FromMilliseconds(500).Ticks)
	  return;

	 await Task.Delay(80);

	 Settings updatedSettings = null;
	 for (int i = 0; i < 5; i++)
	 {
	  try
	  {
		if (!File.Exists(fullPath))
		 return;

		string json;
		using (var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
		using (var sr = new StreamReader(fs))
		{
		 json = await sr.ReadToEndAsync();
		}

		if (!string.IsNullOrWhiteSpace(json))
		 updatedSettings = FromJson(json);

		if (updatedSettings != null)
		 break;
	  }
	  catch
	  {
		await Task.Delay(80);
	  }
	 }

	 if (updatedSettings == null)
	  return;

	 ProcessDiagnosticInfo diagnosticInfo = ProcessDiagnosticInfo.GetForCurrentProcess();
	 if (updatedSettings.CurrentWindowID == diagnosticInfo.ProcessId)
	  return;

	 void apply()
	 {
	  if (Default == null)
		return;

	  _isApplyingExternalSettings = true;
	  try
	  {
		CopySettingsValues(Default, updatedSettings);
		RewireCollectionHandlers(Default);
	  }
	  finally
	  {
		_isApplyingExternalSettings = false;
	  }
	 }

	 if (App.MainWindow?.DispatcherQueue != null && !App.MainWindow.DispatcherQueue.HasThreadAccess)
	  App.MainWindow.DispatcherQueue.TryEnqueue(apply);
	 else
	  apply();
	}
	catch (Exception ex)
	{
	 App.VM?.Log(ex.Message);
	}
	finally
	{
	 _reloadSemaphore.Release();
	}
  }

  private static void CopySettingsValues(Settings target, Settings source)
  {
	MergeProjectList(target, source);

	var properties = typeof(Settings).GetProperties(BindingFlags.Public | BindingFlags.Instance)
  .Where(x => x.CanRead && x.CanWrite && x.GetIndexParameters().Length == 0 && x.Name != nameof(Default) && x.Name != nameof(ProjectList));

	foreach (var property in properties)
	{
	 object sourceValue = property.GetValue(source);

	 if (property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(ObservableCollection<>))
	 {
	  var targetList = property.GetValue(target) as IList;
	  var sourceList = sourceValue as IEnumerable;
	  if (targetList != null && sourceList != null)
	  {
		targetList.Clear();
		foreach (var item in sourceList)
		 targetList.Add(item);
		continue;
	  }
	 }

	 property.SetValue(target, sourceValue);
	}
  }

  private static string NormalizeProjectPath(string path)
  {
	if (string.IsNullOrWhiteSpace(path))
	 return string.Empty;

	try
	{
	 return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToLowerInvariant();
	}
	catch
	{
	 return path.Trim().ToLowerInvariant();
	}
  }

  private static bool IsSameProject(Project a, Project b)
  {
	if (ReferenceEquals(a, b))
	 return true;
	if (a == null || b == null)
	 return false;

	string pathA = NormalizeProjectPath(a.Path);
	string pathB = NormalizeProjectPath(b.Path);
	if (!string.IsNullOrWhiteSpace(pathA) && !string.IsNullOrWhiteSpace(pathB))
	 return string.Equals(pathA, pathB, StringComparison.OrdinalIgnoreCase);

	return !string.IsNullOrWhiteSpace(a.Name) && string.Equals(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
  }

  private static void CopyProjectValues(Project target, Project source)
  {
	if (target == null || source == null)
	 return;

	if (!string.Equals(target.Name, source.Name, StringComparison.Ordinal))
	 target.Name = source.Name;

	if (!string.Equals(target.Path, source.Path, StringComparison.OrdinalIgnoreCase))
	 target.Path = source.Path;

	if (!string.Equals(target.RootFilePath, source.RootFilePath, StringComparison.OrdinalIgnoreCase))
	 target.RootFilePath = source.RootFilePath;

	List<string> sourceLastOpenedFiles = source.LastOpenedFiles ?? new List<string>();
	if (target.LastOpenedFiles == null || !target.LastOpenedFiles.SequenceEqual(sourceLastOpenedFiles))
	 target.LastOpenedFiles = new List<string>(sourceLastOpenedFiles);

	if (!ReferenceEquals(target.SyncTeX, source.SyncTeX))
	 target.SyncTeX = source.SyncTeX;

	ObservableCollection<Mode> sourceModes = source.Modes != null
	 ? new ObservableCollection<Mode>(source.Modes.Select(x => new Mode() { Name = x.Name, IsSelected = x.IsSelected }))
	 : new ObservableCollection<Mode>();
	if (!AreModeCollectionsEqual(target.Modes, sourceModes))
	 target.Modes = sourceModes;

	ObservableCollection<Mode> sourceEnvironments = source.Environments != null
	 ? new ObservableCollection<Mode>(source.Environments.Select(x => new Mode() { Name = x.Name, IsSelected = x.IsSelected }))
	 : new ObservableCollection<Mode>();
	if (!AreModeCollectionsEqual(target.Environments, sourceEnvironments))
	 target.Environments = sourceEnvironments;
  }

  private static bool AreModeCollectionsEqual(IList<Mode> a, IList<Mode> b)
  {
	if (ReferenceEquals(a, b))
	 return true;
	if (a == null || b == null || a.Count != b.Count)
	 return false;

	for (int i = 0; i < a.Count; i++)
	{
	 if (!string.Equals(a[i]?.Name, b[i]?.Name, StringComparison.Ordinal)
		 || a[i]?.IsSelected != b[i]?.IsSelected)
	  return false;
	}

	return true;
  }

  private static void MergeProjectList(Settings target, Settings source)
  {
	if (target?.ProjectList == null || source?.ProjectList == null)
	 return;

	var existing = target.ProjectList.ToList();
	foreach (var sourceProject in source.ProjectList)
	{
	 if (sourceProject == null)
	  continue;

	 var targetProject = existing.FirstOrDefault(x => IsSameProject(x, sourceProject));
	 if (targetProject == null)
	 {
	  target.ProjectList.Add(sourceProject);
	 }
	 else
	 {
	  CopyProjectValues(targetProject, sourceProject);
	 }
	}

	for (int i = target.ProjectList.Count - 1; i >= 0; i--)
	{
	 var targetProject = target.ProjectList[i];
	 if (!source.ProjectList.Any(x => IsSameProject(x, targetProject)))
	  target.ProjectList.RemoveAt(i);
	}
  }

  public bool AutoOpenLOG { get => Get(false); set => Set(value); }
  public bool AutoOpenPDFOnFileOpen { get => Get(false); set => Set(value); }
  public bool AutoOpenLOGOnlyOnError { get => Get(true); set => Set(value); }
  public bool AutoOpenPDF { get => Get(true); set => Set(value); }
  public bool DistributionInstalled { get => Get(false); set => Set(value); }
  public bool EvergreenInstalled { get => Get(false); set => Set(value); }
  public bool FirstStart { get => Get(true); set => Set(value); }
  public bool HelpPDFInInternalViewer { get => Get(false); set => Set(value); }
  public bool InternalViewer { get => Get(true); set { Set(value); if (App.VM != null) App.VM.IsInternalViewerActive = false; } }
  public bool PDFWindow { get => Get(false); set => Set(value); }
  public bool MultiInstance { get => Get(true); set => Set(value); }
  public bool ShowLog { get => Get(false); set => Set(value); }
  public bool ShowCompilerOutput { get => Get(false); set => Set(value); }
  public bool ShowOutline { get => Get(true); set => Set(value); }
  public bool ShowProjectPane { get => Get(true); set => Set(value); }

  public bool UseModernStyle { get => Get(true); set => Set(value); }
  public uint CurrentWindowID { get; set; } = (uint)0;

  public bool ShowMarkdownViewer { get => Get(false); set => Set(value); }
  public bool StartWithLastActiveProject { get => Get(true); set => Set(value); }
  public bool StartWithLastOpenFiles { get => Get(false); set => Set(value); }
  public bool SuggestArguments { get => Get(true); set => Set(value); }
  public bool SuggestCommands { get => Get(true); set => Set(value); }
  public bool SuggestFontSwitches { get => Get(true); set => Set(value); }
  public bool SuggestPrimitives { get => Get(true); set => Set(value); }
  public bool SuggestStartStop { get => Get(true); set => Set(value); }

  public bool TextWrapping { get => Get(false); set => Set(value); }
  public bool LineNumbers { get => Get(true); set => Set(value); }
  public bool LineMarkers { get => Get(true); set => Set(value); }
  public bool ShowScrollBars { get => Get(true); set => Set(value); }
  public bool ScrollbarMarkers { get => Get(true); set => Set(value); }
  public bool CodeFolding { get => Get(true); set => Set(value); }
  public bool ControlCharacters { get => Get(false); set => Set(value); }
  public bool FilterFavorites { get => Get(false); set => Set(value); }

  public string AccentColor { get => Get("Default"); set { Set(value); } }

  public string ContextVersion { get => Get(""); set { Set(value); } }
  public string ContextDistributionPath { get => Get(ApplicationData.Current.LocalFolder.Path); set => Set(value); }
  public string ContextDownloadLink { get => Get(@"http://lmtx.pragma-ade.nl/install-lmtx/context-mswin.zip"); set => Set(value); }
  public string LastActiveProject { get => Get(""); set => Set(value); }

  public WindowSetting WindowSettingMain { get => Get(new WindowSetting()); set => Set(value); }
  public WindowSetting WindowSettingPDF { get => Get(new WindowSetting()); set => Set(value); }

  public string NavigationViewPaneMode { get => Get("Auto"); set => Set(value); }
  public string PackageID { get => Get(Package.Current.Id.FamilyName); set => Set(value); }
  public int FontSize { get => Get(14); set => Set(value); }
  public int RibbonMarginValue
  {
	get => Get(4); set
	{
	 Set(value);
	 if (App.VM != null)
	 {
	  void apply()
	  {
		try
		{
		 App.VM.RibbonMarginValue = value;
		 Application.Current.Resources["OverlayCornerRadius"] = new CornerRadius(value);
		 Application.Current.Resources["CornerRadius"] = new CornerRadius(value);
		 Application.Current.Resources["LargeCornerRadius"] = new CornerRadius(2 * value);
		 Application.Current.Resources["CornerRadiusRight"] = new CornerRadius(0, value, value, 0);
		 Application.Current.Resources["CornerRadiusLeft"] = new CornerRadius(value, 0, 0, value);
		}
		catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException || ex is InvalidOperationException)
		{
		}
	  }

	  if (App.MainWindow?.DispatcherQueue != null && !App.MainWindow.DispatcherQueue.HasThreadAccess)
	  {
		if (!App.MainWindow.DispatcherQueue.TryEnqueue(apply))
		{
		 apply();
		}
	  }
	  else
	  {
		apply();
	  }
	 }
	}
  }
  public int TabLength { get => Get(2); set => Set(value); }
  public string Theme
  {
	get => Get("Default"); set
	{
	 Set(value);

	 //if (App.MainPage != null)
	 //    App.MainPage.SetColor(null, (ElementTheme)Enum.Parse(typeof(ElementTheme),value), false); ;
	}
  }

  public string Backdrop { get => Get("Mica"); set { Set(value); } }

  public ObservableCollection<Mode> Parameters
  {
	get => Get(new ObservableCollection<Mode>() { new Mode() { Name = "print", IsSelected = false }, new Mode() { Name = "screen", IsSelected = false }, new Mode() { Name = "draft", IsSelected = false }, });
	set => Set(value);
  }

  public List<CommandGroup> CommandGroups { get => Get(new List<CommandGroup>()); set => Set(value); }

  public ObservableCollection<CommandFavorite> CommandFavorites { get => Get(new ObservableCollection<CommandFavorite>()); set => Set(value); }

  public ObservableCollection<ContextModule> ContextModules { get => Get(new ObservableCollection<ContextModule>()); set => Set(value); }



  public ObservableCollection<Project> ProjectList { get => Get(new ObservableCollection<Project>()); set => Set(value); }

  public PDFViewer CurrentPDFViewer { get => Get(new PDFViewer()); set => Set(value); }

  public ObservableCollection<PDFViewer> PDFViewerList { get => Get(new ObservableCollection<PDFViewer>()); set => Set(value); }

  public ObservableCollection<HelpItem> HelpItemList { get => Get(new ObservableCollection<HelpItem>()); set => Set(value); }
  public ObservableCollection<TokenDefinition> TokenColorDefinitions
  {
	get => Get(new ObservableCollection<TokenDefinition>()
	{


	}); set => Set(value);
  }

  [Newtonsoft.Json.JsonIgnore]
  public string[] ThemeOption => Enum.GetNames<ElementTheme>();

  [Newtonsoft.Json.JsonIgnore]
  public string[] BackdropOption => Enum.GetNames<BackdropType>();
 }

 public static class Serialize
 {
  public static string ToJson(this Settings self) => JsonConvert.SerializeObject(self, Formatting.Indented);
 }

 public static class SettingsExtensions
 {
  public static void SaveSettings(this Settings settings)
  {
	string file = "settings.json";
	settings.CurrentWindowID = ProcessDiagnosticInfo.GetForCurrentProcess().ProcessId;
	Settings.WriteSettings(settings.ToJson());
  }
 }
}