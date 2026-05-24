// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Windows.System;

namespace EvolveOS_Optimizer.Core.Model
{
    public class Hotkey
    {
        public Hotkey(VirtualKeyModifiers modifiers, VirtualKey key)
        {
            Key = key;
            Modifiers = modifiers;
        }

        public VirtualKey Key { get; private set; }

        public VirtualKeyModifiers Modifiers { get; private set; }

        public bool Equals(Hotkey? hotKey)
        {
            if (ReferenceEquals(null, hotKey))
            {
                return false;
            }

            if (ReferenceEquals(this, hotKey))
            {
                return true;
            }

            return hotKey.Key == Key && hotKey.Modifiers == Modifiers;
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj is Hotkey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Key, Modifiers);
        }

        public override string ToString()
        {
            var modifiers = Enum.GetValues(typeof(VirtualKeyModifiers))
                .Cast<VirtualKeyModifiers>()
                .Where(flag => Modifiers.HasFlag(flag) && (int)flag != 0)
                .Select(f => f == VirtualKeyModifiers.Menu ? "ALT" : f.ToString().ToUpper())
                .OrderBy(s => s);

            string modifierString = string.Join(" + ", modifiers);

            return string.IsNullOrEmpty(modifierString)
                ? Key.ToString().ToUpper()
                : $"{modifierString} + {Key}".ToUpper();
        }
    }
}