// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;

namespace EvolveOS_Optimizer.Core.Controls;

public sealed partial class SettingsListView : UserControl
{
    #region Dependency Properties & Wrappers
    public static readonly DependencyProperty GroupedSettingsSourceProperty =
        DependencyProperty.Register(
            nameof(GroupedSettingsSource),
            typeof(ICollectionView),
            typeof(SettingsListView),
            new PropertyMetadata(null, OnGroupedSettingsSourceChanged));

    public static readonly DependencyProperty IsLoadingProperty =
        DependencyProperty.Register(
            nameof(IsLoading),
            typeof(bool),
            typeof(SettingsListView),
            new PropertyMetadata(false, OnIsLoadingChanged));

    public static readonly DependencyProperty HasNoSearchResultsProperty =
        DependencyProperty.Register(
            nameof(HasNoSearchResults),
            typeof(bool),
            typeof(SettingsListView),
            new PropertyMetadata(false));

    public ICollectionView? GroupedSettingsSource
    {
        get => (ICollectionView?)GetValue(GroupedSettingsSourceProperty);
        set => SetValue(GroupedSettingsSourceProperty, value);
    }

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public bool IsNotLoading => !IsLoading;

    public bool HasNoSearchResults
    {
        get => (bool)GetValue(HasNoSearchResultsProperty);
        set => SetValue(HasNoSearchResultsProperty, value);
    }
    #endregion

    #region Constructor
    public SettingsListView()
    {
        this.InitializeComponent();
        SettingsListViewControl.LosingFocus += ListView_LosingFocus;

        PageScrollHelper.Attach(this, ContentScrollView);
    }
    #endregion

    #region Focus & Keyboard Navigation Handlers
    private void ListView_LosingFocus(object sender, LosingFocusEventArgs e)
    {
        if (e.InputDevice != FocusInputDeviceKind.Keyboard) return;

        bool isForward;
        if (e.Direction == FocusNavigationDirection.Next)
            isForward = true;
        else if (e.Direction == FocusNavigationDirection.Previous)
            isForward = false;
        else if (e.Direction == FocusNavigationDirection.None && e.NewFocusedElement == null)
            isForward = true;
        else
            return;

        var oldElement = e.OldFocusedElement as DependencyObject;
        if (oldElement == null) return;

        DependencyObject? current = oldElement;
        while (current != null && current is not ListViewItem)
            current = VisualTreeHelper.GetParent(current);

        if (current is not ListViewItem currentItem) return;

        if (e.NewFocusedElement is DependencyObject newElement)
        {
            DependencyObject? newParent = newElement;
            while (newParent != null && newParent is not ListViewItem)
                newParent = VisualTreeHelper.GetParent(newParent);
            if (newParent == currentItem) return;
        }

        var currentIndex = SettingsListViewControl.IndexFromContainer(currentItem);
        if (currentIndex < 0) return;

        var itemCount = SettingsListViewControl.Items.Count;
        var step = isForward ? 1 : -1;

        for (var i = currentIndex + step; i >= 0 && i < itemCount; i += step)
        {
            var nextContainer = SettingsListViewControl.ContainerFromIndex(i) as ListViewItem;

            if (nextContainer == null)
            {
                SettingsListViewControl.ScrollIntoView(SettingsListViewControl.Items[i]);
                SettingsListViewControl.UpdateLayout();
                nextContainer = SettingsListViewControl.ContainerFromIndex(i) as ListViewItem;
            }

            if (nextContainer == null) continue;

            var nextFocusable = isForward
                ? FocusManager.FindFirstFocusableElement(nextContainer)
                : FocusManager.FindLastFocusableElement(nextContainer);

            if (nextFocusable is DependencyObject nextTarget)
            {
                if (e.TrySetNewFocusedElement(nextTarget)) return;
            }
        }
    }
    #endregion

    #region Accessibility & Interactivity
    private void TechnicalDetailsAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;

        var focused = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
        if (focused == null) return;

        DependencyObject? current = focused;
        while (current != null && current is not ListViewItem)
            current = VisualTreeHelper.GetParent(current);

        if (current is not ListViewItem listViewItem) return;

        var dataItem = SettingsListViewControl.ItemFromContainer(listViewItem);
        if (dataItem is not SettingItemViewModel vm) return;

        if (!vm.ShowTechnicalDetailsBar) return;

        vm.ToggleTechnicalDetails();

        var localizationService = App.Services.GetService<ILocalizationService>();
        var stateText = vm.IsTechnicalDetailsExpanded
            ? localizationService?.GetString("TechnicalDetails_On") ?? "Technical Details: On"
            : localizationService?.GetString("TechnicalDetails_Off") ?? "Technical Details: Off";

        var announcement = $"{vm.Name}: {stateText}";

        if (vm.IsTechnicalDetailsExpanded && vm.TechnicalDetailSections.Count > 0)
        {
            var details = string.Join(". ",
                vm.TechnicalDetailSections.SelectMany(s => s.Rows).Select(d => d.AccessibleSummary));
            announcement = $"{announcement}. {details}";
        }

        if (focused is UIElement focusedUi)
        {
            var peer = FrameworkElementAutomationPeer.FromElement(focusedUi)
                       ?? FrameworkElementAutomationPeer.CreatePeerForElement(focusedUi);
            peer?.RaiseNotificationEvent(
                AutomationNotificationKind.ActionCompleted,
                AutomationNotificationProcessing.ImportantMostRecent,
                announcement,
                "TechnicalDetailsToggle");
        }
    }
    #endregion

    #region Property Changed Callbacks
    private static void OnGroupedSettingsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SettingsListView control)
        {
            control.SettingsListViewControl.ItemsSource = e.NewValue as ICollectionView;
            control.ScheduleFocusFirstSetting();
        }
    }

    private static void OnIsLoadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SettingsListView control)
        {
            control.Bindings.Update();
        }
    }

    private void ScheduleFocusFirstSetting()
    {
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (SettingsListViewControl.Items.Count > 0)
            {
                var container = SettingsListViewControl.ContainerFromIndex(0) as ListViewItem;
                if (container != null)
                {
                    var firstFocusable = FocusManager.FindFirstFocusableElement(container);
                    if (firstFocusable is Control focusTarget)
                    {
                        focusTarget.Focus(FocusState.Programmatic);
                        return;
                    }
                }
                SettingsListViewControl.Focus(FocusState.Programmatic);
            }
        });
    }
    #endregion
}