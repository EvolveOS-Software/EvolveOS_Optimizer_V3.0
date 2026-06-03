// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using EvolveOS_Optimizer.Views;
using WinRT.Interop;

namespace EvolveOS_Optimizer.Core.ViewModel
{
    public partial class ScriptsViewModel : ObservableObject, IDisposable
    {
        #region Events & Delegates
        public Action<TerminalOutputWindow>? RequestShowTerminal;
        public event Action? OnScriptsUpdated;
        #endregion

        #region Fields & Constants
        public enum SortMode { Name, Extension }
        private readonly object _locker = new();
        private string _lastKnownFolderPath = string.Empty;
        private readonly string[] _allowedExtensions = { ".ps1", ".bat", ".cmd", ".reg" };
        #endregion

        #region Observable Properties
        [ObservableProperty]
        public partial string SearchText { get; set; } = string.Empty;
        partial void OnSearchTextChanging(string value) => RefreshFilteredScripts();

        [ObservableProperty]
        public partial string SelectedPath { get; set; } = string.Empty;
        partial void OnSelectedPathChanged(string value)
        {
            if (!string.IsNullOrWhiteSpace(value) && Directory.Exists(value))
            {
                SettingsEngine.UserScriptsPath = value;
                _ = RefreshScriptsAsync();
                OnPropertyChanged(nameof(IsPathSet));
            }
        }

        partial void OnIsMultiSelectModeChanged(bool value)
        {
            if (!value)
            {
                ClearSelection();
            }
        }

        [ObservableProperty] public partial bool IsMultiSelectMode { get; set; }
        [ObservableProperty] public partial bool IsRunAsTrustedInstaller { get; set; }
        [ObservableProperty] public partial bool IsSortDescending { get; set; }
        [ObservableProperty] public partial SortMode CurrentSortMode { get; set; }

        public bool IsPathSet => !string.IsNullOrWhiteSpace(SelectedPath);
        public bool HasSelection => Scripts.Any(s => s.IsSelected);
        #endregion

        #region Collections
        public ObservableCollection<string> SavedPaths { get; } = new();
        public ObservableCollection<ScriptsModel> Scripts { get; } = new();
        public ObservableCollection<ScriptsModel> FilteredScripts { get; } = new();
        #endregion

        #region Constructor & Initialization
        public ScriptsViewModel()
        {
            IsSortDescending = false;
            CurrentSortMode = SortMode.Name;

            InitializeHistory();

            if (!string.IsNullOrWhiteSpace(SettingsEngine.UserScriptsPath))
            {
                SelectedPath = SettingsEngine.UserScriptsPath;
                _ = RefreshScriptsAsync();
            }
        }

        private void InitializeHistory()
        {
            var history = SettingsEngine.AllUserScriptsPaths;
            if (history == null) return;

            foreach (var path in history.Where(Directory.Exists))
            {
                SavedPaths.Add(path);
            }
        }
        #endregion

        #region Drag & Drop Processing
        public async Task HandleDropAsync(string[] paths)
        {
            if (paths == null || paths.Length == 0) return;

            bool foundInvalidFiles = false;

            var result = await Task.Run(() =>
            {
                bool listChanged = false;
                string? folderToFocus = null;
                var currentHistory = SavedPaths.ToList();

                foreach (var path in paths)
                {
                    string? targetFolder = null;

                    if (Directory.Exists(path))
                    {
                        targetFolder = path;
                    }
                    else if (File.Exists(path))
                    {
                        string extension = Path.GetExtension(path).ToLowerInvariant();
                        if (_allowedExtensions.Contains(extension))
                        {
                            string? parent = Path.GetDirectoryName(path);
                            if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
                            {
                                targetFolder = parent;
                            }
                        }
                        else
                        {
                            foundInvalidFiles = true;
                            continue;
                        }
                    }

                    if (targetFolder != null)
                    {
                        if (currentHistory.Contains(targetFolder))
                            currentHistory.Remove(targetFolder);

                        currentHistory.Insert(0, targetFolder);
                        folderToFocus = targetFolder;
                        listChanged = true;
                    }
                }
                return new { listChanged, folderToFocus, currentHistory };
            });

            if (foundInvalidFiles)
            {
                NotificationManager.Show(ResourceString.GetString("noty_warn_title"), ResourceString.GetString("warn_invalid_script_ext"))
                                   .WithSeverity(NotificationManager.NoticeSeverity.Warning)
                                   .Perform();
            }

            if (result.listChanged && result.folderToFocus != null)
            {
                SavedPaths.Clear();
                foreach (var p in result.currentHistory) SavedPaths.Add(p);

                SettingsEngine.AllUserScriptsPaths = SavedPaths.ToList();
                SelectedPath = result.folderToFocus;
                OnPropertyChanged(nameof(IsPathSet));
            }
        }
        #endregion

