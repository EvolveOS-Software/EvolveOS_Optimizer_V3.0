// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class LightingNativeBridge
    {
        private const string DllName = "EvolveOS_LightingProxy.dll";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool InitLighting();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetGlobalColor(byte r, byte g, byte b);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ShutdownLighting();
    }
}