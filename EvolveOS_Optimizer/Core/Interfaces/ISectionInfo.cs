// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface ISectionInfo
{
    string Key { get; }
    string IconGlyphKey { get; }
    string DisplayName { get; }
    string ModuleId { get; }
}
