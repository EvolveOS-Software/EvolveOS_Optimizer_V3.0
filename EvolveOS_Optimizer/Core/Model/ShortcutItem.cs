// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EvolveOS_Optimizer.Core.Model
{
    public class ShortcutItem : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        private string _iconGlyph = string.Empty;
        public string IconGlyph
        {
            get => _iconGlyph;
            set { _iconGlyph = value; OnPropertyChanged(); OnPropertyChanged(nameof(FontIconVisibility)); }
        }

        private string _iconImagePath = string.Empty;
        public string IconImagePath
        {
            get => _iconImagePath;
            set
            {
                _iconImagePath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CustomImageVisibility));
                OnPropertyChanged(nameof(FontIconVisibility));
                UpdateIconImage();
            }
        }

        private ImageSource? _iconImage;
        public ImageSource? IconImage
        {
            get => _iconImage;
            private set
            {
                _iconImage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CustomImageVisibility));
                OnPropertyChanged(nameof(FontIconVisibility));
            }
        }

        private void UpdateIconImage()
        {
            if (!string.IsNullOrEmpty(_iconImagePath) && File.Exists(_iconImagePath))
            {
                try
                {
                    IconImage = new BitmapImage(new Uri(_iconImagePath));
                }
                catch
                {
                    IconImage = null;
                }
            }
            else
            {
                IconImage = null;
            }
        }

        private int _displayModeIndex;
        public int DisplayModeIndex
        {
            get => _displayModeIndex;
            set { _displayModeIndex = value; OnPropertyChanged(); }
        }

        private bool _isSeparator;
        public bool IsSeparator
        {
            get => _isSeparator;
            set
            {
                _isSeparator = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StandardItemVisibility));
                OnPropertyChanged(nameof(SeparatorVisibility));
            }
        }

        public string TargetPath { get; set; } = string.Empty;
        public bool HasCustomIcon => !string.IsNullOrEmpty(IconGlyph);

        public Visibility CustomImageVisibility => !string.IsNullOrEmpty(IconImagePath) && IconImage != null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility FontIconVisibility => (string.IsNullOrEmpty(IconImagePath) || IconImage == null) && !string.IsNullOrEmpty(IconGlyph) ? Visibility.Visible : Visibility.Collapsed;
        public Visibility StandardItemVisibility => IsSeparator ? Visibility.Collapsed : Visibility.Visible;
        public Visibility SeparatorVisibility => IsSeparator ? Visibility.Visible : Visibility.Collapsed;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}