using EvolveOS_Optimizer.Core.Model;
using Windows.System;

namespace EvolveOS_Optimizer.Core.Interfaces
{
    public interface IHotkeyService : IDisposable
    {
        List<VirtualKey> Keys { get; }

        Dictionary<VirtualKeyModifiers, string> Modifiers { get; }

        bool Register(Hotkey hotkey, Action action);

        void Register(uint modifiers, uint key, Action action);

        bool Unregister(Hotkey hotkey);

        void UnregisterAll();
    }
}