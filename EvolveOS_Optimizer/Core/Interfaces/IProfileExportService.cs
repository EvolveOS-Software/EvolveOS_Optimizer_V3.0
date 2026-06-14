// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface IProfileExportService
{
    /// <summary>
    /// Reads the current state of the system and exports it to a JSON profile.
    /// </summary>
    /// <param name="filePath">The destination path for the .json file.</param>
    Task ExportCurrentSystemStateAsync(string filePath);
}