// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Base;
using EvolveOS_Optimizer.Core.Constants;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Core.ViewModel;

public partial class CustomizeViewModel : SectionPageViewModel<CustomizeSectionInfo>
{
    protected override string PageTitleKey => "Category_Customize_Title";
    protected override string PageDescriptionKey => "Category_Customize_StatusText";
    protected override string BreadcrumbRootFallback => "Customizations";
    protected override string LogPrefix => "CustomizeViewModel";
    protected override IReadOnlyList<CustomizeSectionInfo> SectionDefinitions => Sections;

    public static readonly IReadOnlyList<CustomizeSectionInfo> Sections = new List<CustomizeSectionInfo>()
    {
        new("Explorer", "ExplorerIconGlyph", "Explorer", FeatureIds.ExplorerCustomization),
        new("StartMenu", "StartMenuIconGlyph", "Start Menu", FeatureIds.StartMenu),
        new("Taskbar", "TaskbarIconGlyph", "Taskbar", FeatureIds.Taskbar),
        new("WindowsTheme", "WindowsThemeIconGlyph", "Windows Theme", FeatureIds.WindowsTheme),
    };

    public ISettingsFeatureViewModel ExplorerViewModel { get; }
    public ISettingsFeatureViewModel StartMenuViewModel { get; }
    public ISettingsFeatureViewModel TaskbarViewModel { get; }
    public ISettingsFeatureViewModel WindowsThemeViewModel { get; }

    public CustomizeViewModel(
        ILogService logService,
        ILocalizationService localizationService,
        IEnumerable<ICustomizationFeatureViewModel> featureViewModels)
        : base(logService, localizationService, featureViewModels.Cast<ISettingsFeatureViewModel>())
    {
        InitializeSectionMappings();

        ExplorerViewModel = GetFeatureByModuleId(FeatureIds.ExplorerCustomization);
        StartMenuViewModel = GetFeatureByModuleId(FeatureIds.StartMenu);
        TaskbarViewModel = GetFeatureByModuleId(FeatureIds.Taskbar);
        WindowsThemeViewModel = GetFeatureByModuleId(FeatureIds.WindowsTheme);
    }
}
