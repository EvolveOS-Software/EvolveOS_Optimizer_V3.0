// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public class DarkGlassBackdrop : SystemBackdrop
    {
        private DesktopAcrylicController? _acrylicController;
        private SystemBackdropConfiguration? _configuration;

        protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
        {
            base.OnTargetConnected(connectedTarget, xamlRoot);

            _acrylicController = new DesktopAcrylicController
            {
                TintColor = Color.FromArgb(255, 12, 12, 12),
                TintOpacity = 0.75f,
                LuminosityOpacity = 0.85f,
                FallbackColor = Color.FromArgb(255, 18, 18, 18)
            };

            _configuration = new SystemBackdropConfiguration
            {
                IsInputActive = true,

                Theme = SystemBackdropTheme.Dark
            };

            _acrylicController.AddSystemBackdropTarget(connectedTarget);
            _acrylicController.SetSystemBackdropConfiguration(_configuration);
        }

        protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop connectedTarget)
        {
            base.OnTargetDisconnected(connectedTarget);

            if (_acrylicController is not null)
            {
                _acrylicController.RemoveSystemBackdropTarget(connectedTarget);
                _acrylicController.Dispose();
                _acrylicController = null;
            }
        }
    }
}