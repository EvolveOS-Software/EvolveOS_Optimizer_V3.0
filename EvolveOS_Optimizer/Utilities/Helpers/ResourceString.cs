using EvolveOS_Optimizer.Utilities.Services;
using Microsoft.UI.Xaml.Markup;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    [MarkupExtensionReturnType(ReturnType = typeof(string))]
    public sealed class ResourceString : MarkupExtension
    {
        public string Key { get; set; } = string.Empty;

        protected override object ProvideValue()
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

            var result = LocalizationService.Instance[key];
            if (!string.IsNullOrEmpty(result) && !result.StartsWith("["))
            {
                return result;
            }

            if (Application.Current != null)
            {
                var resources = Application.Current.Resources;

                if (resources.TryGetValue(key, out object? val))
                {
                    return val?.ToString() ?? $"[{key}]";
                }

                foreach (var dict in resources.MergedDictionaries)
                {
                    if (dict.TryGetValue(key, out object? mergedVal))
                    {
                        return mergedVal?.ToString() ?? $"[{key}]";
                    }
                }
            }

            try
            {
                string altKey = key.Replace('.', '_').Replace('/', '_');
                if (altKey != key)
                {
                    var altResult = GetString(altKey);
                    if (!altResult.StartsWith("[")) return altResult;
                }
            }
            catch { }

            return $"[{key}]";
        }
    }
}