using EvolveOS_Optimizer.Views;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media.Animation;
using System.Threading;

namespace EvolveOS_Optimizer.Utilities.Managers
{
    public static class NotificationManager
    {
        public enum NoticeAction { None, Logout, Restart }
        public enum NoticeSeverity { Info, Warning, Error, Success }

        private static DispatcherTimer? _autoDismissTimer;
        private static double _timeLeft;
        private static bool _isPaused;
        private static MainWindow? _mainWindow;

        private static readonly Queue<Action> _pendingNotifications = new Queue<Action>();

        internal static readonly Dictionary<string, NoticeAction> ConfActions = new Dictionary<string, NoticeAction>()
        {
            //["TglButton1"] = NoticeAction.Restart
        };

        internal static readonly Dictionary<string, NoticeAction> IntfActions = new Dictionary<string, NoticeAction>()
        {
            ["TglButton1"] = NoticeAction.Logout,
            ["TglButton2"] = NoticeAction.Logout,
            ["TglButton3"] = NoticeAction.Logout,
            ["TglButton4"] = NoticeAction.Logout,
            ["TglButton5"] = NoticeAction.Logout,
            ["TglButton10"] = NoticeAction.Logout,
            ["TglButton11"] = NoticeAction.Logout,
            ["TglButton12"] = NoticeAction.Logout,
            ["TglButton20"] = NoticeAction.Restart,
            ["TglButton22"] = NoticeAction.Logout,
            ["TglButton23"] = NoticeAction.Restart
        };

        internal static readonly Dictionary<string, NoticeAction> SysActions = new Dictionary<string, NoticeAction>()
        {
            ["TglButton2"] = NoticeAction.Logout,
            ["TglButton3"] = NoticeAction.Restart,
            ["TglButton4"] = NoticeAction.Restart,
            ["TglButton5"] = NoticeAction.Restart,
            ["TglButton7"] = NoticeAction.Restart,
            ["TglButton12"] = NoticeAction.Restart,
            ["TglButton13"] = NoticeAction.Restart,
            ["TglButton14"] = NoticeAction.Restart,
            ["TglButton15"] = NoticeAction.Restart,
            ["TglButton20"] = NoticeAction.Restart,
            ["TglButton23"] = NoticeAction.Restart,
            ["TglButton25"] = NoticeAction.Restart,
            ["TglButton27"] = NoticeAction.Restart
        };

        private static int _isNotificationOpen = 0;

        public static void Initialize(MainWindow window)
        {
            _mainWindow = window;

            Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.SetIsTranslationEnabled(_mainWindow.UpdateBanner, true);
            Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.SetIsTranslationEnabled(_mainWindow.GlobalNotificationBanner, true);

            _autoDismissTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _autoDismissTimer.Tick += AutoDismissTimer_Tick;

            _isNotificationOpen = 0;
            _pendingNotifications.Clear();
        }

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

            internal void Perform(NoticeAction action = NoticeAction.None) => Create(action);
            internal void Logout() => Create(NoticeAction.Logout);
            internal void Restart() => Create(NoticeAction.Restart);

