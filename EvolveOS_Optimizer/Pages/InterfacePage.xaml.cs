using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Maintenance;
using EvolveOS_Optimizer.Utilities.Managers;
using EvolveOS_Optimizer.Utilities.Tweaks;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using System.Numerics;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class InterfacePage : Page
    {
        private InterfaceTweaks? _intfTweaks = new InterfaceTweaks();

        public InterfacePage()
        {
            this.InitializeComponent();

            //this.Loaded += (s, e) => DebugAvailableCards();

            if (!WindowsLicense.IsWindowsActivated)
            {
                NotificationManager.Show("info", "warn_activate_noty").Perform();
            }
        }

        private void Tweak_MouseEnter(object sender, PointerRoutedEventArgs e)
        {
            if (sender is ContentControl card)
            {
                VisualStateManager.GoToState(card, SettingsEngine.IsHoverGlowEnabled ? "PointerOver" : "Normal", true);

                if (SettingsEngine.IsHoverGlowEnabled)
                {
                    try
                    {
                        var backgroundBorder = UIHelper.FindVisualChildByName<Border>(card, "CardBackground");
                        if (backgroundBorder != null)
                        {
                            var visual = ElementCompositionPreview.GetElementVisual(backgroundBorder);
                            visual.CenterPoint = new Vector3((float)backgroundBorder.ActualWidth / 2, (float)backgroundBorder.ActualHeight / 2, 0);

                            var compositor = visual.Compositor;
                            var anim = compositor.CreateVector3KeyFrameAnimation();
                            anim.InsertKeyFrame(1f, new Vector3(1.01f, 1.01f, 1f));
                            anim.Duration = TimeSpan.FromMilliseconds(200);
                            visual.StartAnimation("Scale", anim);
                        }
                    }
                    catch { }
                }

                string tagName = card.Tag?.ToString() ?? string.Empty;
                string resourceKey = $"{tagName.ToLower().Replace("button", "")}_desc_intf";
                try
                {
                    DescBlock.Text = ResourceString.GetString(resourceKey);
                }
                catch { }
            }
        }

        private void Tweak_MouseLeave(object sender, PointerRoutedEventArgs e)
        {
            if (sender is ContentControl card)
            {
                try
                {
                    var backgroundBorder = UIHelper.FindVisualChildByName<Border>(card, "CardBackground");
                    if (backgroundBorder != null)
                    {
                        var visual = ElementCompositionPreview.GetElementVisual(backgroundBorder);
                        var anim = visual.Compositor.CreateVector3KeyFrameAnimation();
                        anim.InsertKeyFrame(1f, new Vector3(1.0f, 1.0f, 1f));
                        anim.Duration = TimeSpan.FromMilliseconds(200);
                        visual.StartAnimation("Scale", anim);
                    }
                }
                catch { }

                VisualStateManager.GoToState(card, "Normal", true);
            }
            DescBlock.Text = ResourceString.GetString("defaultDescription");
        }

        private void NativeTgl_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch tgl)
            {
                if (!tgl.IsLoaded || tgl.FocusState == FocusState.Unfocused) return;

                var card = UIHelper.FindParent<ContentControl>(tgl);
                if (card != null)
                {
                    string key = card.Tag?.ToString() ?? string.Empty;
                    bool isOn = tgl.IsOn;

                    if (this.DataContext is InterfaceViewModel vm)
                    {
                        var model = vm[key];
                        if (model != null)
                        {
                            model.State = isOn;
                        }
                    }

                    _intfTweaks?.ApplyTweaks(key, isOn);

                    if (ExplorerManager.IntfMapping.TryGetValue(key, out bool needRestart) && needRestart)
                    {
                        ExplorerManager.Restart();
                    }

                    if (SettingsEngine.IsSelectionGlowEnabled)
                    {
                        VisualStateManager.GoToState(card, isOn ? "Selected" : "Unselected", true);
                    }
                }
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is IDisposable disposableVM)
            {
                disposableVM.Dispose();
            }
            this.DataContext = null;
            _intfTweaks = null;
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {

        }

        private void DebugAvailableCards()
        {
            var allButtonKeys = Enumerable.Range(1, 40).Select(i => $"TglButton{i}").ToList();

            var existingCards = UIHelper.FindVisualChildren<ContentControl>(this)
                                .Where(c => c.Tag?.ToString()?.StartsWith("TglButton") == true)
                                .ToList();

            Debug.WriteLine("--- INTERFACE PAGE DIAGNOSTICS ---");

            foreach (var key in allButtonKeys)
            {
                var card = existingCards.FirstOrDefault(c => string.Equals(c.Tag?.ToString(), key, StringComparison.Ordinal));

                if (card == null)
                {
                    Debug.WriteLine($"[MISSING] {key}: Card is not in the XAML at all.");
                }
                else if (card.Visibility == Visibility.Collapsed)
                {
                    Debug.WriteLine($"[HIDDEN] {key}: Card exists but is hidden by Win11/Build logic.");
                }
                else
                {
                    Debug.WriteLine($"[OK] {key}: Visible.");
                }
            }

            int visibleCount = existingCards.Count(c => c.Visibility == Visibility.Visible);
            Debug.WriteLine($"Total Cards Visible: {visibleCount}");
            Debug.WriteLine("----------------------------------");
        }
    }
}