// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Security;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public class LoginSuccessData
    {
        public string Username { get; }

        public SecureString MasterSecurePassword { get; }

        public LoginSuccessData(string username)
        {
            this.Username = username;
            this.MasterSecurePassword = new SecureString();
        }

        public LoginSuccessData(string username, SecureString masterSecurePassword)
        {
            this.Username = username;

            this.MasterSecurePassword = masterSecurePassword;
        }

        public static string ToInsecureString(SecureString secureString)
        {
            if (secureString == null)
            {
                return string.Empty;
            }

            IntPtr unmanagedString = IntPtr.Zero;
            try
            {
                unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(secureString);
                return Marshal.PtrToStringUni(unmanagedString) ?? string.Empty;
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
            }
        }
    }
}