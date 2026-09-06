// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.Win32;
using EvolveOS_Optimizer.Core.Model;

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

            Registry.SetValue(FullRegistryPath, "UserType", UserSession.UserType ?? "Guest", RegistryValueKind.String);
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
                        key.DeleteValue("UserType", false); // Clear the saved role
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
            object? userTypeObj = Registry.GetValue(FullRegistryPath, "UserType", "Guest");

            string isEnabled = isEnabledObj?.ToString() ?? "False";
            string expiryStr = expiryObj?.ToString() ?? string.Empty;

            var (savedUsername, savedToken) = TokenManager.LoadAndDecryptToken();

            if (isEnabled == "True" && !string.IsNullOrEmpty(savedToken) && DateTime.TryParse(expiryStr, out expiry))
            {
                if (DateTime.Now < expiry)
                {
                    username = savedUsername;

                    UserSession.UserType = userTypeObj?.ToString() ?? "Guest";

                    return !string.IsNullOrEmpty(username);
                }
            }
            ClearSession();
            return false;
        }
    }
}