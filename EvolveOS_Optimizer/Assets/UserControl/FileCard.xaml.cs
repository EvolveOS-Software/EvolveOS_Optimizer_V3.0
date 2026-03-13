using System.Windows.Input;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Pages;
using EvolveOS_Optimizer.Utilities.Helpers;
using Microsoft.UI.Xaml.Input;

namespace EvolveOS_Optimizer.Assets.UserControl
{
    public sealed partial class FileCard : Microsoft.UI.Xaml.Controls.UserControl
    {
        public static readonly DependencyProperty FileNameProperty = DependencyProperty.Register(nameof(FileName), typeof(string), typeof(FileCard), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty IconSourceProperty = DependencyProperty.Register(nameof(IconSource), typeof(Microsoft.UI.Xaml.Media.ImageSource), typeof(FileCard), new PropertyMetadata(null));
        public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(FileCard), new PropertyMetadata(null));
        public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(FileCard), new PropertyMetadata(null));

        public string FileName { get => (string)GetValue(FileNameProperty); set => SetValue(FileNameProperty, value); }
        public Microsoft.UI.Xaml.Media.ImageSource IconSource { get => (Microsoft.UI.Xaml.Media.ImageSource)GetValue(IconSourceProperty); set => SetValue(IconSourceProperty, value); }
        public ICommand Command { get => (ICommand)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
        public object CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

        private ScriptsModel? _boundScript;

        public FileCard()
        {
            this.InitializeComponent();
            this.DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (_boundScript != null)
            {
                _boundScript.PropertyChanged -= Script_PropertyChanged;
            }

            if (this.DataContext is ScriptsModel script)
            {
                _boundScript = script;
                _boundScript.PropertyChanged += Script_PropertyChanged;
                UpdateSelectionState();
            }
        }

        private void Script_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ScriptsModel.IsSelected))
            {
                this.DispatcherQueue.TryEnqueue(() => UpdateSelectionState());
            }
        }

        private void UpdateSelectionState()
        {
            if (_boundScript != null)
            {
                VisualStateManager.GoToState(this, _boundScript.IsSelected ? "Selected" : "NotSelected", true);
            }
        }

        private void RootGrid_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            VisualStateManager.GoToState(this, "PointerOver", true);
        }

        private void RootGrid_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            VisualStateManager.GoToState(this, "Normal", true);
        }

        private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var scriptsPage = UIHelper.FindParent<ScriptsPage>(this);
            var viewModel = scriptsPage?.DataContext as ScriptsViewModel;

            if (viewModel == null || _boundScript == null) return;

            if (viewModel.IsMultiSelectMode)
            {
                _boundScript.IsSelected = !_boundScript.IsSelected;
            }
            else
            {
                if (Command != null && Command.CanExecute(CommandParameter))
                {
                    Command.Execute(CommandParameter);
                }
            }
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (_boundScript != null && !string.IsNullOrEmpty(_boundScript.FilePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_boundScript.FilePath}\"") { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Explorer error: {ex.Message}");
                }
            }
        }
    }
}