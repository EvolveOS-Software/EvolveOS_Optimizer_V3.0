// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Model;
using Windows.System;

namespace EvolveOS_Optimizer.Core.Interfaces
{
    public interface IHotkeyService : IDisposable
    {
        List<VirtualKey> Keys { get; }

        Dictionary<VirtualKeyModifiers, string> Modifiers { get; }

        Task<bool> Register(Hotkey hotkey, Action action);

        Task<bool> Register(uint modifiers, uint key, Action action);

        bool Unregister(Hotkey hotkey);

        void UnregisterAll();
    }
}