        #region Commands
        [RelayCommand]
        private void SelectFolder()
        {
            try
            {
                var ofn = new OpenFileName();
                ofn.structSize = Marshal.SizeOf(ofn);

                var activeWindow = (Application.Current as App)?.GetType().GetProperty("MainWindow")?.GetValue(Application.Current) as Window;
                if (activeWindow != null)
                {
                    ofn.hwnd = WindowNative.GetWindowHandle(activeWindow);
                }

                ofn.filter = "Folders\0*.none\0All Files (*.*)\0*.*\0";
                ofn.filterIndex = 1;

                string dummyName = "Folder Selection";
                char[] fileChars = new char[260];
                dummyName.CopyTo(0, fileChars, 0, dummyName.Length);
                ofn.file = new string(fileChars);
                ofn.maxFile = fileChars.Length;

                ofn.fileTitle = new string(new char[64]);
                ofn.maxFileTitle = 64;

                ofn.title = "Navigate to your Scripts folder and click 'Open'";
                ofn.initialDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                ofn.flags = 0x00000100 | 0x00000004 | 0x00000800 | 0x00010000;

                if (GetOpenFileName(ofn))
                {
                    string selectedPath = ofn.file.TrimEnd('\0');
                    string? folderPath = System.IO.Path.GetDirectoryName(selectedPath);

                    if (!string.IsNullOrWhiteSpace(folderPath) && System.IO.Directory.Exists(folderPath))
                    {
                        if (!SavedPaths.Contains(folderPath))
                        {
                            SavedPaths.Insert(0, folderPath);
                            SettingsEngine.AllUserScriptsPaths = SavedPaths.ToList();
                        }

                        SelectedPath = folderPath;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
            }
        }

        [RelayCommand]
        private async Task RefreshScripts() => await RefreshScriptsAsync();

        [RelayCommand]
        private void ClearSelection()
        {
            foreach (var script in Scripts) script.IsSelected = false;
            OnPropertyChanged(nameof(HasSelection));
        }

        [RelayCommand]
        private void ClearPath()
        {
            lock (_locker)
            {
                Scripts.Clear();
                FilteredScripts.Clear();
            }

            SavedPaths.Clear();
            SettingsEngine.AllUserScriptsPaths = new List<string>();

            _lastKnownFolderPath = string.Empty;
            SettingsEngine.UserScriptsPath = string.Empty;

            SelectedPath = string.Empty;

            OnPropertyChanged(nameof(IsPathSet));
            OnPropertyChanged(nameof(HasSelection));
            OnScriptsUpdated?.Invoke();
        }

        [RelayCommand]
        private void ToggleSort()
        {
            IsSortDescending = !IsSortDescending;
            ApplySort();
        }

        [RelayCommand]
        private void ChangeSortMode(object parameter)
        {
            if (parameter is SortMode mode) CurrentSortMode = mode;
            else if (parameter is string modeStr && Enum.TryParse(modeStr, out SortMode parsedMode)) CurrentSortMode = parsedMode;
            ApplySort();
        }

        [RelayCommand]
        private async Task RunSingleScript(ScriptsModel script)
        {
            if (script == null) return;

            UIHelper.SetOverlay(true, false);

            var terminal = new TerminalOutputWindow();

            CenterTerminalOverMainWindow(terminal);

            RequestShowTerminal?.Invoke(terminal);
            terminal.Activate();

            await RunSingleScriptInternal(script, terminal);
            terminal.MarkAsFinished();
        }

        [RelayCommand]
        private async Task RunSelectedScripts()
        {
            var selected = Scripts.Where(s => s.IsSelected).ToList();
            if (!selected.Any()) return;

            UIHelper.SetOverlay(true, false);

            var terminal = new TerminalOutputWindow();

            CenterTerminalOverMainWindow(terminal);

            RequestShowTerminal?.Invoke(terminal);
            terminal.Activate();

            foreach (var script in selected)
            {
                await RunSingleScriptInternal(script, terminal);
            }
            terminal.MarkAsFinished();
            ClearSelection();
        }
        #endregion

        #region Private Helpers & Core Logic
        private void CenterTerminalOverMainWindow(Window terminalWindow)
        {
            var mainWin = (Application.Current as App)?.GetType().GetProperty("MainWindow")?.GetValue(Application.Current) as Window;
            if (mainWin == null) return;

            var mainHwnd = WinRT.Interop.WindowNative.GetWindowHandle(mainWin);
            var mainAppWin = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(mainHwnd));

            var termHwnd = WinRT.Interop.WindowNative.GetWindowHandle(terminalWindow);
            var termAppWin = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(termHwnd));

            if (mainAppWin != null && termAppWin != null)
            {
                var newX = mainAppWin.Position.X + (mainAppWin.Size.Width - termAppWin.Size.Width) / 2;
                var newY = mainAppWin.Position.Y + (mainAppWin.Size.Height - termAppWin.Size.Height) / 2;

                termAppWin.Move(new Windows.Graphics.PointInt32(newX, newY));
            }
        }

        private void ApplySort()
        {
            IEnumerable<ScriptsModel> query = CurrentSortMode == SortMode.Extension
                ? Scripts.OrderBy(s => Path.GetExtension(s.FilePath))
                : Scripts.OrderBy(s => s.FileName);

            if (IsSortDescending) query = query.Reverse();

            var finalSortedList = query.ToList();
            lock (_locker)
            {
                Scripts.Clear();
                foreach (var item in finalSortedList) Scripts.Add(item);
            }
            RefreshFilteredScripts();
        }

