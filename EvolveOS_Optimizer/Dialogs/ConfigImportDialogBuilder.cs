// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Automation;
using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.Constants;
using EvolveOS_Optimizer.Utilities.Helpers;
using FluentIcons.WinUI;
using FluentIcons.Common;
using Microsoft.UI.Text;

namespace EvolveOS_Optimizer.Dialogs;

internal class ConfigImportDialogBuilder
{
    private readonly ILocalizationService _localization;

    private ImportOption? _selectedOption;
    private Button? _selectedCardButton;
    private Border? _selectedBgBorder;
    private Border? _selectedAccentBorder;

    private ContentDialog _dialog = null!;
    private CheckBox _skipReviewCheckbox = null!;
    private RadioButton _winAppsInstallRadio = null!;
    private RadioButton _winAppsUninstallRadio = null!;
    private RadioButton _winAppsSelectOnlyRadio = null!;
    private RadioButton _extAppsInstallRadio = null!;
    private RadioButton _extAppsUninstallRadio = null!;
    private RadioButton _extAppsSelectOnlyRadio = null!;
    private CheckBox _themeWallpaperCheckbox = null!;
    private CheckBox _cleanTaskbarCheckbox = null!;
    private CheckBox _cleanStartMenuCheckbox = null!;

    public ConfigImportDialogBuilder(ILocalizationService localization)
    {
        _localization = localization;
    }

    public ContentDialog Build(XamlRoot xamlRoot)
    {
        bool isDark = (xamlRoot.Content as FrameworkElement)?.ActualTheme == ElementTheme.Dark;

        _dialog = new ContentDialog
        {
            Title = _localization.GetString("Dialog_ImportConfig_Title"),
            PrimaryButtonText = StringKeys.Localized.Button_Continue,
            IsPrimaryButtonEnabled = false,
            CloseButtonText = StringKeys.Localized.Button_Cancel,
            DefaultButton = ContentDialogButton.None,
            MinWidth = 500
        };

        var ownIcon = new FluentIcon { Icon = Icon.FolderOpen, IconVariant = IconVariant.Regular, FontSize = 24, VerticalAlignment = VerticalAlignment.Center };
        var ownCard = CreateOptionCard(ownIcon,
            "Dialog_ImportConfig_Option_Own_Title",
            "Dialog_ImportConfig_Option_Own_Description",
            ImportOption.ImportOwn, isDark);

        var logoUri = isDark
            ? "ms-appx:///Resources/EvolveOSLogo.png"
            : "ms-appx:///Resources/EvolveOSLogo.png";
        var recIcon = new Image
        {
            Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new System.Uri(logoUri)),
            Width = 24,
            Height = 24,
            VerticalAlignment = VerticalAlignment.Center
        };
        var recCard = CreateOptionCard(recIcon,
            "Dialog_ImportConfig_Option_Recommended_Title",
            "Dialog_ImportConfig_Option_Recommended_Description",
            ImportOption.ImportRecommended, isDark);

        var backupIcon = new FluentIcon { Icon = Icon.History, IconVariant = IconVariant.Regular, FontSize = 24, VerticalAlignment = VerticalAlignment.Center };
        var backupCard = CreateOptionCard(backupIcon,
            "Dialog_ImportConfig_Option_Backup_Title",
            "Dialog_ImportConfig_Option_Backup_Description",
            ImportOption.ImportBackup, isDark);

        var defaultsIcon = new FluentIcon { Icon = Icon.ArrowReset, IconVariant = IconVariant.Regular, FontSize = 24, VerticalAlignment = VerticalAlignment.Center };
        var defaultsCard = CreateOptionCard(defaultsIcon,
            "Dialog_ImportConfig_Option_Defaults_Title",
            "Dialog_ImportConfig_Option_Defaults_Description",
            ImportOption.ImportWindowsDefaults, isDark, isLast: true);

        var skipReviewText = _localization.GetString("Review_Mode_Skip_Checkbox") ?? "Skip review and apply immediately";
        _skipReviewCheckbox = new CheckBox
        {
            Content = skipReviewText,
            IsChecked = false,
            Margin = new Thickness(0, 12, 0, 0)
        };

