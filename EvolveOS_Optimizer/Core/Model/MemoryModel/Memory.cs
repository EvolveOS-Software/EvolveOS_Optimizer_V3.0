// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

namespace EvolveOS_Optimizer.Core.Model.MemoryModel
{
    public class Memory
    {
        public Memory()
        {
            Physical = new MemoryStats(0, 0, 0);
            Virtual = new MemoryStats(0, 0, 0);
        }

        public Memory(EvolveOS_Optimizer.Core.Structs.Windows.MemoryStatusEx memoryStatusEx)
        {
            if (memoryStatusEx == null)
            {
                throw new ArgumentNullException("memoryStatusEx");
            }

            Physical = new MemoryStats(memoryStatusEx.AvailPhys, memoryStatusEx.TotalPhys, memoryStatusEx.MemoryLoad);
            Virtual = new MemoryStats(memoryStatusEx.AvailPageFile, memoryStatusEx.TotalPageFile);
        }

        public MemoryStats Physical { get; set; }
        public MemoryStats Virtual { get; private set; }
    }
}