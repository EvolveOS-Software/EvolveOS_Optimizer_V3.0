using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using EvolveOS_Optimizer.Utilities.Tweaks;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using System.Numerics;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class ServicesPage : Page
    {
        private readonly ServicesTweaks _svcTweaks = new ServicesTweaks();

        public ServicesPage()
        {
            this.InitializeComponent();
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
                string resourceKey = $"{tagName.ToLower().Replace("button", "")}_desc_svc";
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

            if (DescBlock != null)
            {
                DescBlock.Text = ResourceString.GetString("defaultDescription");
            }
        }

        private void NativeTgl_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch tgl)
            {
                var card = UIHelper.FindParent<ContentControl>(tgl);
                if (card != null && SettingsEngine.IsSelectionGlowEnabled)
                {
                    VisualStateManager.GoToState(card, tgl.IsOn ? "Selected" : "Unselected", true);
                }

                string key = card?.Tag?.ToString() ?? string.Empty;
                bool isOn = tgl.IsOn;

                _svcTweaks.ApplyTweaks(key, isOn);

                if (NotificationManager.SysActions.TryGetValue(key, out var action))
                {
                    NotificationManager.Show().WithDuration(300).Perform(action);
                }
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
        }
    }
}