        var (optionsPanel, importControls) = BuildImportOptionsGrid(skipReviewText);
        _winAppsInstallRadio = importControls.WinAppsInstall;
        _winAppsUninstallRadio = importControls.WinAppsUninstall;
        _winAppsSelectOnlyRadio = importControls.WinAppsSelectOnly;
        _extAppsInstallRadio = importControls.ExtAppsInstall;
        _extAppsUninstallRadio = importControls.ExtAppsUninstall;
        _extAppsSelectOnlyRadio = importControls.ExtAppsSelectOnly;
        _themeWallpaperCheckbox = importControls.ThemeWallpaper;
        _cleanTaskbarCheckbox = importControls.CleanTaskbar;
        _cleanStartMenuCheckbox = importControls.CleanStartMenu;

        _skipReviewCheckbox.Checked += (_, _) =>
        {
            optionsPanel.Opacity = 1.0;
            SetImportControlsEnabled(true);
            DialogAccessibilityHelper.AnnounceToNarrator(_skipReviewCheckbox, $"{skipReviewText}: {_localization.GetString("Accessibility_Checked") ?? "Checked"}");
        };
        _skipReviewCheckbox.Unchecked += (_, _) =>
        {
            optionsPanel.Opacity = 0.4;
            SetImportControlsEnabled(false);
            DialogAccessibilityHelper.AnnounceToNarrator(_skipReviewCheckbox, $"{skipReviewText}: {_localization.GetString("Accessibility_Unchecked") ?? "Unchecked"}");
        };

        var contentPanel = new StackPanel { Spacing = 0, Margin = new Thickness(0, 0, 14, 0) };
        contentPanel.Children.Add(new TextBlock
        {
            Text = _localization.GetString("Dialog_ImportOptions_Message"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });
        contentPanel.Children.Add(ownCard);
        contentPanel.Children.Add(recCard);
        contentPanel.Children.Add(backupCard);
        contentPanel.Children.Add(defaultsCard);
        contentPanel.Children.Add(_skipReviewCheckbox);
        contentPanel.Children.Add(optionsPanel);

        var scrollViewer = new ScrollViewer
        {
            Content = contentPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 480
        };

        _dialog.Content = scrollViewer;
        return _dialog;
    }

    public (ImportOption? Option, ImportOptions Options) ExtractResult(ContentDialogResult dialogResult)
    {
        if (dialogResult != ContentDialogResult.Primary)
        {
            _selectedOption = null;
        }

        bool skipReview = _skipReviewCheckbox.IsChecked == true;
        var importOptions = new ImportOptions
        {
            ReviewBeforeApplying = !skipReview,
            ProcessWindowsAppsInstallation = skipReview && _winAppsInstallRadio.IsChecked == true,
            ProcessWindowsAppsRemoval = skipReview && _winAppsUninstallRadio.IsChecked == true,
            ProcessExternalAppsInstallation = skipReview && _extAppsInstallRadio.IsChecked == true,
            ProcessExternalAppsRemoval = skipReview && _extAppsUninstallRadio.IsChecked == true,
            ApplyThemeWallpaper = skipReview && _themeWallpaperCheckbox.IsChecked == true,
            ApplyCleanTaskbar = skipReview && _cleanTaskbarCheckbox.IsChecked == true,
            ApplyCleanStartMenu = skipReview && _cleanStartMenuCheckbox.IsChecked == true,
        };
        return (_selectedOption, importOptions);
    }

    private void SetImportControlsEnabled(bool enabled)
    {
        _winAppsInstallRadio.IsEnabled = enabled;
        _winAppsUninstallRadio.IsEnabled = enabled;
        _winAppsSelectOnlyRadio.IsEnabled = enabled;
        _extAppsInstallRadio.IsEnabled = enabled;
        _extAppsUninstallRadio.IsEnabled = enabled;
        _extAppsSelectOnlyRadio.IsEnabled = enabled;
        _themeWallpaperCheckbox.IsEnabled = enabled;
        _cleanTaskbarCheckbox.IsEnabled = enabled;
        _cleanStartMenuCheckbox.IsEnabled = enabled;
    }

    private Button CreateOptionCard(UIElement icon, string titleKey, string descKey, ImportOption option, bool isDark, bool isLast = false)
    {
        var titleText = _localization.GetString(titleKey);
        var descText = _localization.GetString(descKey);

        var textPanel = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        textPanel.Children.Add(new TextBlock
        {
            Text = titleText,
            FontWeight = FontWeights.SemiBold,
            FontSize = 14
        });
        textPanel.Children.Add(new TextBlock
        {
            Text = descText,
            FontSize = 12,
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap
        });

        var contentPanel2 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, Margin = new Thickness(12, 8, 12, 8) };
        contentPanel2.Children.Add(icon);
        contentPanel2.Children.Add(textPanel);

