// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.ComponentModel;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface IComboBoxSetupService
{
    Task<ComboBoxSetupResult> SetupComboBoxOptionsAsync(SettingDefinition setting, object? currentValue);
    Task<int> ResolveIndexFromRawValuesAsync(SettingDefinition setting, Dictionary<string, object?> rawValues);
}

#region ComboBox Setup Result
public class ComboBoxSetupResult
{
    public ObservableCollection<ComboBoxDisplayOption> Options { get; set; } = new();
    public object? SelectedValue { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
#endregion

#region ComboBox Display Option
public class ComboBoxDisplayOption : INotifyPropertyChanged
{
    private string _displayText;

    public ComboBoxDisplayOption(string displayText, object value, string? description = null, object? tag = null)
    {
        _displayText = displayText;
        Value = value;
        Description = description;
        Tag = tag;
    }

    public string DisplayText
    {
        get => _displayText;
        set
        {
            if (_displayText != value)
            {
                _displayText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayText)));
            }
        }
    }

    public object Value { get; }
    public string? Description { get; }
    public object? Tag { get; }
    public bool IsRecommended { get; set; }
    public bool IsDefault { get; set; }
    public bool IsSubjectivePreference { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    public override string ToString() => DisplayText;
}
#endregion
