// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using Microsoft.Win32;

namespace EvolveOS_Optimizer.Utilities.Managers
{
    public static class AuthSessionManager
    {
        private const string FullRegistryPath = @"HKEY_CURRENT_USER\Software\EvolveOS_Optimizer\Session";

        public static void CreateAutoLoginSession(string username, int hours)
        {
            int sessionHours = Math.Min(hours, 8);
            DateTime expiry = DateTime.Now.AddHours(sessionHours);

            Registry.SetValue(FullRegistryPath, "SessionExpiry", expiry.ToString("o"), RegistryValueKind.String);
            Registry.SetValue(FullRegistryPath, "IsAutoLoginEnabled", "True", RegistryValueKind.String);
        }

        public static void ClearSession()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\EvolveOS_Optimizer\Session", true))
                {
                    if (key != null)
                    {
                        key.DeleteValue("IsAutoLoginEnabled", false);
                        key.DeleteValue("SessionExpiry", false);
                    }
                }

                TokenManager.DeleteToken();
            }
            catch (Exception ex) { Debug.WriteLine("Logout cleanup failed: " + ex.Message); }
        }

        public static bool IsSessionValid(out string? username, out DateTime expiry)
        {
            username = null;
            expiry = DateTime.MinValue;

            object? isEnabledObj = Registry.GetValue(FullRegistryPath, "IsAutoLoginEnabled", "False");
            object? expiryObj = Registry.GetValue(FullRegistryPath, "SessionExpiry", string.Empty);

            string isEnabled = isEnabledObj?.ToString() ?? "False";
            string expiryStr = expiryObj?.ToString() ?? string.Empty;

            var (savedUsername, savedToken) = TokenManager.LoadAndDecryptToken();

            if (isEnabled == "True" && !string.IsNullOrEmpty(savedToken) && DateTime.TryParse(expiryStr, out expiry))
            {
                if (DateTime.Now < expiry)
                {
                    username = savedUsername;
                    return !string.IsNullOrEmpty(username);
                }
            }
            ClearSession();
            return false;
        }
    }
}