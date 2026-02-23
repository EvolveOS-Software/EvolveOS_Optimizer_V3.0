using EvolveOS_Optimizer.Utilities.Helpers;

namespace EvolveOS_Optimizer.Core.ViewModel.Items
{
    public sealed class CategorySummaryItem
    {
        public required string Category { get; init; }
        public required int TotalCount { get; init; }
        public required int ConfiguredCount { get; init; }
        public required string IconGlyph { get; init; }

        public string? StatusText => ConfiguredCount == 0
            ? ResourceString.GetString("GroupPolicyPage_NotConfigured")
            : string.Format(ResourceString.GetString("GroupPolicyPage_ConfiguredCount"), ConfiguredCount);

        public SolidColorBrush StatusColor => ConfiguredCount == 0
            ? new SolidColorBrush(Colors.Green)
            : (SolidColorBrush)Application.Current.Resources["SystemFillColorCautionBrush"];

        public bool HasConfiguredPolicies => ConfiguredCount > 0;

        public string? ButtonText => ResourceString.GetString("GroupPolicyPage_RemoveOverrides");
    }
}
