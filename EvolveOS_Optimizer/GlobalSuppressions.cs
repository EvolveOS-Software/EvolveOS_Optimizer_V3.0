using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Interoperability", "CA1416:Validate platform compatibility",
    Justification = "EvolveOS Optimizer is a Windows-specific tool and is not intended for cross-platform use.")]

[assembly: SuppressMessage("Style", "IDE0060:Remove unused parameter",
    Justification = "Standard for WinUI 3 event handlers (sender/e).",
    Scope = "module")]

[assembly: SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable",
    Justification = "MainWindow and Pages are managed by the WinUI 3 lifecycle.",
    Scope = "module")]