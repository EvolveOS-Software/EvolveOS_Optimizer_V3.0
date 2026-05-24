// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Data;

namespace EvolveOS_Optimizer.Utilities.Converters
{
    public partial class BoolToGlyphConverter : IValueConverter
    {
        public string? ExpandedGlyph { get; set; }
        public string? CollapsedGlyph { get; set; }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var isExpanded = value as bool?;

            if (isExpanded.HasValue && isExpanded.Value)
            {
                return ExpandedGlyph ?? string.Empty;
            }
            else
            {
                return CollapsedGlyph ?? string.Empty;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
