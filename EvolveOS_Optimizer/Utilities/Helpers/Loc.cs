// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Services;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class Loc
    {
        private static readonly HashSet<FrameworkElement> _trackedElements = new();

        public static readonly DependencyProperty KeyProperty =
            DependencyProperty.RegisterAttached(
                "Key",
                typeof(string),
                typeof(Loc),
                new PropertyMetadata(string.Empty, OnKeyChanged));

        public static string GetKey(DependencyObject obj) => (string)obj.GetValue(KeyProperty);
        public static void SetKey(DependencyObject obj, string value) => obj.SetValue(KeyProperty, value);

        private static void OnKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element)
            {
                string key = (string)e.NewValue;
                if (string.IsNullOrEmpty(key)) return;

                ApplyLocalization(element, key);

                _trackedElements.Add(element);

                element.Unloaded -= Element_Unloaded;
                element.Unloaded += Element_Unloaded;
            }
        }

        private static void Element_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                _trackedElements.Remove(element);
            }
        }

        public static void RefreshAll()
        {
            foreach (var element in _trackedElements.ToList())
            {
                string key = GetKey(element);
                if (!string.IsNullOrEmpty(key))
                {
                    ApplyLocalization(element, key);
                }
            }
        }

        private static void ApplyLocalization(FrameworkElement element, string key)
        {
            string text = key;
            StringStatus status = StringStatus.Missing;

            if (!Windows.ApplicationModel.DesignMode.DesignModeEnabled)
            {
                var result = LocalizationService.Instance.GetWithStatus(key);
                text = result.Value;
                status = result.Status;
            }

            Brush? colorBrush = null;

            if (LocalMachineSettingsEngine.IsDeveloperMode)
            {
                colorBrush = status switch
                {
                    StringStatus.Fallback => new SolidColorBrush(Colors.Orange),
                    StringStatus.Missing => new SolidColorBrush(Colors.Red),
                    _ => null
                };
            }

            if (element is TextBlock textBlock)
            {
                textBlock.Text = text;
                if (colorBrush != null) textBlock.Foreground = colorBrush;
                else textBlock.ClearValue(TextBlock.ForegroundProperty);
            }
            else if (element is ContentControl contentControl)
            {
                contentControl.Content = text;
                if (colorBrush != null) contentControl.Foreground = colorBrush;
                else contentControl.ClearValue(Control.ForegroundProperty);
            }
        }

        public static List<(string Key, StringStatus Status)> GetMissingStringsReport()
        {
            var missingKeys = new HashSet<(string Key, StringStatus Status)>();

            foreach (var element in _trackedElements.ToList())
            {
                string key = GetKey(element);
                if (string.IsNullOrEmpty(key)) continue;

                var result = LocalizationService.Instance.GetWithStatus(key);

                if (result.Status == StringStatus.Fallback || result.Status == StringStatus.Missing)
                {
                    missingKeys.Add((key, result.Status));
                }
            }

            return missingKeys.ToList();
        }
    }
}