        private void RefreshFilteredScripts()
        {
            var filtered = Scripts.Where(s =>
            {
                if (string.IsNullOrWhiteSpace(SearchText)) return true;
                return s.FileName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                       s.FilePath.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
            }).ToList();

            FilteredScripts.Clear();
            foreach (var item in filtered) FilteredScripts.Add(item);

            OnScriptsUpdated?.Invoke();
        }

        private async Task RunSingleScriptInternal(ScriptsModel script, TerminalOutputWindow terminal)
        {
            terminal.AppendOutput($"[EXECUTING] {script.FileName}...");
            await Task.Run(() =>
            {
                try
                {
                    string fileName, arguments;
                    string ext = Path.GetExtension(script.FilePath).ToLowerInvariant();
                    switch (ext)
                    {
                        case ".reg": fileName = "reg.exe"; arguments = $"import \"{script.FilePath}\""; break;
                        case ".ps1": fileName = "powershell.exe"; arguments = $"-ExecutionPolicy Bypass -NoProfile -File \"{script.FilePath}\""; break;
                        default: fileName = "cmd.exe"; arguments = $"/c \"{script.FilePath}\""; break;
                    }

                    var startInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        RedirectStandardInput = true,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.GetDirectoryName(script.FilePath)
                    };

                    using var process = new Process { StartInfo = startInfo };
                    process.OutputDataReceived += (s, e) => { if (e.Data != null) terminal.AppendOutput(e.Data); };
                    process.ErrorDataReceived += (s, e) => { if (e.Data != null) terminal.AppendOutput("[ERROR] " + e.Data); };
                    process.Start();
                    process.StandardInput.Close();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                    terminal.AppendOutput($"[FINISHED] {script.FileName} with code: {process.ExitCode}\n");
                }
                catch (Exception ex) { terminal.AppendOutput($"[CRITICAL ERROR] {ex.Message}"); }
            });
        }

        public async Task RefreshScriptsAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    string path = SettingsEngine.UserScriptsPath;
                    if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;

                    var files = Directory.EnumerateFiles(path, "*.*", SearchOption.TopDirectoryOnly)
                                     .Where(f => _allowedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                                     .ToList();

                    var activeWindow = (Application.Current as App)?.GetType().GetProperty("MainWindow")?.GetValue(Application.Current) as Window;

                    activeWindow?.DispatcherQueue.TryEnqueue(() =>
                    {
                        lock (_locker)
                        {
                            if (_lastKnownFolderPath != path)
                            {
                                Scripts.Clear();
                                _lastKnownFolderPath = path;
                            }

                            var toRemove = Scripts.Where(s => !files.Contains(s.FilePath)).ToList();
                            foreach (var r in toRemove) Scripts.Remove(r);

                            foreach (string file in files.Where(f => Scripts.All(s => f != s.FilePath)))
                            {
                                string ext = Path.GetExtension(file).ToLowerInvariant();
                                string iconRes = ext switch { ".ps1" => "Img_PowershellFile", ".reg" => "Img_RegistryFile", _ => "Img_BatFile" };

                                ImageSource? icon = null;
                                if (Application.Current.Resources.TryGetValue(iconRes, out var res)) icon = res as ImageSource;
                                if (icon == null) icon = Application.Current.Resources["Img_BatFile"] as ImageSource;

                                var model = new ScriptsModel(file, Path.GetFileName(file), icon!, null!, IsRunAsTrustedInstaller);
                                model.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(ScriptsModel.IsSelected)) OnPropertyChanged(nameof(HasSelection)); };
                                Scripts.Add(model);
                            }
                        }
                        RefreshFilteredScripts();
                    });
                }
                catch (Exception ex) { ErrorLogging.LogDebug(ex); }
            });
        }
        #endregion

        #region Native Win32 File Dialog

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class OpenFileName
        {
            public int structSize = 0;
            public IntPtr hwnd = IntPtr.Zero;
            public IntPtr hinst = IntPtr.Zero;
            public string? filter = null;
            public string? custFilter = null;
            public int custFilterMax = 0;
            public int filterIndex = 0;
            public string? file = null;
            public int maxFile = 0;
            public string? fileTitle = null;
            public int maxFileTitle = 0;
            public string? initialDir = null;
            public string? title = null;
            public int flags = 0;
            public short fileOffset = 0;
            public short fileExtension = 0;
            public string? defExt = null;
            public IntPtr custData = IntPtr.Zero;
            public IntPtr hook = IntPtr.Zero;
            public string? templateName = null;
            public IntPtr reservedPtr = IntPtr.Zero;
            public int reservedInt = 0;
            public int flagsEx = 0;
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

        #endregion

        #region Disposal
        public void Dispose()
        {
            RequestShowTerminal = null;
            OnScriptsUpdated = null;

            lock (_locker)
            {
                Scripts.Clear();
                FilteredScripts.Clear();
            }
            SavedPaths.Clear();

            Debug.WriteLine("[ScriptsViewModel] Cleanly Disposed.");
        }
        #endregion
    }
}