            public async void Create(NoticeAction action = NoticeAction.None)
            {
                if (_mainWindow == null) return;

                bool isMinimized = false;
                if (_mainWindow.AppWindow != null && _mainWindow.AppWindow.Presenter is OverlappedPresenter presenter)
                {
                    isMinimized = presenter.State == OverlappedPresenterState.Minimized;
                }

                if (isMinimized)
                {
                    // ShowSystemToast(_title, _text); || Windows own native System Toast ||   Nuget: Microsoft.Toolkit.Uwp.Notifications;

                    if (Interlocked.CompareExchange(ref _isNotificationOpen, 1, 0) == 0)
                    {
                        if (_delayMs > 0) await Task.Delay(_delayMs);

                        _mainWindow.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                        {
                            try
                            {
                                var window = new NotificationWindow(_title, _text, action, _severity, _durationMs);

                                window.Closed += (s, e) =>
                                {
                                    Interlocked.Exchange(ref _isNotificationOpen, 0);
                                    ProcessQueue();
                                };

                                window.Activate();
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[NotifyLog] Window Threading Error: {ex.Message}");
                                Interlocked.Exchange(ref _isNotificationOpen, 0);
                            }
                        });
                    }
                    else
                    {
                        _pendingNotifications.Enqueue(() => ShowInAppBanner(_title, _text, _severity, _durationMs));
                    }
                }
                else
                {
                    if (_mainWindow.UpdateBanner.Visibility == Visibility.Visible)
                    {
                        _pendingNotifications.Enqueue(() => ShowInAppBanner(_title, _text, _severity, _durationMs));
                        return;
                    }

                    if (Interlocked.CompareExchange(ref _isNotificationOpen, 1, 0) == 0)
                    {
                        if (_delayMs > 0) await Task.Delay(_delayMs);

                        _mainWindow.DispatcherQueue.TryEnqueue(() =>
                        {
                            ShowInAppBanner(_title, _text, _severity, _durationMs);
                        });
                    }
                    else
                    {
                        _pendingNotifications.Enqueue(() => ShowInAppBanner(_title, _text, _severity, _durationMs));
                    }
                }
            }
        }

        #region Internal Logic

        public static void ProcessQueue()
        {
            if (_mainWindow == null) return;

            if (_mainWindow.UpdateBanner.Visibility == Visibility.Visible)
            {
                Debug.WriteLine("[NotifyLog] Aborted: UpdateBanner is blocking the queue.");
                return;
            }

            if (Interlocked.CompareExchange(ref _isNotificationOpen, 1, 0) == 1)
            {
                Debug.WriteLine("[NotifyLog] Aborted: A notification is already being displayed.");
                return;
            }

            if (_pendingNotifications.Count > 0)
            {
                var nextNotification = _pendingNotifications.Dequeue();
                Debug.WriteLine($"[NotifyLog] Processing next notification. Remaining: {_pendingNotifications.Count}");

                _mainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    nextNotification.Invoke();
                });
            }
            else
            {
                Interlocked.Exchange(ref _isNotificationOpen, 0);
                Debug.WriteLine("[NotifyLog] Queue empty. Flag reset to 0 (Ready).");
            }
        }

        /*private static void ShowSystemToast(string title, string text)
        {
            new ToastContentBuilder()
                .AddHeader("EvolveOS", "EvolveOS Optimizer", "")
                .AddText(title)
                .AddText(text)
                .Show();
        }*/

        private static void ShowInAppBanner(string title, string message, NoticeSeverity severity, int duration)
        {
            if (_mainWindow == null) return;

            _autoDismissTimer?.Stop();
            _timeLeft = 100;
            _isPaused = false;
            _mainWindow.NotificationProgress.Value = 100;

            var banner = _mainWindow.GlobalNotificationBanner;
            var textBlock = _mainWindow.NotificationText;

            banner.Visibility = Visibility.Visible;
            banner.Opacity = 1;
            Canvas.SetZIndex(banner, 99999);
            banner.UpdateLayout();

            var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(banner);
            var compositor = visual.Compositor;

            visual.StopAnimation("Opacity");
            visual.StopAnimation("Translation.Y");

            var props = visual.Properties;
            props.InsertScalar("Opacity", 0.0f);
            props.InsertVector3("Translation", new System.Numerics.Vector3(0, 250f, 32f));

            banner.Background = severity switch
            {
                NoticeSeverity.Error => new SolidColorBrush(Color.FromArgb(255, 199, 0, 57)),
                NoticeSeverity.Warning => new SolidColorBrush(Color.FromArgb(255, 255, 195, 0)),
                NoticeSeverity.Success => new SolidColorBrush(Color.FromArgb(255, 46, 204, 113)),
                _ => new SolidColorBrush((Color)Application.Current.Resources["SystemAccentColor"])
            };

            textBlock.Text = string.IsNullOrEmpty(title) ? message : $"{title}: {message}";

            var batch = compositor.CreateScopedBatch(Microsoft.UI.Composition.CompositionBatchTypes.Animation);
            var easeOut = compositor.CreateCubicBezierEasingFunction(new System.Numerics.Vector2(0.3f, 0.3f), new System.Numerics.Vector2(0.0f, 1.0f));

            var moveAnim = compositor.CreateScalarKeyFrameAnimation();
            moveAnim.InsertKeyFrame(1.0f, 0f, easeOut);
            moveAnim.Duration = TimeSpan.FromMilliseconds(600);

            var fadeAnim = compositor.CreateScalarKeyFrameAnimation();
            fadeAnim.InsertKeyFrame(1.0f, 1.0f);
            fadeAnim.Duration = TimeSpan.FromMilliseconds(400);

            visual.StartAnimation("Translation.Y", moveAnim);
            visual.StartAnimation("Opacity", fadeAnim);

            batch.Completed += (s, e) =>
            {
                Debug.WriteLine("[NotifyLog] Animation In Complete. Timer starting.");
                _autoDismissTimer?.Start();
            };
            batch.End();

            if (_mainWindow.RootGrid.Resources["GlobalPulseAnimation"] is Storyboard pulse)
                pulse.Begin();
        }

        private static void AutoDismissTimer_Tick(object? sender, object e)
        {
            if (_isPaused || _mainWindow == null || _isNotificationOpen == 0) return;

            _timeLeft -= 1.2;
            _mainWindow.NotificationProgress.Value = _timeLeft;

            if (_timeLeft <= 0)
            {
                _autoDismissTimer?.Stop();
                HideBanner();
            }
        }

        public static void SetPaused(bool paused) => _isPaused = paused;

        public static void HideBanner()
        {
            if (_mainWindow == null) return;
            _autoDismissTimer?.Stop();

            var banner = _mainWindow.GlobalNotificationBanner;
            var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(banner);
            var compositor = visual.Compositor;

            var batch = compositor.CreateScopedBatch(Microsoft.UI.Composition.CompositionBatchTypes.Animation);

            var slideDown = compositor.CreateScalarKeyFrameAnimation();
            slideDown.InsertKeyFrame(1.0f, 250f);
            slideDown.Duration = TimeSpan.FromMilliseconds(300);

            var fadeOut = compositor.CreateScalarKeyFrameAnimation();
            fadeOut.InsertKeyFrame(1.0f, 0.0f);
            fadeOut.Duration = TimeSpan.FromMilliseconds(250);

            visual.StartAnimation("Translation.Y", slideDown);
            visual.StartAnimation("Opacity", fadeOut);

            batch.Completed += (s, e) =>
            {
                banner.Visibility = Visibility.Collapsed;
                Interlocked.Exchange(ref _isNotificationOpen, 0);
                ProcessQueue();
            };
            batch.End();
        }

        #endregion
    }
}