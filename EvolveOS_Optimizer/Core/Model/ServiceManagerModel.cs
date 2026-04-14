using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EvolveOS_Optimizer.Core.Model;

internal class ServiceManagerModel : INotifyPropertyChanged
{
    private string _status = string.Empty;
    private string _startType = string.Empty;
    private bool _canStart;
    private bool _canStop;
    private string _displayName = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string DisplayName
    {
        get => _displayName;
        set { if (_displayName != value) { _displayName = value; OnPropertyChanged(); } }
    }

    public string Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusDisplay));
            }
        }
    }

    public string StartType
    {
        get => _startType;
        set
        {
            if (_startType != value)
            {
                _startType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StartTypeIndex));
            }
        }
    }

    public bool CanStart
    {
        get => _canStart;
        set { if (_canStart != value) { _canStart = value; OnPropertyChanged(); } }
    }

    public bool CanStop
    {
        get => _canStop;
        set { if (_canStop != value) { _canStop = value; OnPropertyChanged(); } }
    }

    #region Computed Properties
    public StatusInfo StatusDisplay => Status switch
    {
        "Running" => new StatusInfo
        {
            Glyph = "\uE768",
            Color = (Brush)App.Current.Resources["SystemFillColorSuccessBrush"]
        },
        "Stopped" => new StatusInfo
        {
            Glyph = "\uE71A",
            Color = (Brush)App.Current.Resources["SystemFillColorCautionBrush"]
        },
        _ => new StatusInfo
        {
            Glyph = "\uE7BA",
            Color = (Brush)App.Current.Resources["SystemFillColorBaseMediumBrush"]
        }
    };

    public int StartTypeIndex => StartType switch
    {
        "Automatic" => 0,
        "Manual" => 1,
        "Disabled" => 2,
        _ => 1
    };
    #endregion

    #region Methods
    public void UpdateFrom(ServiceManagerModel other)
    {
        DisplayName = other.DisplayName;
        Status = other.Status;
        StartType = other.StartType;
        CanStart = other.CanStart;
        CanStop = other.CanStop;
    }
    #endregion

    #region INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
    #endregion
}

internal class StatusInfo
{
    public string Glyph { get; set; } = string.Empty;
    public Brush Color { get; set; } = null!;
}