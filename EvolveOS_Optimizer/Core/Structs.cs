using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace EvolveOS_Optimizer.Core
{
    public static class Structs
    {
        #region General Windows Messages
        [StructLayout(LayoutKind.Sequential)]
        public struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public int pt_x;
            public int pt_y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct COORD
        {
            public short X, Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct STARTUPINFO
        {
            public int cb;
            public IntPtr lpReserved, lpDesktop, lpTitle;
            public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
            public short wShowWindow, cbReserved2;
            public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct STARTUPINFOEX
        {
            public STARTUPINFO StartupInfo; public IntPtr lpAttributeList;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId;
        }
        #endregion

        #region DNS Encryption DNSEntry
        public struct DNSEntry
        {
            public string Value;
            public bool IsV4 => Regex.IsMatch(Value, @"^(\d+?[\.]?){4}$");
            public bool IsV6 => !IsV4;

            public DNSEntry(string value)
            {
                Value = value;
            }

            public override string ToString()
            {
                return Value;
            }
        }

        public class DNSServerEntry
        {
            public string? Name;
            public float Latency = -1f;
            public DNSEntry[]? DnsEntries;

            public override string ToString()
            {
                if (Latency == -1f)
                {
                    return $"{Name}";
                }

                if (Latency <= 1f)
                {
                    return $"{Name} [<1 ms]";
                }

                if (float.IsNaN(Latency))
                {
                    return $"{Name} [Timeout]";
                }

                return $"{Name} [{Math.Round(Latency, 1)} ms]";
            }
        }

        public struct Interface
        {
            public string Name;
            public override string ToString() => Name;
        }

        public class ComboBoxItem
        {
            private readonly string _text;
            public readonly object Value;

            public ComboBoxItem(string text)
            {
                _text = text;
                Value = text;
            }

            public ComboBoxItem(string text, object value)
            {
                _text = text;
                Value = value;
            }

            public override string ToString()
            {
                return _text;
            }
        }
        #endregion

        public static class Windows
        {
            #region Memory Management Structs
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct MemoryCombineInformationEx
            {
                public IntPtr Handle;
                public IntPtr PagesCombined;
                public long Flags;
            }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
            public class MemoryStatusEx
            {
                public readonly int Length;          // The size of the structure, in bytes.  
                public int MemoryLoad;               // A number between 0 and 100 that specifies the approximate percentage of physical memory that is in use.  
                public long TotalPhys;               // The amount of actual physical memory, in bytes.  
                public long AvailPhys;               // The amount of physical memory currently available, in bytes.  
                public long TotalPageFile;           // The current committed memory limit for the system or the current process, whichever is smaller, in bytes.  
                public long AvailPageFile;           // The maximum amount of memory the current process can commit, in bytes.  
                public long TotalVirtual;            // The size of the user-mode portion of the virtual address space of the calling process, in bytes.  
                public long AvailVirtual;            // The amount of unreserved and uncommitted memory currently in the user-mode portion of the virtual address space of the calling process, in bytes.  
                public long AvailExtendedVirtual;    // Reserved. This value is always 0.  

                public MemoryStatusEx()
                {
                    Length = Marshal.SizeOf(typeof(MemoryStatusEx));
                    MemoryLoad = 0;
                    TotalPhys = 0;
                    AvailPhys = 0;
                    TotalPageFile = 0;
                    AvailPageFile = 0;
                    TotalVirtual = 0;
                    AvailVirtual = 0;
                    AvailExtendedVirtual = 0;
                }
            }
            #endregion

            #region Cache Information Structs
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct SystemFileCacheInformation32
            {
                public int CurrentSize;
                public int PeakSize;
                public int PageFaultCount;
                public int MinimumWorkingSet;
                public int MaximumWorkingSet;
                public int CurrentSizeIncludingTransitionInPages;
                public int PeakSizeIncludingTransitionInPages;
                public int TransitionRePurposeCount;
                public int Flags;
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct SystemFileCacheInformation64
            {
                public long CurrentSize;
                public long PeakSize;
                public long PageFaultCount;
                public long MinimumWorkingSet;
                public long MaximumWorkingSet;
                public long CurrentSizeIncludingTransitionInPages;
                public long PeakSizeIncludingTransitionInPages;
                public long TransitionRePurposeCount;
                public long Flags;
            }
            #endregion

            #region Security & Privilege Structs
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct TokenPrivileges
            {
                public int Count;
                public long Luid;
                public int Attr;
            }
            #endregion

            #region UI & Drawing Structs
            [StructLayout(LayoutKind.Sequential)]
            public struct Rect
            {
                public int Left;
                public int Top;
                public int Right;
                public int Bottom;
            }
            #endregion
        }
    }
}