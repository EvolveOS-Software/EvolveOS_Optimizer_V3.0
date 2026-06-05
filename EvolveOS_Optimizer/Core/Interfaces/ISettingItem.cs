// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.Generic;
using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface ISettingItem
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    string? GroupName { get; }
    InputType InputType { get; }
    IReadOnlyList<SettingDependency> Dependencies { get; }

}
