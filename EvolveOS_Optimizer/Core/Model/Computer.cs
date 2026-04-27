// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using EvolveOS_Optimizer.Core.Model.MemoryModel;

namespace EvolveOS_Optimizer.Core.Model
{
    public class Computer : ObservableObject
    {
        private Memory _memory;
        private OperatingSystem _operatingSystem;

        public Computer()
        {
            _memory = new Memory();
            _operatingSystem = new OperatingSystem();
        }

        public Memory Memory
        {
            get => _memory;
            set => SetProperty(ref _memory, value);
        }

        public OperatingSystem OperatingSystem
        {
            get => _operatingSystem;
            set => SetProperty(ref _operatingSystem, value);
        }
    }
}
