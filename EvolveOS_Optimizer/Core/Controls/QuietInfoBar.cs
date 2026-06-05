// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Automation.Peers;

namespace EvolveOS_Optimizer.Core.Controls;

public partial class QuietInfoBar : InfoBar
{
    protected override AutomationPeer OnCreateAutomationPeer()
        => new QuietInfoBarAutomationPeer(this);
}

internal partial class QuietInfoBarAutomationPeer : FrameworkElementAutomationPeer
{
    public QuietInfoBarAutomationPeer(QuietInfoBar owner) : base(owner) { }

    protected override AutomationLiveSetting GetLiveSettingCore()
        => AutomationLiveSetting.Off;

    protected override string GetClassNameCore() => "InfoBar";

    protected override AutomationControlType GetAutomationControlTypeCore()
        => AutomationControlType.StatusBar;

    protected override string GetNameCore()
    {
        if (Owner is InfoBar infoBar && !string.IsNullOrEmpty(infoBar.Message))
            return $"{infoBar.Severity}: {infoBar.Message}";
        return base.GetNameCore();
    }
}
