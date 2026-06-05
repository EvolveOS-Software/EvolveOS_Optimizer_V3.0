using Microsoft.UI.Xaml.Automation.Peers;

namespace EvolveOS_Optimizer.Utilities.Helpers;

internal static class DialogAccessibilityHelper
{
    public static void AnnounceToNarrator(UIElement element, string announcement, string activityId = "DialogNotification")
    {
        var peer = FrameworkElementAutomationPeer.FromElement(element)
                ?? FrameworkElementAutomationPeer.CreatePeerForElement(element);
        peer?.RaiseNotificationEvent(
            AutomationNotificationKind.ActionCompleted,
            AutomationNotificationProcessing.ImportantMostRecent,
            announcement,
            activityId);
    }
}
