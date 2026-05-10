// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface ISearchablePage
{
    void OnSearch(string text);
}

public interface IPageActions
{
    void BuildActions(MenuFlyout flyout);
}
