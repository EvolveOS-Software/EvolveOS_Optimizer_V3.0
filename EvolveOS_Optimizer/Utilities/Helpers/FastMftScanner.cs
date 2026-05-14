// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class FastMftScanner
    {
        #region P/Invoke Definitions
        private const uint GENERIC_READ = 0x80000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;
        private const uint FSCTL_ENUM_USN_DATA = 0x000900B3;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode,
            IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool DeviceIoControl(IntPtr hDevice, uint dwIoControlCode,
            IntPtr lpInBuffer, int nInBufferSize, IntPtr lpOutBuffer, int nOutBufferSize,
            out uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetFileAttributesEx(string lpFileName, int fInfoLevelId, out WIN32_FILE_ATTRIBUTE_DATA fileData);

        [StructLayout(LayoutKind.Sequential)]
        private struct MFT_ENUM_DATA_V0
        {
            public ulong StartFileReferenceNumber;
            public long LowUsn;
            public long HighUsn;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct USN_RECORD_V2
        {
            public uint RecordLength;
            public ushort MajorVersion;
            public ushort MinorVersion;
            public ulong FileReferenceNumber;
            public ulong ParentFileReferenceNumber;
            public long Usn;
            public long TimeStamp;
            public uint Reason;
            public uint SourceInfo;
            public uint SecurityId;
            public uint FileAttributes;
            public ushort FileNameLength;
            public ushort FileNameOffset;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WIN32_FILE_ATTRIBUTE_DATA
        {
            public uint dwFileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
        }
        #endregion

        public static StorageNode BuildTreeFromMFT(string driveLetter, CancellationToken token)
        {
            string driveRoot = driveLetter.TrimEnd('\\');
            string volumePath = $@"\\.\{driveRoot}";

            IntPtr hVol = CreateFile(volumePath, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);

            if (hVol == IntPtr.Zero || hVol == new IntPtr(-1))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            var allNodes = new Dictionary<ulong, StorageNode>();

            try
            {
                MFT_ENUM_DATA_V0 med = new MFT_ENUM_DATA_V0
                {
                    StartFileReferenceNumber = 0,
                    LowUsn = 0,
                    HighUsn = long.MaxValue
                };

                int bufferSize = 1024 * 1024;
                IntPtr pBuffer = Marshal.AllocHGlobal(bufferSize);
                IntPtr pMed = Marshal.AllocHGlobal(Marshal.SizeOf(med));
                Marshal.StructureToPtr(med, pMed, false);

                try
                {
                    while (true)
                    {
                        if (token.IsCancellationRequested) break;

                        bool result = DeviceIoControl(hVol, FSCTL_ENUM_USN_DATA, pMed, Marshal.SizeOf(med),
                            pBuffer, bufferSize, out uint bytesReturned, IntPtr.Zero);

                        if (!result || bytesReturned <= 8)
                        {
                            int error = Marshal.GetLastWin32Error();
                            if (error == 38)
                                break;
                            throw new Win32Exception(error);
                        }

                        ulong nextReference = (ulong)Marshal.ReadInt64(pBuffer);
                        Marshal.WriteInt64(pMed, (long)nextReference);

                        IntPtr pRecord = new IntPtr(pBuffer.ToInt64() + 8);
                        int bytesProcessed = 8;

                        while (bytesProcessed < bytesReturned)
                        {
                            USN_RECORD_V2 record = Marshal.PtrToStructure<USN_RECORD_V2>(pRecord);

                            IntPtr namePtr = new IntPtr(pRecord.ToInt64() + record.FileNameOffset);
                            string name = Marshal.PtrToStringUni(namePtr, record.FileNameLength / 2);

                            bool isDir = (record.FileAttributes & 0x00000010) != 0;
                            bool isHidden = (record.FileAttributes & 0x00000002) != 0 || (record.FileAttributes & 0x00000004) != 0;

                            var node = new StorageNode
                            {
                                Name = name,
                                IsFolder = isDir,
                                IsHidden = isHidden,
                                LastModified = DateTime.FromFileTime(record.TimeStamp)
                            };

                            allNodes[record.FileReferenceNumber] = node;

                            node.Tag = record.ParentFileReferenceNumber;

                            pRecord = new IntPtr(pRecord.ToInt64() + record.RecordLength);
                            bytesProcessed += (int)record.RecordLength;
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(pBuffer);
                    Marshal.FreeHGlobal(pMed);
                }
            }
            finally
            {
                CloseHandle(hVol);
            }

            StorageNode? trueRoot = null;

            foreach (var kvp in allNodes)
            {
                var node = kvp.Value;

                ulong parentId = node.Tag is ulong id ? id : 0UL;

                if ((kvp.Key & 0x0000FFFFFFFFFFFF) == 5)
                {
                    trueRoot = node;
                    trueRoot.Name = driveRoot;
                    trueRoot.Path = driveLetter;
                }
                else if (allNodes.TryGetValue(parentId, out var parentNode))
                {
                    parentNode.Children.Add(node);
                }
            }

            if (trueRoot == null)
            {
                trueRoot = new StorageNode { Name = driveRoot, Path = driveLetter, IsFolder = true };
                foreach (var kvp in allNodes)
                {
                    ulong fallbackParentId = kvp.Value.Tag is ulong id ? id : 0UL;

                    if (!allNodes.ContainsKey(fallbackParentId))
                    {
                        trueRoot.Children.Add(kvp.Value);
                    }
                }
            }

            PopulatePathsAndSizes(trueRoot, driveLetter, token);

            CalculateDirectorySizes(trueRoot, token);

            return trueRoot;
        }

        private static void PopulatePathsAndSizes(StorageNode node, string currentPath, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;

            node.Path = currentPath;

            if (GetFileAttributesEx(currentPath, 0, out var fileData))
            {
                long ft = (((long)fileData.ftLastWriteTime.dwHighDateTime) << 32) | (uint)fileData.ftLastWriteTime.dwLowDateTime;

                if (ft != 0)
                {
                    node.LastModified = DateTime.FromFileTime(ft);
                }

                if (!node.IsFolder)
                {
                    long fileSize = ((long)fileData.nFileSizeHigh << 32) | (fileData.nFileSizeLow & 0xFFFFFFFFL);
                    node.SizeBytes = fileSize;
                    node.AllocatedSizeBytes = ((fileSize + 4095) / 4096) * 4096;
                    return;
                }
            }

            string dirPath = currentPath.EndsWith("\\") ? currentPath : currentPath + "\\";
            foreach (var child in node.Children)
            {
                PopulatePathsAndSizes(child, dirPath + child.Name, token);
            }
        }

        private static void CalculateDirectorySizes(StorageNode node, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;

            if (!node.IsFolder) return;

            long totalSize = 0;
            long totalAllocated = 0;
            int fileCount = 0;
            int folderCount = 0;

            foreach (var child in node.Children)
            {
                if (child.IsFolder)
                {
                    CalculateDirectorySizes(child, token);
                    folderCount += child.FoldersCount + 1;
                }
                else
                {
                    fileCount++;
                }

                totalSize += child.SizeBytes;
                totalAllocated += child.AllocatedSizeBytes;
                fileCount += child.FilesCount;
            }

            node.SizeBytes = totalSize;
            node.AllocatedSizeBytes = totalAllocated;
            node.FilesCount = fileCount;
            node.FoldersCount = folderCount;

            var sortedChildren = node.Children.OrderByDescending(c => c.SizeBytes).ToList();
            node.Children.Clear();

            foreach (var child in sortedChildren)
            {
                child.Percentage = node.SizeBytes > 0 ? ((double)child.SizeBytes / node.SizeBytes) * 100 : 0;
                node.Children.Add(child);
            }
        }
    }
}