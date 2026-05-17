// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Utilities.Converters
{
    public sealed partial class BadgeIconTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? DefaultTemplate { get; set; }
        public DataTemplate? CustomTemplate { get; set; }
        public DataTemplate? RecommendedTemplate { get; set; }

        public static T? PickByState<T>(
            bool isCustomized,
            T? @default,
            T? custom)
            where T : class
            => isCustomized ? custom : @default;

        protected override DataTemplate? SelectTemplateCore(object item)
        {
            if (item is bool isCustomized)
            {
                return PickByState(isCustomized, DefaultTemplate, CustomTemplate);
            }

            return DefaultTemplate;
        }

        protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
            => SelectTemplateCore(item);
    }
}