        var bgBorder = new Border
        {
            Background = new SolidColorBrush(Colors.Transparent),
            BorderBrush = new SolidColorBrush(isDark ? Colors.White : Colors.Black),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4)
        };

        var accentBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(4)
        };

        var cardVisual = new Grid();
        cardVisual.Children.Add(bgBorder);
        cardVisual.Children.Add(accentBorder);
        cardVisual.Children.Add(contentPanel2);

        var cardButton = new Button
        {
            Content = cardVisual,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Margin = new Thickness(0, 0, 0, isLast ? 0 : 6)
        };
        AutomationProperties.SetName(cardButton, $"{titleText}. {descText}");

        cardButton.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(Colors.Transparent);
        cardButton.Resources["ButtonBackgroundPressed"] = new SolidColorBrush(Colors.Transparent);
        cardButton.Resources["ButtonBorderBrushPointerOver"] = new SolidColorBrush(Colors.Transparent);
        cardButton.Resources["ButtonBorderBrushPressed"] = new SolidColorBrush(Colors.Transparent);

        cardButton.Click += (_, _) =>
        {
            if (_selectedCardButton == cardButton) return;

            if (_selectedBgBorder != null)
                _selectedBgBorder.Background = new SolidColorBrush(Colors.Transparent);
            if (_selectedAccentBorder != null)
                _selectedAccentBorder.BorderBrush = new SolidColorBrush(Colors.Transparent);

            bgBorder.Background = (Brush)Application.Current.Resources["SubtleFillColorTertiaryBrush"];
            accentBorder.BorderBrush = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];

            _selectedCardButton = cardButton;
            _selectedBgBorder = bgBorder;
            _selectedAccentBorder = accentBorder;
            _selectedOption = option;
            _dialog.IsPrimaryButtonEnabled = true;

            DialogAccessibilityHelper.AnnounceToNarrator(
                cardButton,
                $"{_localization.GetString("Accessibility_Selected") ?? "Selected"}: {titleText}",
                "ConfigOptionSelected");
        };

        cardButton.PointerEntered += (_, _) =>
        {
            if (_selectedCardButton != cardButton)
                bgBorder.Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
        };

        cardButton.PointerExited += (_, _) =>
        {
            if (_selectedCardButton != cardButton)
                bgBorder.Background = new SolidColorBrush(Colors.Transparent);
        };

        cardButton.GotFocus += (_, _) =>
        {
            if (_selectedCardButton != cardButton)
                bgBorder.Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
        };
        cardButton.LostFocus += (_, _) =>
        {
            if (_selectedCardButton != cardButton)
                bgBorder.Background = new SolidColorBrush(Colors.Transparent);
        };

        return cardButton;
    }

    private record ImportOptionControls(
        RadioButton WinAppsInstall,
        RadioButton WinAppsUninstall,
        RadioButton WinAppsSelectOnly,
        RadioButton ExtAppsInstall,
        RadioButton ExtAppsUninstall,
        RadioButton ExtAppsSelectOnly,
        CheckBox ThemeWallpaper,
        CheckBox CleanTaskbar,
        CheckBox CleanStartMenu);

    private (StackPanel Panel, ImportOptionControls Controls) BuildImportOptionsGrid(string skipReviewText)
    {
        var appsGrid = new Grid
        {
            Margin = new Thickness(0, 3, 0, 0),
            RowSpacing = 0,
            ColumnSpacing = 2
        };
        appsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        appsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        appsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        appsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        appsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        appsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var winAppsText = _localization.GetString("Config_Import_Options_WindowsApps") ?? "Windows Apps";
        var extAppsText = _localization.GetString("Config_Import_Options_ExternalApps") ?? "External Apps";
        var installText = _localization.GetString("Config_Import_Options_Install") ?? "Install";
        var uninstallText = _localization.GetString("Config_Import_Options_Uninstall") ?? "Uninstall";
        var selectOnlyText = _localization.GetString("Config_Import_Options_SelectOnly") ?? "Select Only";

        var winAppsLabel = new TextBlock
        {
            Text = winAppsText,
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(winAppsLabel, 0);
        Grid.SetColumn(winAppsLabel, 0);

        var winAppsInstallRadio = CreateRadioButton(installText, "WindowsApps", false, winAppsText, installText, 0, 1);
        var winAppsUninstallRadio = CreateRadioButton(uninstallText, "WindowsApps", true, winAppsText, uninstallText, 0, 2);
        var winAppsSelectOnlyRadio = CreateRadioButton(selectOnlyText, "WindowsApps", false, winAppsText, selectOnlyText, 0, 3);

        var extAppsLabel = new TextBlock
        {
            Text = extAppsText,
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(extAppsLabel, 1);
        Grid.SetColumn(extAppsLabel, 0);

        var extAppsInstallRadio = CreateRadioButton(installText, "ExternalApps", true, extAppsText, installText, 1, 1);
        var extAppsUninstallRadio = CreateRadioButton(uninstallText, "ExternalApps", false, extAppsText, uninstallText, 1, 2);
        var extAppsSelectOnlyRadio = CreateRadioButton(selectOnlyText, "ExternalApps", false, extAppsText, selectOnlyText, 1, 3);

        appsGrid.Children.Add(winAppsLabel);
        appsGrid.Children.Add(winAppsInstallRadio);
        appsGrid.Children.Add(winAppsUninstallRadio);
        appsGrid.Children.Add(winAppsSelectOnlyRadio);
        appsGrid.Children.Add(extAppsLabel);
        appsGrid.Children.Add(extAppsInstallRadio);
        appsGrid.Children.Add(extAppsUninstallRadio);
        appsGrid.Children.Add(extAppsSelectOnlyRadio);

        var themeWallpaperCheckbox = CreateAccessibleCheckBox(
            _localization.GetString("Config_Import_Options_ThemeWallpaper") ?? "Apply default wallpaper for theme",
            isChecked: true, margin: new Thickness(0, 2, 0, 0));

        var cleanTaskbarCheckbox = CreateAccessibleCheckBox(
            _localization.GetString("Config_Import_Options_CleanTaskbar") ?? "Clean Taskbar",
            isChecked: true);

        var cleanStartMenuCheckbox = CreateAccessibleCheckBox(
            _localization.GetString("Config_Import_Options_CleanStartMenu") ?? "Clean Start Menu",
            isChecked: true);

        var optionsPanel = new StackPanel
        {
            Spacing = 0,
            Opacity = 0.4
        };
        optionsPanel.Children.Add(appsGrid);
        optionsPanel.Children.Add(themeWallpaperCheckbox);
        optionsPanel.Children.Add(cleanTaskbarCheckbox);
        optionsPanel.Children.Add(cleanStartMenuCheckbox);

        var controls = new ImportOptionControls(
            winAppsInstallRadio, winAppsUninstallRadio, winAppsSelectOnlyRadio,
            extAppsInstallRadio, extAppsUninstallRadio, extAppsSelectOnlyRadio,
            themeWallpaperCheckbox, cleanTaskbarCheckbox, cleanStartMenuCheckbox);

        return (optionsPanel, controls);
    }

    private RadioButton CreateRadioButton(string text, string groupName, bool isChecked, string categoryText, string optionText, int row, int column)
    {
        var radio = new RadioButton
        {
            Content = new TextBlock { Text = text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center },
            GroupName = groupName,
            VerticalContentAlignment = VerticalAlignment.Center,
            IsChecked = isChecked,
            IsEnabled = false,
            MinWidth = 0,
            MinHeight = 0,
            Padding = new Thickness(4, 0, 4, 0)
        };
        AutomationProperties.SetName(radio, $"{categoryText}: {optionText}");
        radio.Checked += (_, _) => DialogAccessibilityHelper.AnnounceToNarrator(radio, $"{categoryText}: {optionText}");
        Grid.SetRow(radio, row);
        Grid.SetColumn(radio, column);
        return radio;
    }

    private CheckBox CreateAccessibleCheckBox(string text, bool isChecked, Thickness? margin = null)
    {
        var checkBox = new CheckBox
        {
            Content = text,
            IsChecked = isChecked,
            IsEnabled = false,
            MinHeight = 0,
            Padding = new Thickness(4, 2, 4, 2)
        };
        if (margin.HasValue)
            checkBox.Margin = margin.Value;

        checkBox.Checked += (_, _) => DialogAccessibilityHelper.AnnounceToNarrator(
            checkBox,
            $"{text}: {_localization.GetString("Accessibility_Checked") ?? "Checked"}");
        checkBox.Unchecked += (_, _) => DialogAccessibilityHelper.AnnounceToNarrator(
            checkBox,
            $"{text}: {_localization.GetString("Accessibility_Unchecked") ?? "Unchecked"}");

        return checkBox;
    }
}
