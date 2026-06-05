// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Threading;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface IPowerShellRunner
{
    Task<string> RunScriptAsync(string script, IProgress<TaskProgressDetail>? progress = null, CancellationToken ct = default);

    Task<string> RunScriptInMemoryAsync(string script, IProgress<TaskProgressDetail>? progress = null, CancellationToken ct = default);

    Task<string> RunScriptFileAsync(string scriptPath, string arguments = "", IProgress<TaskProgressDetail>? progress = null, CancellationToken ct = default);
    Task ValidateScriptSyntaxAsync(string scriptContent, CancellationToken ct = default);
    Task ValidateXmlSyntaxAsync(string xmlContent, CancellationToken ct = default);
}
