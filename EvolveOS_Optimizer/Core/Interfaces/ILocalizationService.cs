// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface ILocalizationService
{
    string GetString(string key);

    string GetString(string key, params object[] args);

    string CurrentLanguage { get; }

    bool IsRightToLeft { get; }

    bool SetLanguage(string languageCode);

    event EventHandler? LanguageChanged;
}