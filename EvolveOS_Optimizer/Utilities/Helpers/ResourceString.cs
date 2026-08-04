// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Reflection;
using Microsoft.UI.Xaml.Markup;
using EvolveOS_Optimizer.Utilities.Services;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    [MarkupExtensionReturnType(ReturnType = typeof(string))]
    public sealed class ResourceString : MarkupExtension
    {
        public string Key { get; set; } = string.Empty;

        protected override object ProvideValue()
        {
            return GetValue();
        }

        protected override object ProvideValue(IXamlServiceProvider serviceProvider)
        {
            if (serviceProvider?.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget provideValueTarget)
            {
                if (provideValueTarget.TargetObject is TextBlock textBlock)
                {
                    bool isTextProperty = false;

                    if (provideValueTarget.TargetProperty is DependencyProperty dp && dp == TextBlock.TextProperty)
                    {
                        isTextProperty = true;
                    }
                    else if (provideValueTarget.TargetProperty is PropertyInfo pi && pi.Name == "Text")
                    {
                        isTextProperty = true;
                    }

                    if (isTextProperty)
                    {
                        Loc.SetKey(textBlock, Key);
                    }
                }
            }

            return GetValue();
        }

        private string GetValue()
        {
            if (Windows.ApplicationModel.DesignMode.DesignModeEnabled)
            {
                return Key;
            }

            return GetString(Key);
        }

        public static string GetString(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            return LocalizationService.Instance.GetString(key);
        }

        public static string GetString(string key, params object[] args)
        {
            string localizedString = GetString(key);

            if (string.IsNullOrEmpty(localizedString))
                return string.Empty;

            try
            {
                return string.Format(localizedString, args);
            }
            catch
            {
                return localizedString;
            }
        }
    }
}