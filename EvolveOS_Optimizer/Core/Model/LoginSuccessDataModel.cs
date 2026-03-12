// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using System.Security;

namespace EvolveOS_Optimizer.Core.Model
{
    public class LoginSuccessData
    {
        public string? Username { get; set; }

        public SecureString? MasterPassword { get; set; }
    }
}
