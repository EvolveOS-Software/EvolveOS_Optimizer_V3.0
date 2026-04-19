// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Utilities.Managers
{
    internal sealed class ControlWriterManager
    {
        private readonly Dictionary<string, object> _controlStates;

        internal ButtonCollection Button { get; }
        internal SliderCollection Slider { get; }

        internal ControlWriterManager(Dictionary<string, object> controlStates)
        {
            _controlStates = controlStates;

            Button = new ButtonCollection(_controlStates);
            Slider = new SliderCollection(_controlStates);
        }

        internal class ButtonCollection
        {
            private readonly Dictionary<string, object> _controlStates;

            internal ButtonCollection(Dictionary<string, object> controlStates)
            {
                _controlStates = controlStates;
            }

            internal bool this[int index]
            {
                get => _controlStates.TryGetValue($"TglButton{index}", out var val) && val is bool b && b;
                set => _controlStates[$"TglButton{index}"] = value;
            }
        }

        internal class SliderCollection
        {
            private readonly Dictionary<string, object> _controlStates;

            internal SliderCollection(Dictionary<string, object> controlStates)
            {
                _controlStates = controlStates;
            }

            internal object this[int index]
            {
                get => _controlStates.TryGetValue($"Slider{index}", out var val) ? val : 0;
                set => _controlStates[$"Slider{index}"] = value;
            }
        }
    }
}