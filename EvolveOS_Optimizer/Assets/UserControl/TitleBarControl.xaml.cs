// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Assets.UserControl
{
    public sealed partial class TitleBarControl : Microsoft.UI.Xaml.Controls.UserControl
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(TitleBarControl), new PropertyMetadata(null));

        public string? Title
        {
            get => (string?)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public TitleBarControl()
        {
            this.InitializeComponent();
        }
    }
}