// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Threading.Tasks;

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface IWallpaperService
{
    Task<bool> SetWallpaperAsync(string wallpaperPath);
    string GetDefaultWallpaperPath(bool isWindows11, bool isDarkMode);
}
