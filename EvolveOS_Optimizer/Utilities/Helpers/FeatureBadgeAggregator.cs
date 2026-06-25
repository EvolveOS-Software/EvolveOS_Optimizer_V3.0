// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Core.WinOptimize.Helpers;

public static class FeatureBadgeAggregator
{
    public static FeatureBadgeSummary Aggregate(ISettingsFeatureViewModel feature)
    {
        var settings = feature.Settings;
        if (settings == null || settings.Count == 0)
            return new FeatureBadgeSummary(0, 0, 0, 0, 0);

        int totalWithBadgeData = 0;
        int recommended = 0;
        int defaultCount = 0;
        int custom = 0;
        int newCount = 0;

        foreach (var s in settings)
        {
            if (s.HasBadgeData)
            {
                totalWithBadgeData++;

                bool anyRecommended = false, anyDefault = false, anyCustom = false;
                foreach (var pill in s.BadgeRow)
                {
                    if (!pill.IsHighlighted) continue;
                    switch (pill.Kind)
                    {
                        case SettingBadgeKind.Recommended: anyRecommended = true; break;
                        case SettingBadgeKind.Default: anyDefault = true; break;
                        case SettingBadgeKind.Custom: anyCustom = true; break;
                    }
                }
                if (anyRecommended) recommended++;
                if (anyDefault) defaultCount++;
                if (anyCustom) custom++;
            }
            if (s.IsNew) newCount++;
        }

        return new FeatureBadgeSummary(totalWithBadgeData, recommended, defaultCount, custom, newCount);
    }
}
