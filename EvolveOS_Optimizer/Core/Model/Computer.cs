// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using EvolveOS_Optimizer.Core.Model.MemoryModel;

namespace EvolveOS_Optimizer.Core.Model
{
    public class Computer
    {
        public Computer()
        {
            Memory = new Memory();
            OperatingSystem = new OperatingSystem();
        }

        public Memory Memory { get; set; }

        public OperatingSystem OperatingSystem { get; set; }

    }
}
