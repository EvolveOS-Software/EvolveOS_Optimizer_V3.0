// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;
using EvolveOS_Optimizer.Utilities.Extensions;

namespace EvolveOS_Optimizer.Core.Model
{
    public partial class FileCategoryInsight : ObservableObject
    {
        public string CategoryName { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string FormattedSize => SizeBytes.FormatBytes();
        public double Percentage { get; set; }
        public string ColorHex { get; set; } = "#808080";
        public string IconGlyph { get; set; } = "\uE8A5";
        public bool IsSelected { get; set; }
    }
}