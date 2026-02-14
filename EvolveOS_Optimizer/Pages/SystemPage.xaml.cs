using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using EvolveOS_Optimizer.Utilities.Tweaks;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class SystemPage : Page
    {
        private readonly SystemTweaks _sysTweaks = new SystemTweaks();

        public SystemPage()
        {
            this.InitializeComponent();

            //this.Loaded += (s, e) => DebugAvailableCards();
        }

        #region Interaction & Hover Logic

        private void Tweak_MouseEnter(object sender, PointerRoutedEventArgs e)
        {
            if (sender is ContentControl card)
            {
                if (SettingsEngine.IsHoverGlowEnabled)
                {
                    VisualStateManager.GoToState(card, "PointerOver", true);
                }
                else
                {
                    VisualStateManager.GoToState(card, "Normal", true);
                }

                string tagName = card.Tag?.ToString() ?? string.Empty;
                string resourceKey = (tagName == "SliderGroup")
                    ? "slider_desc_sys"
                    : $"{tagName.ToLower().Replace("button", "")}_desc_sys";

                try
                {
                    string description = ResourceString.GetString(resourceKey);
                    if (!string.IsNullOrEmpty(description) && DescBlock != null)
                    {
                        DescBlock.Text = description;
                    }
                }
                catch { }
            }
        }

        private void Tweak_MouseLeave(object sender, PointerRoutedEventArgs e)
        {
            if (sender is ContentControl card)
            {
                VisualStateManager.GoToState(card, "Normal", true);
            }

            if (DescBlock != null)
            {
                DescBlock.Text = ResourceString.GetString("defaultDescriptionApp");
            }
        }

        #endregion

        #region Toggles & Sliders Logic

        private void NativeTgl_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch tgl)
            {
                if (!tgl.IsLoaded || tgl.FocusState == Microsoft.UI.Xaml.FocusState.Unfocused) return;

                if (tgl.DataContext is SystemModel model)
                {
                    string key = model.Name;
                    bool isOn = tgl.IsOn;

                    model.State = isOn;

                    _sysTweaks.ApplyTweaks(key, isOn);

                    if (NotificationManager.SysActions.TryGetValue(key, out var action))
                    {
                        NotificationManager.Show().WithDuration(300).Perform(action);
                    }

                    var card = UIHelper.FindParent<ContentControl>(tgl);
                    if (card != null && SettingsEngine.IsSelectionGlowEnabled)
                    {
                        VisualStateManager.GoToState(card, isOn ? "Selected" : "Unselected", true);
                    }
                }
            }
        }

        private void Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (sender is Slider slider)
            {
                _sysTweaks.ApplyTweaksSlider(slider.Name, (uint)slider.Value);
            }
        }

        #endregion

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {

        }

        private void DebugAvailableCards()
        {
            var allButtonKeys = Enumerable.Range(1, 32).Select(i => $"TglButton{i}").ToList();

            var existingCards = UIHelper.FindVisualChildren<ContentControl>(this)
                                .Where(c => c.Tag?.ToString()?.StartsWith("TglButton") == true)
                                .ToList();

            Debug.WriteLine("--- SYSTEM PAGE DIAGNOSTICS ---");

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