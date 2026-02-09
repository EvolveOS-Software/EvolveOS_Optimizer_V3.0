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

            if (!string.IsNullOrEmpty(result))
            {
                return result;
            }

            try
            {
                string altKey = string.Empty;

                if (key.Contains('.'))
                    altKey = key.Replace('.', '_');
                else if (key.Contains('/'))
                    altKey = key.Replace('/', '_');

                if (!string.IsNullOrEmpty(altKey))
                {
                    var altResult = LocalizationService.Instance[altKey];
                    if (!string.IsNullOrEmpty(altResult)) return altResult;
                }
            }
            catch
            {
                // Fallback to key if logic fails
            }

            return $"[{key}]";
        }
    }
}