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
    public sealed partial class PrivacyPage : Page
    {
        private PrivacyTweaks? _confTweaks = new PrivacyTweaks();

        public PrivacyPage()
        {
            this.InitializeComponent();

            if (!WindowsLicense.IsWindowsActivated)
            {
                NotificationManager.Show("info", "warn_activate_noty").Perform();
            }
        }

        private void Tweak_MouseEnter(object sender, PointerRoutedEventArgs e)
        {
            if (sender is ContentControl card)
            {
                // Glow Logic matching InterfacePage
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

                // Description logic specific to Confidentiality Keys
                string tagName = card.Tag?.ToString() ?? string.Empty;
                string resourceKey = $"{tagName.ToLower().Replace("button", "")}_desc_conf";
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
                // Prevent trigger during initialization
                if (!tgl.IsLoaded || tgl.FocusState == FocusState.Unfocused) return;

                var card = UIHelper.FindParent<ContentControl>(tgl);
                if (card != null)
                {
                    string key = card.Tag?.ToString() ?? string.Empty;
                    bool isOn = tgl.IsOn;

                    // Sync State to ViewModel
                    if (this.DataContext is PrivacyViewModel vm)
                    {
                        var model = vm[key];
                        if (model != null) model.State = isOn;
                    }

                    // Run the actual Tweak logic
                    _confTweaks?.ApplyTweaks(key, isOn);

                    // Selection Glow logic
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
            _confTweaks = null;
        }
    }
}