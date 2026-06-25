// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.ViewModel;

namespace EvolveOS_Optimizer.Core.TemplateSelectors;

public partial class SettingTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ToggleTemplate { get; set; }
    public DataTemplate? SelectionTemplate { get; set; }
    public DataTemplate? PowerPlanTemplate { get; set; }
    public DataTemplate? NumericTemplate { get; set; }
    public DataTemplate? SliderTemplate { get; set; }
    public DataTemplate? ActionTemplate { get; set; }
    public DataTemplate? ActionWithStatusTemplate { get; set; }
    public DataTemplate? CheckBoxTemplate { get; set; }
    public DataTemplate? DualSelectionTemplate { get; set; }
    public DataTemplate? SingleACSelectionTemplate { get; set; }
    public DataTemplate? DualNumericTemplate { get; set; }
    public DataTemplate? SingleACNumericTemplate { get; set; }

    public DataTemplate? PitrDiskUsageTemplate { get; set; }
    public DataTemplate? PitrSnapshotsTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        if (item is SettingItemViewModel vm)
        {
            if (vm.SettingId == "PointInTimeRestore_MaxStorage" && PitrDiskUsageTemplate != null)
            {
                return PitrDiskUsageTemplate;
            }

            if (vm.SettingId == "PointInTimeRestore_Snapshots" && PitrSnapshotsTemplate != null)
            {
                return PitrSnapshotsTemplate;
            }

            if (vm.SettingId == "gaming-performance-os-compression" && ActionWithStatusTemplate != null)
            {
                return ActionWithStatusTemplate;
            }

            if (vm.IsPowerPlanSetting && PowerPlanTemplate != null)
            {
                return PowerPlanTemplate;
            }

            if (vm.SupportsSeparateACDC)
            {
                if (vm.InputType == InputType.Selection)
                    return vm.HasBattery ? DualSelectionTemplate : SingleACSelectionTemplate;
                if (vm.InputType == InputType.NumericRange)
                    return vm.HasBattery ? DualNumericTemplate : SingleACNumericTemplate;
            }

            if (vm.IsSliderType && SliderTemplate != null)
            {
                return SliderTemplate;
            }

            return vm.InputType switch
            {
                InputType.Toggle => ToggleTemplate,
                InputType.Selection => SelectionTemplate,
                InputType.NumericRange => NumericTemplate,
                InputType.Action => ActionTemplate,
                InputType.CheckBox => CheckBoxTemplate,
                _ => ToggleTemplate
            };
        }

        return base.SelectTemplateCore(item);
    }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
    {
        return SelectTemplateCore(item);
    }
}
