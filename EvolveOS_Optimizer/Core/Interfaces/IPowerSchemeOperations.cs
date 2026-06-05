// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface IPowerSchemeOperations
{
    uint DeleteScheme(Guid schemeGuid);
    uint DuplicateScheme(Guid sourceGuid, out Guid destinationGuid);
    uint SetActiveScheme(Guid schemeGuid);
    uint WriteFriendlyName(Guid schemeGuid, string name);
    uint WriteDescription(Guid schemeGuid, string description);
}
