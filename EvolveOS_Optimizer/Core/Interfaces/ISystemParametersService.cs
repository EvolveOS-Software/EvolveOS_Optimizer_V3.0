// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface ISystemParametersService
{
    int SystemParametersInfo(int uAction, int uParam, string? lpvParam, int fuWinIni);
}
