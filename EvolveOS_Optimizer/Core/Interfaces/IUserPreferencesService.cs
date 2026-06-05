// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface IUserPreferencesService
{
    Task<Dictionary<string, object>> GetPreferencesAsync();
    Task<OperationResult> SavePreferencesAsync(Dictionary<string, object> preferences);
    Task<T> GetPreferenceAsync<T>(string key, T defaultValue);
    Task<OperationResult> SetPreferenceAsync<T>(string key, T value);
    T GetPreference<T>(string key, T defaultValue);
}
