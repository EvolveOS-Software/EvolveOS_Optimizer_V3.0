// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using System.ComponentModel;

namespace EvolveOS_Optimizer.Utilities.Configuration
{
    public enum KeyDerivationMode
    {
        [Description("AES-256 (GCM Authenticated)")]
        AES_256_GCM_Authenticated,

        [Description("AES-256 (High Security)")]
        HighSecurity,

        [Description("AES-256 (Balanced)")]
        Balanced,

        [Description("AES-256 (Compatibility)")]
        Compatibility
    }

    public class KeyDerivationConfig
    {
        public int IterationCount { get; }
        public int KeySize { get; }
        public int IvSize { get; }
        public string HashAlgorithm { get; }

        public bool UseGcm { get; private set; }
        public int GcmTagSize { get; private set; } = 16;

        private KeyDerivationConfig(int iterations, int keySize, int ivSize, string hashAlgorithm)
        {
            IterationCount = iterations;
            KeySize = keySize;
            IvSize = ivSize;
            HashAlgorithm = hashAlgorithm;
            UseGcm = false;
        }

        public static KeyDerivationConfig Create(KeyDerivationMode mode)
        {
            KeyDerivationConfig config;

            switch (mode)
            {
                case KeyDerivationMode.AES_256_GCM_Authenticated:
                    config = new KeyDerivationConfig(
                        iterations: 300000,
                        keySize: 32,
                        ivSize: 12,
                        hashAlgorithm: "SHA512");
                    config.UseGcm = true;
                    return config;

                case KeyDerivationMode.HighSecurity:
                    return new KeyDerivationConfig(
                        iterations: 100000,
                        keySize: 32,
                        ivSize: 16,
                        hashAlgorithm: "SHA512");

                case KeyDerivationMode.Balanced:
                    return new KeyDerivationConfig(
                        iterations: 100000,
                        keySize: 32,
                        ivSize: 16,
                        hashAlgorithm: "SHA256");

                case KeyDerivationMode.Compatibility:
                    return new KeyDerivationConfig(
                        iterations: 10000,
                        keySize: 32,
                        ivSize: 16,
                        hashAlgorithm: "SHA1");

                default:
                    return Create(KeyDerivationMode.Balanced);
            }
        }

        public static KeyDerivationConfig Default => Create(KeyDerivationMode.Balanced);
    }
}