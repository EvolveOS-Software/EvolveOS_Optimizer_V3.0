using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using EvolveOS_Optimizer.Core.Base;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Tweaks;
using EvolveOS_Optimizer.Utilities.Managers;

namespace EvolveOS_Optimizer.Core.ViewModel
{
    internal class PackagesViewModel : ViewModelBase, IDisposable
    {
        private Action? _dataChangedHandler;

        public ObservableCollection<PackagesModel> DisplayState { get; set; }

        public ObservableCollection<Tuple<string, string, bool>> SystemAppList { get; } = new();

        public Visibility Win11FeatureOnly => HardwareData.OS.IsWin11 ? Visibility.Visible : Visibility.Collapsed;

        public PackagesModel? this[string name] => DisplayState?.FirstOrDefault(d => d.Name == name);

        public ObservableCollection<PackagesModel> SelectedPackages { get; } = new ObservableCollection<PackagesModel>();

        private bool _IsMultiSelectMode;
        public bool IsMultiSelectMode
        {
            get => _IsMultiSelectMode;
            set
            {
                _IsMultiSelectMode = value;
                OnPropertyChanged();
                if (!value)
                {
                    ClearSelection();
                }
                OnPropertyChanged(nameof(RemoveButtonVisibility));
            }
        }

        private bool _IsRefreshing;
        public bool IsRefreshing
        {
            get => _IsRefreshing;
            set
            {
                _IsRefreshing = value;
                OnPropertyChanged();
            }
        }

        public PackagesViewModel()
        {
            DisplayState = new ObservableCollection<PackagesModel>();

            BuildCollection();

            var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

            _dataChangedHandler = () =>
            {
                if (DisplayState == null) return;

                dispatcherQueue?.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    if (DisplayState == null) return;

                    foreach (PackagesModel item in DisplayState)
                    {
                        UpdatePackageState(item);
                    }
                });
            };

            UninstallingPakages.DataChanged += _dataChangedHandler;
        }

        public async Task RefreshAllDataAsync()
        {
            IsRefreshing = true;

            var pkgManager = new UninstallingPakages();
            await Task.Run(() => pkgManager.GetInstalledPackages());

            var installedApps = await AppManager.GetInstalledApps(uninstallableOnly: true);

            SystemAppList.Clear();
            foreach (var app in installedApps)
            {
                SystemAppList.Add(app);
            }

            IsRefreshing = false;
        }

        private void BuildCollection()
        {
            if (DisplayState == null) return;
            DisplayState.Clear();

            foreach (var kv in UninstallingPakages.PackagesDetails)
            {
                string name = kv.Key;
                bool unavailableStatus = kv.Value.IsUnavailable;

                PackagesModel pkg = new PackagesModel
                {
                    Name = name,
                    IsUnavailable = !unavailableStatus
                };

                UpdatePackageState(pkg);
                DisplayState.Add(pkg);
            }
        }

        public Visibility RemoveButtonVisibility => (IsMultiSelectMode && SelectedPackages.Count > 0) ? Visibility.Visible : Visibility.Collapsed;

        public void ToggleSelection(PackagesModel model)
        {
            model.IsSelected = !model.IsSelected;
            if (model.IsSelected)
            {
                SelectedPackages.Add(model);
            }
            else
            {
                SelectedPackages.Remove(model);
            }

            OnPropertyChanged(nameof(RemoveButtonVisibility));
        }

        private void ClearSelection()
        {
            if (DisplayState == null) return;

            foreach (var pkg in DisplayState)
            {
                pkg.IsSelected = false;
            }

            SelectedPackages.Clear();
            OnPropertyChanged(nameof(RemoveButtonVisibility));
        }

        private void UpdatePackageState(PackagesModel item)
        {
            if (item == null || string.IsNullOrEmpty(item.Name)) return;

            var details = UninstallingPakages.PackagesDetails;
            if (details != null && details.TryGetValue(item.Name, out var val) && val != null)
            {
                item.IsUnavailable = !val.IsUnavailable;

                if (!string.Equals(item.Name, "OneDrive", StringComparison.OrdinalIgnoreCase))
                {
                    var scripts = val.Scripts;
                    var cache = UninstallingPakages.InstalledPackagesCache;

                    if (scripts != null && scripts.Count > 0 && cache != null)
                    {
                        item.Installed = scripts.Any(pattern =>
                            cache.Any(pkg =>
                                !string.IsNullOrEmpty(pkg) &&
                                Regex.IsMatch(pkg, $"^{Regex.Escape(pattern)}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)));
                    }
                    else
                    {
                        item.Installed = false;
                    }
                }
                else
                {
                    item.Installed = UninstallingPakages.IsOneDriveInstalled;
                }
            }
        }

        public override void Dispose()
        {
            if (_dataChangedHandler != null)
            {
                UninstallingPakages.DataChanged -= _dataChangedHandler;
                _dataChangedHandler = null;
            }

            DisplayState?.Clear();
            DisplayState = null!;

            SystemAppList?.Clear();

            SelectedPackages?.Clear();

            base.Dispose();
            Debug.WriteLine("[PackagesVM] Cleanly Disposed.");
        }
    }
}