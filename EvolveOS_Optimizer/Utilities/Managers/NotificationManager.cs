using System.Threading;
using EvolveOS_Optimizer.Views;

namespace EvolveOS_Optimizer.Utilities.Managers
{
    public static class NotificationManager
    {
        public enum NoticeAction { None, Logout, Restart }
        public enum NoticeSeverity { Info, Warning, Error, Success }

        internal static readonly Dictionary<string, NoticeAction> ConfActions = new Dictionary<string, NoticeAction>()
        {
            //["TglButton1"] = NoticeAction.Restart
        };

        internal static readonly Dictionary<string, NoticeAction> IntfActions = new Dictionary<string, NoticeAction>()
        {
            //["TglButton2"] = NoticeAction.Logout
        };

        internal static readonly Dictionary<string, NoticeAction> SysActions = new Dictionary<string, NoticeAction>()
        {
            //["TglButton3"] = NoticeAction.Logout
        };

        private static int _isNotificationOpen;

        internal static NotificationBuilder Show(string title = "", string text = "")
        {
            return new NotificationBuilder(title, text);
        }

        internal sealed class NotificationBuilder
        {
            private readonly string _title;
            private readonly string _text;
            private NoticeSeverity _severity = NoticeSeverity.Info;
            private int _delayMs = 100;
            private int _durationMs = 4000;

            internal NotificationBuilder(string title, string text)
            {
                _title = title;
                _text = text;
            }

            internal NotificationBuilder WithDuration(int ms)
            {
                _durationMs = ms;
                return this;
            }

            internal NotificationBuilder WithSeverity(NoticeSeverity severity)
            {
                _severity = severity;
                return this;
            }

            internal NotificationBuilder WithSeverity(InfoBarSeverity severity)
            {
                _severity = severity switch
                {
                    InfoBarSeverity.Success => NoticeSeverity.Success,
                    InfoBarSeverity.Warning => NoticeSeverity.Warning,
                    InfoBarSeverity.Error => NoticeSeverity.Error,
                    _ => NoticeSeverity.Info
                };
                return this;
            }

            internal NotificationBuilder WithDelay(int ms)
            {
                _delayMs = ms;
                return this;
            }

            internal void Perform(NoticeAction action = NoticeAction.None) => Create(action);
            internal void Logout() => Create(NoticeAction.Logout);
            internal void Restart() => Create(NoticeAction.Restart);

            public async void Create(NoticeAction action = NoticeAction.None)
            {
                if (Interlocked.CompareExchange(ref _isNotificationOpen, 1, 0) == 0)
                {
                    if (_delayMs > 0) await Task.Delay(_delayMs);

                    var window = new NotificationWindow(_title, _text, action, _severity, _durationMs);
                    window.Closed += (s, e) => Interlocked.Exchange(ref _isNotificationOpen, 0);
                    window.Activate();
                }
            }
        }
    }
}