// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface IInitializationService
{
    bool IsGloballyInitializing { get; }
    void StartFeatureInitialization(string featureName);
    void CompleteFeatureInitialization(string featureName);
}
