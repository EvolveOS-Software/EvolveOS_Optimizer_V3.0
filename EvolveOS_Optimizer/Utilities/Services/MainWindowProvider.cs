// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Utilities.Services;

public class MainWindowProvider : IMainWindowProvider
{
    public Window? MainWindow => App.MainWindow;
}