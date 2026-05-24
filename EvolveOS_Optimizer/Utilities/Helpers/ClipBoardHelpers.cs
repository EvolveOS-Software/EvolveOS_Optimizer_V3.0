// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Windows.ApplicationModel.DataTransfer;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class ClipBoardHelpers
    {
        public static void SetContent(string str)
        {
            var dp = new DataPackage();
            dp.SetText(str);
            Clipboard.SetContent(dp);
        }
    }
}
