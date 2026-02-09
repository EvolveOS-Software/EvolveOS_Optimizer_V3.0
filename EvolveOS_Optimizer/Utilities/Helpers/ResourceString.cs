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
            return GetString(Key);
        }

        public static string GetString(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            return LocalizationService.Instance[key] ?? key;
        }
    }
}