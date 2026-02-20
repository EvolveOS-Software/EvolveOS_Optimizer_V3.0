using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using EvolveOS_Optimizer.Core;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using Windows.System;

namespace EvolveOS_Optimizer.Utilities.Services
{
    public class HotkeyService : IHotkeyService, IDisposable
    {
        #region Fields
        private readonly bool _isSupported = Environment.OSVersion.Version.Major >= 6;
        private readonly Dictionary<int, Action> _registeredActions = new Dictionary<int, Action>();
        private readonly List<Hotkey> _registeredKeys = new List<Hotkey>();
        private Thread? _messageLoopThread;
        private uint _threadId;
        private bool _isRunning;

        private readonly ConcurrentQueue<Action> _threadActions = new ConcurrentQueue<Action>();
        #endregion

        #region Constructor & Properties

        public HotkeyService()
        {
            Keys = new List<VirtualKey>();
            Modifiers = new Dictionary<VirtualKeyModifiers, string>();

            if (!_isSupported) return;

            Keys = Enum.GetValues(typeof(VirtualKey))
                .Cast<VirtualKey>()
                .Where(key => new Regex("^([A-Z]|F([1-9]|1[0-2]))$", RegexOptions.IgnoreCase)
                .IsMatch(key.ToString().ToUpper()))
                .ToList();

            Modifiers = new Dictionary<VirtualKeyModifiers, string>
            {
                { VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu, "CTRL + ALT" },
                { VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, "CTRL + SHIFT" },
                { VirtualKeyModifiers.Menu | VirtualKeyModifiers.Shift, "SHIFT + ALT" }
            };

            StartMessageLoop();
        }

        public List<VirtualKey> Keys { get; }
        public Dictionary<VirtualKeyModifiers, string> Modifiers { get; }

        #endregion

        #region Background Message Loop

        private void StartMessageLoop()
        {
            _isRunning = true;

            using var threadReadyEvent = new ManualResetEventSlim(false);

            _messageLoopThread = new Thread(() =>
            {
                _threadId = Win32Helper.GetCurrentThreadId();
                threadReadyEvent.Set();

                PeekMessage(out _, IntPtr.Zero, 0, 0, 0);

                while (_isRunning)
                {
                    sbyte result = Win32Helper.GetMessage(out Structs.MSG msg, IntPtr.Zero, 0, 0);

                    if (result <= 0) break;

                    if (msg.message == Win32Helper.WM_HOTKEY)
                    {
                        int id = msg.wParam.ToInt32();
                        if (_registeredActions.TryGetValue(id, out var action))
                        {
                            action?.Invoke();
                        }
                    }
                    else if (msg.message == Win32Helper.WM_USER_REGISTER_HOTKEY)
                    {
                        while (_threadActions.TryDequeue(out var pendingAction))
                        {
                            pendingAction();
                        }
                    }

                    Win32Helper.TranslateMessage(ref msg);
                    Win32Helper.DispatchMessage(ref msg);
                }
            })
            {
                IsBackground = true,
                Name = "HotkeyMessageLoopThread"
            };

            _messageLoopThread.Start();
            threadReadyEvent.Wait();
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PeekMessage(out Structs.MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

        #endregion

        #region Registration Logic

        public void Register(uint modifiers, uint key, Action action)
        {
            var hotkey = new Hotkey((VirtualKeyModifiers)modifiers, (VirtualKey)key);
            Register(hotkey, action);
        }

        public bool Register(Hotkey hotkey, Action action)
        {
            if (!_isSupported || hotkey == null || action == null || !_isRunning) return false;

            _threadActions.Enqueue(() =>
            {
                try
                {
                    int id = hotkey.GetHashCode();
                    uint fsModifiers = ConvertWinRTModifiersToWin32(hotkey.Modifiers) | Win32Helper.MOD_NOREPEAT;

                    Win32Helper.UnregisterHotKey(IntPtr.Zero, id);

                    bool result = Win32Helper.RegisterHotKey(IntPtr.Zero, id, fsModifiers, (uint)hotkey.Key);

                    if (result)
                    {
                        _registeredActions[id] = action;
                        if (!_registeredKeys.Contains(hotkey)) _registeredKeys.Add(hotkey);
                        Debug.WriteLine($"[Hotkey] Registered {hotkey.Modifiers} + {hotkey.Key} on Thread {_threadId}");
                    }
                    else
                    {
                        int error = Marshal.GetLastWin32Error();
                        Debug.WriteLine($"[Hotkey] Failed to register. Error code: {error}");
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogging.LogDebug(ex);
                }
            });

            Win32Helper.PostThreadMessage(_threadId, Win32Helper.WM_USER_REGISTER_HOTKEY, IntPtr.Zero, IntPtr.Zero);
 
            return true;
        }

        public bool Unregister(Hotkey hotkey)
        {
            if (!_isSupported || hotkey == null || !_isRunning) return false;

            _threadActions.Enqueue(() =>
            {
                try
                {
                    int id = hotkey.GetHashCode();
                    Win32Helper.UnregisterHotKey(IntPtr.Zero, id);
                    _registeredActions.Remove(id);
                    _registeredKeys.Remove(hotkey);
                }
                catch (Exception ex)
                {
                    ErrorLogging.LogDebug(ex);
                }
            });

            Win32Helper.PostThreadMessage(_threadId, Win32Helper.WM_USER_REGISTER_HOTKEY, IntPtr.Zero, IntPtr.Zero);
            return true;
        }

        public void UnregisterAll()
        {
            if (!_isSupported || !_isRunning) return;

            _threadActions.Enqueue(() =>
            {
                try
                {
                    var keysToRemove = _registeredKeys.ToList();
                    foreach (var hotkey in keysToRemove)
                    {
                        int id = hotkey.GetHashCode();
                        Win32Helper.UnregisterHotKey(IntPtr.Zero, id);
                    }

                    _registeredActions.Clear();
                    _registeredKeys.Clear();

                    Debug.WriteLine("[Hotkey] All hotkeys unregistered.");
                }
                catch (Exception ex)
                {
                    ErrorLogging.LogDebug(ex);
                }
            });

            Win32Helper.PostThreadMessage(_threadId, Win32Helper.WM_USER_REGISTER_HOTKEY, IntPtr.Zero, IntPtr.Zero);
        }

        #endregion

        #region Helpers & IDisposable

        private uint ConvertWinRTModifiersToWin32(VirtualKeyModifiers mods)
        {
            uint win32Mods = 0;
            if (mods.HasFlag(VirtualKeyModifiers.Control)) win32Mods |= Win32Helper.MOD_CONTROL;
            if (mods.HasFlag(VirtualKeyModifiers.Menu)) win32Mods |= Win32Helper.MOD_ALT;
            if (mods.HasFlag(VirtualKeyModifiers.Shift)) win32Mods |= Win32Helper.MOD_SHIFT;
            if (mods.HasFlag(VirtualKeyModifiers.Windows)) win32Mods |= Win32Helper.MOD_WIN;
            return win32Mods;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                UnregisterAll();
                _isRunning = false;

                Win32Helper.PostThreadMessage(_threadId, 0x0012 /* WM_QUIT */, IntPtr.Zero, IntPtr.Zero);
            }
        }

        #endregion
    }
}