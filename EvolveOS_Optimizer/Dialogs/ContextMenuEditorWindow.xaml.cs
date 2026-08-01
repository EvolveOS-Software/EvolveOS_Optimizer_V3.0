// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Windows.Storage.Pickers;
using System.IO;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Controls;
using WinRT.Interop;

namespace EvolveOS_Optimizer.Dialogs
{
    public sealed partial class ContextMenuEditorWindow : Window
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct OPENFILENAME
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public string lpstrFilter;
            public string lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public IntPtr lpstrFile;
            public int nMaxFile;
            public string lpstrFileTitle;
            public int nMaxFileTitle;
            public string lpstrInitialDir;
            public string lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public string lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public string lpTemplateName;
            public IntPtr pvReserved;
            public int dwReserved;
            public int flagsEx;
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GetOpenFileName(ref OPENFILENAME ofn);

        private const int GWLP_HWNDPARENT = -8;

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            else
                return SetWindowLong32(hWnd, nIndex, dwNewLong);
        }

        private ObservableCollection<ModernContextMenuItem> _modernItems = new();
        private ObservableCollection<ClassicContextMenuItem> _classicItems = new();

        public ContextMenuEditorWindow()
        {
            this.InitializeComponent();

            RootGrid.DataContext = this;

            UIHelper.SetOverlay(true, true);
            ConfigureWindow();

            RootGrid.Loaded += RootElement_Loaded;

            LoadData();
        }

        private void RootElement_Loaded(object sender, RoutedEventArgs e)
        {
            // Apply the user's global backdrop setting
            UIHelper.ApplyBackdrop(this, SettingsEngine.Backdrop);
        }

        private void ConfigureWindow()
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(wndId);

            var mainWindow = (Application.Current as App)?.GetType().GetProperty("MainWindow")?.GetValue(Application.Current) as Window;
            if (mainWindow != null)
            {
                IntPtr mainHWnd = WindowNative.GetWindowHandle(mainWindow);
                SetWindowLongPtr(hWnd, GWLP_HWNDPARENT, mainHWnd);
            }

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.SetBorderAndTitleBar(true, false);
            }

            double scale = UIHelper.GetScaleAdjustment(hWnd);

            int physicalWidth = (int)(800 * scale);
            int physicalHeight = (int)(750 * scale);

            var displayArea = DisplayArea.GetFromWindowId(wndId, DisplayAreaFallback.Primary);
            if (displayArea != null)
            {
                int x = displayArea.WorkArea.X + ((displayArea.WorkArea.Width - physicalWidth) / 2);
                int y = displayArea.WorkArea.Y + ((displayArea.WorkArea.Height - physicalHeight) / 2);

                appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, physicalWidth, physicalHeight));
            }
        }

        private void LoadData()
        {
            // Load Modern JSON
            var config = ContextMenuEngine.LoadModernItems();
            _modernItems = new ObservableCollection<ModernContextMenuItem>(config.Items);

            // 🚀 NEW: Scan the Registry for Classic Items
            var classicItemsList = ContextMenuEngine.GetClassicItems();
            _classicItems = new ObservableCollection<ClassicContextMenuItem>(classicItemsList);

            UpdateListBinding();
        }

        private void MenuTypeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateListBinding();
        }

        private void UpdateListBinding()
        {
            if (ItemsListView == null) return;

            if (MenuTypeSelector.SelectedIndex == 0) // Modern
            {
                ItemsListView.ItemsSource = _modernItems;
            }
            else // Classic
            {
                ItemsListView.ItemsSource = _classicItems;
            }
        }

        private void BrowseExe_Click(object sender, RoutedEventArgs e)
        {
            // Allocate a safe memory block for the unmanaged Win32 API to write the file path into
            IntPtr pFile = Marshal.AllocHGlobal(260 * Marshal.SystemDefaultCharSize);

            try
            {
                // Null-terminate the empty buffer
                Marshal.WriteInt16(pFile, 0);

                var ofn = new OPENFILENAME();
                ofn.lStructSize = Marshal.SizeOf(typeof(OPENFILENAME));
                ofn.hwndOwner = WinRT.Interop.WindowNative.GetWindowHandle(this);

                // Define the file filters
                ofn.lpstrFilter = "Executables (*.exe;*.bat;*.cmd)\0*.exe;*.bat;*.cmd\0All Files (*.*)\0*.*\0";

                ofn.lpstrFile = pFile;
                ofn.nMaxFile = 260;
                ofn.lpstrTitle = "Select Executable";

                // Flags: OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_NOCHANGEDIR
                ofn.Flags = 0x00080000 | 0x00001000 | 0x00000008;

                // Launch the classic Win32 dialog
                if (GetOpenFileName(ref ofn))
                {
                    ExePathInput.Text = Marshal.PtrToStringAuto(ofn.lpstrFile);
                }
            }
            finally
            {
                // Prevent memory leaks
                Marshal.FreeHGlobal(pFile);
            }
        }

        private async void AddItem_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TitleInput.Text) || string.IsNullOrWhiteSpace(ExePathInput.Text))
                return;

            string targetStr = (TargetInput.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Files";

            if (MenuTypeSelector.SelectedIndex == 0) // Modern
            {
                var newItem = new ModernContextMenuItem
                {
                    Title = TitleInput.Text,
                    ExePath = ExePathInput.Text,
                    Arguments = ArgsInput.Text,
                    Icon = ExePathInput.Text, // 🚀 FIX: Assign the EXE path as the Icon
                    Target = targetStr
                };

                _modernItems.Add(newItem);
                ContextMenuEngine.SaveModernItems(_modernItems.ToList());

                string packageFolder = Path.Combine(AppContext.BaseDirectory, "ModernMenuPackage");
                await ContextMenuEngine.RegisterSparsePackageAsync(packageFolder);

                await ContextMenuEngine.RestartExplorerAsync();
            }
            else // Classic
            {
                ContextMenuTarget targetEnum = targetStr switch
                {
                    "Folders" => ContextMenuTarget.Folders,
                    "Background" => ContextMenuTarget.Background,
                    _ => ContextMenuTarget.Files
                };

                var newItem = new ClassicContextMenuItem
                {
                    Title = TitleInput.Text,
                    ExecutablePath = ExePathInput.Text,
                    Arguments = ArgsInput.Text,
                    IconPath = ExePathInput.Text, // 🚀 FIX: Assign the EXE path as the Icon
                    Target = targetEnum
                };

                _classicItems.Add(newItem);
                ContextMenuEngine.AddClassicItem(newItem);
            }

            // Clear inputs
            TitleInput.Text = string.Empty;
            ExePathInput.Text = string.Empty;
            ArgsInput.Text = "\"%1\"";
        }

        private void RemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                if (MenuTypeSelector.SelectedIndex == 0 && btn.Tag is ModernContextMenuItem modernItem)
                {
                    _modernItems.Remove(modernItem);
                    ContextMenuEngine.SaveModernItems(_modernItems.ToList());
                }
                else if (MenuTypeSelector.SelectedIndex == 1 && btn.Tag is ClassicContextMenuItem classicItem)
                {
                    _classicItems.Remove(classicItem);
                    // 🚀 Update to pass the whole item so we have the exact KeyName
                    ContextMenuEngine.RemoveClassicItem(classicItem);
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            // Drop the background dimming overlay when window exits
            UIHelper.SetOverlay(false);
        }
    }
}