// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Native;

namespace EvolveOS_Optimizer.Utilities.Services;

public class SystemParametersService : ISystemParametersService
{
    public int SystemParametersInfo(int uAction, int uParam, string? lpvParam, int fuWinIni)
    {
        return User32Api.SystemParametersInfo(uAction, uParam, lpvParam, fuWinIni);
    }
}
