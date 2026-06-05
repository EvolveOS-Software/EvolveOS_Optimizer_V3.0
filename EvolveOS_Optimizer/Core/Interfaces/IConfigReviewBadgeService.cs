// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System;

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface IConfigReviewBadgeService
{
    event EventHandler? BadgeStateChanged;
    void MarkFeatureVisited(string featureId);
    int GetNavBadgeCount(string sectionTag);
    int GetFeatureDiffCount(string featureId);
    int GetFeaturePendingDiffCount(string featureId);
    bool IsFeatureInConfig(string featureId);
    bool IsSectionFullyReviewed(string sectionTag);
    bool IsFeatureFullyReviewed(string featureId);
    bool IsSoftwareAppsReviewed { get; set; }
    void NotifyBadgeStateChanged();
}
