// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;
using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Core.ViewModel;

public partial class BuilderSettingItem : ObservableObject
{
    #region Properties

    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public InputType InputType { get; }

    public List<string> Options { get; } = new();

    private bool _isEnabled;
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    private int _selectedIndex;
    public int SelectedIndex
    {
        get => _selectedIndex;
        set => SetProperty(ref _selectedIndex, value);
    }

    #endregion

    #region Constructor

    public BuilderSettingItem(SettingDefinition definition)
    {
        Id = definition.Id;
        Name = definition.Name;
        Description = definition.Description ?? string.Empty;
        InputType = definition.InputType;

        if (InputType == InputType.Selection && definition.ComboBox?.Options != null)
        {
            foreach (var opt in definition.ComboBox.Options)
            {
                Options.Add(opt.DisplayName ?? "Unknown Option");
            }
            SelectedIndex = 0;
        }
    }

    #endregion
}