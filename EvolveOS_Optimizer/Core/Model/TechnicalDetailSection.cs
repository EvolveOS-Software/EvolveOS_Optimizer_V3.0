// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Model;

public sealed record TechnicalDetailSection(
    DetailRowType Type,
    string Label,
    bool StartsExpanded,
    IReadOnlyList<TechnicalDetailRow> Rows)
{
    public int Count => Rows.Count;
}
