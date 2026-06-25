// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.TemplateSelectors;

public class BuilderCategoryHeader
{
    public string DisplayName { get; }
    public BuilderCategoryHeader(string displayName)
    {
        DisplayName = displayName;
    }
}

public class BuilderNavItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate? HeaderTemplate { get; set; }
    public DataTemplate? ItemTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        if (item is BuilderCategoryHeader) return HeaderTemplate;

        return ItemTemplate;
    }
}
