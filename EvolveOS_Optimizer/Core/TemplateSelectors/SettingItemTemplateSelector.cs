// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.ViewModel;

namespace EvolveOS_Optimizer.Core.TemplateSelectors;

public partial class SettingItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate? RegularTemplate { get; set; }
    public DataTemplate? ExpanderTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        if (item is SettingItemViewModel vm && vm.IsParentSetting)
            return ExpanderTemplate;
        return RegularTemplate;
    }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        => SelectTemplateCore(item);
}
