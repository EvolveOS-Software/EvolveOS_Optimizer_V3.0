// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Pages;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Graphics;

namespace EvolveOS_Optimizer.Helpers
{
    public static class PropertyWindowHelpers
    {
        #region Fields
        private static readonly List<Window> _activeWindows = new List<Window>();
        #endregion

        #region Window Creation
        public static void CreatePropertyWindow(KeyItem item)
        {
            var frame = new Frame();

            frame.Navigate(typeof(MainPropertyPage), item, new SuppressNavigationTransitionInfo());

            var propertiesWindow = new Window
            {
                Content = frame,
            };

            UIHelper.ApplyBackdrop(propertiesWindow, SettingsEngine.Backdrop);

            _activeWindows.Add(propertiesWindow);

            propertiesWindow.Closed += (sender, args) =>
            {
                _activeWindows.Remove(propertiesWindow);
            };

            ConfigureWindowAppearance(propertiesWindow, frame, item);
            PositionWindowNearCursor(propertiesWindow.AppWindow);

            propertiesWindow.AppWindow.Show();
        }
        #endregion

        #region Configuration
        private static void ConfigureWindowAppearance(Window window, Frame frame, KeyItem item)
        {
            var appWindow = window.AppWindow;
            appWindow.Title = "Permissions";

            appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            appWindow.Resize(new SizeInt32(460, 550));

            string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "EvolveOS_Optimizer.ico");
            if (File.Exists(iconPath))
            {
                appWindow.SetIcon(iconPath);
            }

            if (frame.Content is MainPropertyPage properties)
            {
                properties.AppWindow = appWindow;
                properties.KeyItem = item;
            }
        }

        private static void PositionWindowNearCursor(AppWindow appWindow)
        {
            if (Win32Helper.GetCursorPos(out var pointerPosition))
            {
                var displayArea = DisplayArea.GetFromPoint(new PointInt32(pointerPosition.X, pointerPosition.Y), DisplayAreaFallback.Nearest);

                var appWindowPos = new PointInt32
                {
                    X = displayArea.WorkArea.X
                        + Math.Max(0, Math.Min(displayArea.WorkArea.Width - appWindow.Size.Width, pointerPosition.X - displayArea.WorkArea.X)),

                    Y = displayArea.WorkArea.Y
                        + Math.Max(0, Math.Min(displayArea.WorkArea.Height - appWindow.Size.Height, pointerPosition.Y - displayArea.WorkArea.Y)),
                };

                appWindow.Move(appWindowPos);
            }
        }
        #endregion
    }
}