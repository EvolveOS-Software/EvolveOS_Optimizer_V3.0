// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public class SecureBootHelper
    {
        public static async Task<bool> IsCa2023EnrolledAsync()
        {
            try
            {
                var powerShellScript = "[System.Text.Encoding]::ASCII.GetString((Get-SecureBootUEFI db).bytes) -match 'Windows UEFI CA 2023'";
                string result = await CommandExecutor.GetCommandOutput(powerShellScript, isPowerShell: true);
                return result.Contains("True", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Secure Boot Check Failed: {ex.Message}");
                return false;
            }
        }
    }
}