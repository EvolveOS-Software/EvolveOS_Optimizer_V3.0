// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using EvolveOS_Optimizer.Core.Model;
using Microsoft.Win32.SafeHandles;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public enum ScanMode
    {
        DeepScanSectorCarving,
        MftQuickScan
    }

    public static class DataRecoveryEngine
    {
        #region Win32 P/Invoke & IOCTL Definitions

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern SafeFileHandle CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool DeviceIoControl(
            SafeFileHandle hDevice,
            uint dwIoControlCode,
            IntPtr lpInBuffer,
            uint nInBufferSize,
            IntPtr lpOutBuffer,
            uint nOutBufferSize,
            out uint lpBytesReturned,
            IntPtr lpOverlapped);

        private const uint FSCTL_ALLOW_EXTENDED_DASD_IO = 0x00090083;
        private const uint FSCTL_LOCK_VOLUME = 0x00090018;
        private const uint FSCTL_UNLOCK_VOLUME = 0x0009001C;

        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;

        private const uint FILE_FLAG_SEQUENTIAL_SCAN = 0x08000000;
        private const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x002D1400;
        private const int SECTOR_SIZE = 4096;

        #endregion

        #region Hardware Discovery & VSS Assessment

        public static List<DriveTarget> GetAvailableDrives()
        {
            var list = new List<DriveTarget>();

            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady || (drive.DriveType != DriveType.Fixed && drive.DriveType != DriveType.Removable))
                    continue;

                string letter = drive.Name.TrimEnd('\\');
                bool isSsd = CheckIfDriveIsSsd(letter);

                list.Add(new DriveTarget
                {
                    DriveLetter = letter,
                    VolumeLabel = string.IsNullOrEmpty(drive.VolumeLabel) ? "Local Disk" : drive.VolumeLabel,
                    FileSystem = drive.DriveFormat,
                    TotalBytes = drive.TotalSize,
                    FreeBytes = drive.TotalFreeSpace,
                    IsSsd = isSsd,
                    IsTrimEnabled = isSsd
                });
            }
            return list;
        }

        public static List<string> GetVssSnapshots(string driveLetter)
        {
            var snapshots = new List<string>();
            try
            {
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "vssadmin",
                        Arguments = $"list shadows /for={driveLetter}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                proc.Start();
                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();

                string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    if (line.Contains("Shadow Copy Volume:"))
                    {
                        string path = line.Substring(line.IndexOf(@"\\?\GLOBALROOT")).Trim();
                        snapshots.Add(path);
                    }
                }
            }
            catch { }
            return snapshots;
        }

        private static bool CheckIfDriveIsSsd(string driveLetter)
        {
            try
            {
                using var handle = CreateFile($@"\\.\{driveLetter}", 0, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                if (handle.IsInvalid) return false;

                int querySize = Marshal.SizeOf(typeof(STORAGE_PROPERTY_QUERY));
                IntPtr queryPtr = Marshal.AllocHGlobal(querySize);
                Marshal.StructureToPtr(new STORAGE_PROPERTY_QUERY { PropertyId = 7, QueryType = 0 }, queryPtr, false);

                int descriptorSize = Marshal.SizeOf(typeof(DEVICE_SEEK_PENALTY_DESCRIPTOR));
                IntPtr descriptorPtr = Marshal.AllocHGlobal(descriptorSize);

                bool result = DeviceIoControl(handle, IOCTL_STORAGE_QUERY_PROPERTY, queryPtr, (uint)querySize, descriptorPtr, (uint)descriptorSize, out _, IntPtr.Zero);

                bool isSsd = false;
                if (result)
                {
                    var descriptor = Marshal.PtrToStructure<DEVICE_SEEK_PENALTY_DESCRIPTOR>(descriptorPtr);
                    isSsd = !descriptor.IncursSeekPenalty;
                }

                Marshal.FreeHGlobal(queryPtr);
                Marshal.FreeHGlobal(descriptorPtr);
                return isSsd;
            }
            catch { return false; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct STORAGE_PROPERTY_QUERY
        {
            public int PropertyId;
            public int QueryType;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
            public byte[] AdditionalParameters;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DEVICE_SEEK_PENALTY_DESCRIPTOR
        {
            public uint Version;
            public uint Size;
            [MarshalAs(UnmanagedType.I1)]
            public bool IncursSeekPenalty;
        }

        #endregion

        #region Low-Level Dual Scanning Engine

        public static async Task ScanDriveAsync(
            DriveTarget target,
            string volumePathOverride,
            ScanMode mode,
            FileCategory? targetCategory,
            Action<List<RecoveredItem>> onItemsDiscoveredBatch,
            Action<double> onProgressChanged,
            CancellationToken cancellationToken)
        {
            await Task.Run(async () =>
            {
                string volumePath = string.IsNullOrEmpty(volumePathOverride) ? $@"\\.\{target.DriveLetter}" : volumePathOverride;
                using var handle = CreateFile(volumePath, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_SEQUENTIAL_SCAN, IntPtr.Zero);

                if (handle.IsInvalid) throw new UnauthorizedAccessException($"Could not obtain raw volume access to {volumePath}. Ensure application is running as Administrator.");

                using var diskStream = new FileStream(handle, FileAccess.Read, 0, isAsync: false);

                const int bufferSize = 4 * 1024 * 1024;
                byte[] buffer = new byte[bufferSize];

                long totalBytes = target.TotalBytes;
                long bytesScanned = 0;
                int fileIndex = 1;

                var batch = new List<RecoveredItem>(256);
                var progressStopwatch = Stopwatch.StartNew();
                var batchStopwatch = Stopwatch.StartNew();

                while (bytesScanned < totalBytes)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    long remaining = totalBytes - bytesScanned;
                    int bytesToRead = (int)Math.Min((long)bufferSize, remaining);
                    bytesToRead = (bytesToRead / SECTOR_SIZE) * SECTOR_SIZE;

                    if (bytesToRead == 0) break;

                    int bytesRead = diskStream.Read(buffer, 0, bytesToRead);
                    if (bytesRead <= 0) break;

                    for (int offset = 0; offset < bytesRead; offset += SECTOR_SIZE)
                    {
                        var slice = buffer.AsSpan(offset, Math.Min(SECTOR_SIZE, bytesRead - offset));

                        if (slice.Length < 8 || MemoryMarshal.Read<ulong>(slice) == 0) continue;

                        if (mode == ScanMode.DeepScanSectorCarving)
                        {
                            if (DetectSignature(slice, out string ext, out FileCategory category, out long estimatedSize))
                            {
                                if (targetCategory.HasValue && category != targetCategory.Value) continue;

                                var status = EvaluateIntegrity(slice, target.IsTrimEnabled);
                                string? smartName = ExtractSmartName(slice, ext);
                                string finalName = smartName ?? $"Recovered_{category}_{fileIndex:D5}";

                                batch.Add(new RecoveredItem
                                {
                                    Name = finalName,
                                    Extension = ext,
                                    SizeBytes = estimatedSize,
                                    SectorOffset = bytesScanned + offset,
                                    Category = category,
                                    Status = status
                                });

                                fileIndex++;
                            }
                        }
                        else if (mode == ScanMode.MftQuickScan)
                        {
                            if (slice.Length > 0x38 && slice[0] == 0x46 && slice[1] == 0x49 && slice[2] == 0x4C && slice[3] == 0x45)
                            {
                                ushort flags = MemoryMarshal.Read<ushort>(slice.Slice(0x16, 2));

                                if (flags == 0x00 || flags == 0x02)
                                {
                                    string originalName = ExtractMftName(slice);
                                    string ext = ".dat";

                                    if (!string.IsNullOrEmpty(originalName))
                                        ext = Path.GetExtension(originalName);

                                    FileCategory category = GetCategoryFromExtension(ext);

                                    if (targetCategory.HasValue && category != targetCategory.Value) continue;

                                    batch.Add(new RecoveredItem
                                    {
                                        Name = string.IsNullOrEmpty(originalName) ? $"Orphaned_MFT_Record_{fileIndex}" : Path.GetFileNameWithoutExtension(originalName),
                                        Extension = ext,
                                        SizeBytes = 1048576,
                                        SectorOffset = bytesScanned + offset,
                                        Category = category,
                                        Status = RecoveryIntegrityStatus.Recoverable
                                    });
                                    fileIndex++;
                                }
                            }
                        }
                    }

                    bytesScanned += bytesRead;

                    if (batchStopwatch.ElapsedMilliseconds >= 250 && batch.Count > 0)
                    {
                        var itemsToSend = new List<RecoveredItem>(batch);
                        batch.Clear();
                        onItemsDiscoveredBatch(itemsToSend);
                        batchStopwatch.Restart();
                    }

                    if (progressStopwatch.ElapsedMilliseconds >= 100)
                    {
                        double progressPct = (double)bytesScanned / totalBytes * 100.0;
                        onProgressChanged(progressPct);
                        progressStopwatch.Restart();

                        await Task.Delay(1, cancellationToken);
                    }
                }

                if (batch.Count > 0) onItemsDiscoveredBatch(new List<RecoveredItem>(batch));
                onProgressChanged(100.0);
            }, cancellationToken);
        }

        #endregion

        #region Recovery Extractors, Shredder & Visual Previewer

        public static async Task<byte[]> GetFilePreviewBytesAsync(string volumePath, RecoveredItem item, int maxBytes = 5242880)
        {
            return await Task.Run(() =>
            {
                using var handle = CreateFile(volumePath, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_SEQUENTIAL_SCAN, IntPtr.Zero);
                if (handle.IsInvalid) return Array.Empty<byte>();

                using var diskStream = new FileStream(handle, FileAccess.Read, 0, isAsync: false);
                diskStream.Seek(item.SectorOffset, SeekOrigin.Begin);

                int bytesToRead = (int)Math.Min(item.SizeBytes, maxBytes);
                int alignedReadSize = (int)Math.Ceiling(bytesToRead / (double)SECTOR_SIZE) * SECTOR_SIZE;

                byte[] readBuffer = new byte[alignedReadSize];
                int read = diskStream.Read(readBuffer, 0, alignedReadSize);

                if (read == 0) return Array.Empty<byte>();

                byte[] exactBuffer = new byte[Math.Min(bytesToRead, read)];
                Array.Copy(readBuffer, exactBuffer, exactBuffer.Length);
                return exactBuffer;
            });
        }

        public static async Task ExtractFileAsync(string volumePath, RecoveredItem item, string destinationDirectory, CancellationToken token)
        {
            await Task.Run(async () =>
            {
                using var handle = CreateFile(volumePath, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_SEQUENTIAL_SCAN, IntPtr.Zero);

                if (handle.IsInvalid) throw new IOException("Unable to open source volume for recovery.");

                using var diskStream = new FileStream(handle, FileAccess.Read, 0, isAsync: false);
                diskStream.Seek(item.SectorOffset, SeekOrigin.Begin);

                string destPath = Path.Combine(destinationDirectory, item.Name + item.Extension);

                using var destStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: false);

                long bytesRemaining = item.SizeBytes;
                var delayStopwatch = Stopwatch.StartNew();

                while (bytesRemaining > 0)
                {
                    token.ThrowIfCancellationRequested();

                    int bytesWanted = (int)Math.Min(1024 * 1024, bytesRemaining);
                    int alignedReadSize = (int)Math.Ceiling(bytesWanted / (double)SECTOR_SIZE) * SECTOR_SIZE;

                    byte[] readBuffer = new byte[alignedReadSize];
                    int read = diskStream.Read(readBuffer, 0, alignedReadSize);

                    if (read == 0) break;

                    int writeSize = Math.Min(bytesWanted, read);
                    destStream.Write(readBuffer, 0, writeSize);
                    bytesRemaining -= writeSize;

                    if (delayStopwatch.ElapsedMilliseconds >= 100)
                    {
                        await Task.Delay(1, token);
                        delayStopwatch.Restart();
                    }
                }
            }, token);
        }

        public static async Task ShredFileAsync(string volumePath, RecoveredItem item, CancellationToken token)
        {
            await Task.Run(() =>
            {
                using var handle = CreateFile(volumePath, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);

                if (handle.IsInvalid)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    throw new IOException($"Unable to open source volume. Win32 Error: {errorCode}. Ensure the app is running as Administrator.");
                }

                DeviceIoControl(handle, FSCTL_LOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero);

                DeviceIoControl(handle, FSCTL_ALLOW_EXTENDED_DASD_IO, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero);

                using var diskStream = new FileStream(handle, FileAccess.Write, 0, isAsync: false);
                diskStream.Seek(item.SectorOffset, SeekOrigin.Begin);

                long bytesRemaining = item.SizeBytes;

                int writeChunkSize = 1024 * 1024;
                byte[] zeroBuffer = new byte[writeChunkSize];

                while (bytesRemaining > 0)
                {
                    token.ThrowIfCancellationRequested();

                    int bytesWanted = (int)Math.Min(writeChunkSize, bytesRemaining);
                    int alignedWriteSize = (int)Math.Ceiling(bytesWanted / (double)SECTOR_SIZE) * SECTOR_SIZE;

                    if (alignedWriteSize > zeroBuffer.Length)
                    {
                        zeroBuffer = new byte[alignedWriteSize];
                    }

                    try
                    {
                        diskStream.Write(zeroBuffer, 0, alignedWriteSize);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        throw new UnauthorizedAccessException("Windows blocked the raw sector destruction because this drive is currently active (e.g., your C: drive). Targeted raw shredding is only available for secondary drives or USBs. To securely erase data on your C: drive, please use the 'Wipe Free Space' utility.");
                    }

                    bytesRemaining -= bytesWanted;
                }
            }, token);
        }

        public static string GenerateAdvancedPreview(byte[] data, RecoveredItem item)
        {
            try
            {
                if (item.Category == FileCategory.Document)
                {
                    if (item.Extension == ".txt" || item.Extension == ".csv" || item.Extension == ".json" || item.Extension == ".xml" || item.Extension == ".rtf")
                    {
                        string text = Encoding.UTF8.GetString(data.Take(3000).ToArray());
                        return "--- TEXT DOCUMENT PREVIEW ---\n\n" + text;
                    }
                }
                else if (item.Category == FileCategory.Video || item.Category == FileCategory.Audio)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine($"--- MEDIA METADATA PREVIEW ---");
                    sb.AppendLine($"Format: {item.Extension.ToUpper().Replace(".", "")}");
                    sb.AppendLine($"Estimated Size: {item.SizeBytes / 1024} KB");

                    if (item.Extension == ".mp4" || item.Extension == ".mov")
                    {
                        int mvhd = data.AsSpan().IndexOf(Encoding.ASCII.GetBytes("mvhd"));
                        if (mvhd != -1 && mvhd + 24 < data.Length)
                        {
                            uint timeScale = (uint)((data[mvhd + 12] << 24) | (data[mvhd + 13] << 16) | (data[mvhd + 14] << 8) | data[mvhd + 15]);
                            uint duration = (uint)((data[mvhd + 16] << 24) | (data[mvhd + 17] << 16) | (data[mvhd + 18] << 8) | data[mvhd + 19]);
                            if (timeScale > 0)
                            {
                                TimeSpan ts = TimeSpan.FromSeconds(duration / (double)timeScale);
                                sb.AppendLine($"Parsed Duration: {ts:hh\\:mm\\:ss}");
                            }
                        }
                    }
                    else if (item.Extension == ".avi")
                    {
                        int strf = data.AsSpan().IndexOf(Encoding.ASCII.GetBytes("strf"));
                        if (strf != -1 && strf + 16 < data.Length)
                        {
                            int width = BitConverter.ToInt32(data, strf + 12);
                            int height = BitConverter.ToInt32(data, strf + 16);
                            if (width > 0 && height > 0)
                            {
                                sb.AppendLine($"Parsed Resolution: {width}x{height}");
                            }
                        }
                    }

                    return sb.ToString();
                }
            }
            catch { }

            return "HEX_FALLBACK";
        }

        #endregion

        #region Parsing Helpers, Smart Naming & Signatures

        private static readonly byte[] TagPdfTitle = Encoding.ASCII.GetBytes("/Title (");
        private static readonly byte[] TagMp3Tit2 = Encoding.ASCII.GetBytes("TIT2");
        private static readonly byte[] TagApple = Encoding.ASCII.GetBytes("Apple");
        private static readonly byte[] TagSamsung = Encoding.ASCII.GetBytes("SAMSUNG");
        private static readonly byte[] TagCanon = Encoding.ASCII.GetBytes("Canon");
        private static readonly byte[] TagNikon = Encoding.ASCII.GetBytes("Nikon");
        private static readonly byte[] TagSony = Encoding.ASCII.GetBytes("SONY");

        private static string? ExtractSmartName(ReadOnlySpan<byte> slice, string ext)
        {
            try
            {
                if (ext == ".pdf")
                {
                    int idx = slice.IndexOf(TagPdfTitle);
                    if (idx != -1)
                    {
                        idx += TagPdfTitle.Length;
                        int endIdx = slice.Slice(idx).IndexOf((byte)0x29); // ')'
                        if (endIdx > 0 && endIdx < 100)
                        {
                            string title = Encoding.ASCII.GetString(slice.Slice(idx, endIdx));
                            return CleanFileName(title);
                        }
                    }
                }
                else if (ext == ".mp3")
                {
                    int idx = slice.IndexOf(TagMp3Tit2);
                    if (idx != -1)
                    {
                        int size = (slice[idx + 4] << 24) | (slice[idx + 5] << 16) | (slice[idx + 6] << 8) | slice[idx + 7];
                        if (size > 1 && size < 200 && idx + 11 + size - 1 <= slice.Length)
                        {
                            byte encoding = slice[idx + 10];
                            int textLen = size - 1;
                            var textSlice = slice.Slice(idx + 11, textLen);
                            string title = encoding == 1
                                ? Encoding.Unicode.GetString(textSlice).Replace("\0", "")
                                : Encoding.ASCII.GetString(textSlice).Replace("\0", "");
                            if (!string.IsNullOrWhiteSpace(title)) return CleanFileName(title);
                        }
                    }
                }
                else if (ext == ".jpg" || ext == ".heic" || ext == ".dng")
                {
                    if (slice.IndexOf(TagApple) != -1) return $"IMG_Apple_{GetShortId()}";
                    if (slice.IndexOf(TagSamsung) != -1) return $"IMG_Samsung_{GetShortId()}";
                    if (slice.IndexOf(TagCanon) != -1) return $"IMG_Canon_{GetShortId()}";
                    if (slice.IndexOf(TagNikon) != -1) return $"IMG_Nikon_{GetShortId()}";
                    if (slice.IndexOf(TagSony) != -1) return $"IMG_Sony_{GetShortId()}";
                }
                else if (ext == ".mov" || ext == ".mp4")
                {
                    if (slice.IndexOf(TagApple) != -1) return $"VID_Apple_{GetShortId()}";
                }
            }
            catch { }
            return null;
        }

        private static string GetShortId()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 5).ToUpper();
        }

        private static string? CleanFileName(string input)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            string clean = new string(input.Where(c => !invalidChars.Contains(c)).ToArray());
            return string.IsNullOrWhiteSpace(clean) ? null : clean.Trim();
        }

        private static string ExtractMftName(ReadOnlySpan<byte> record)
        {
            try
            {
                ushort attrOffset = MemoryMarshal.Read<ushort>(record.Slice(0x38, 2));
                while (attrOffset < record.Length - 12)
                {
                    uint attrType = MemoryMarshal.Read<uint>(record.Slice(attrOffset, 4));
                    if (attrType == 0xFFFFFFFF) break;

                    uint attrLength = MemoryMarshal.Read<uint>(record.Slice(attrOffset + 4, 4));
                    if (attrLength == 0 || attrOffset + attrLength > record.Length) break;

                    if (attrType == 0x30)
                    {
                        byte nameLength = record[attrOffset + 0x58];
                        if (nameLength > 0 && attrOffset + 0x5A + (nameLength * 2) < record.Length)
                        {
                            return Encoding.Unicode.GetString(record.Slice(attrOffset + 0x5A, nameLength * 2));
                        }
                    }
                    attrOffset += (ushort)attrLength;
                }
            }
            catch { }
            return string.Empty;
        }

        private static bool DetectSignature(ReadOnlySpan<byte> data, out string extension, out FileCategory category, out long estimatedSize)
        {
            extension = "";
            category = FileCategory.Other;
            estimatedSize = 1048576; // 1 MB default estimate

            if (data.Length < 16) return false;

            #region Picture Formats
            // JPEG
            if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            {
                extension = ".jpg"; category = FileCategory.Picture; estimatedSize = 2097152; return true;
            }
            // PNG
            if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47 &&
                data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A)
            {
                extension = ".png"; category = FileCategory.Picture; estimatedSize = 1572864; return true;
            }
            // BMP
            if (data[0] == 0x42 && data[1] == 0x4D)
            {
                uint reportedSize = MemoryMarshal.Read<uint>(data.Slice(2, 4));
                if (reportedSize > 14 && reportedSize < 100 * 1024 * 1024)
                {
                    extension = ".bmp"; category = FileCategory.Picture; estimatedSize = reportedSize; return true;
                }
            }
            // GIF
            if (data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x38)
            {
                extension = ".gif"; category = FileCategory.Picture; estimatedSize = 1048576; return true;
            }
            // WebP
            if (data.Length > 12 && data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46 &&
                data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50)
            {
                extension = ".webp"; category = FileCategory.Picture; estimatedSize = 2097152; return true;
            }
            // HEIC / HEIF
            if (data.Length > 12 && data[4] == 0x66 && data[5] == 0x74 && data[6] == 0x79 && data[7] == 0x70 &&
                ((data[8] == 0x68 && data[9] == 0x65 && data[10] == 0x69 && data[11] == 0x63) ||
                 (data[8] == 0x6D && data[9] == 0x69 && data[10] == 0x66 && data[11] == 0x31) ||
                 (data[8] == 0x68 && data[9] == 0x65 && data[10] == 0x69 && data[11] == 0x78)))
            {
                extension = ".heic"; category = FileCategory.Picture; estimatedSize = 3145728; return true;
            }
            // DNG
            if ((data[0] == 0x49 && data[1] == 0x49 && data[2] == 0x2A && data[3] == 0x00) ||
                (data[0] == 0x4D && data[1] == 0x4D && data[2] == 0x00 && data[3] == 0x2A))
            {
                extension = ".dng"; category = FileCategory.Picture; estimatedSize = 26214400; return true;
            }
            #endregion

            #region Document Formats
            // PDF
            if (data[0] == 0x25 && data[1] == 0x50 && data[2] == 0x44 && data[3] == 0x46)
            {
                extension = ".pdf"; category = FileCategory.Document; estimatedSize = 3145728; return true;
            }
            // DOC
            if (data[0] == 0xD0 && data[1] == 0xCF && data[2] == 0x11 && data[3] == 0xE0 &&
                data[4] == 0xA1 && data[5] == 0xB1 && data[6] == 0x1A && data[7] == 0xE1)
            {
                extension = ".doc"; category = FileCategory.Document; estimatedSize = 4194304; return true;
            }
            // RTF
            if (data[0] == 0x7B && data[1] == 0x5C && data[2] == 0x72 && data[3] == 0x74 && data[4] == 0x66)
            {
                extension = ".rtf"; category = FileCategory.Document; estimatedSize = 1048576; return true;
            }
            #endregion

            #region Archive Formats
            // ZIP
            if (data[0] == 0x50 && data[1] == 0x4B && data[2] == 0x03 && data[3] == 0x04)
            {
                extension = ".zip"; category = FileCategory.Archive; estimatedSize = 5242880; return true;
            }
            // RAR
            if (data[0] == 0x52 && data[1] == 0x61 && data[2] == 0x72 && data[3] == 0x21 && data[4] == 0x1A && data[5] == 0x07)
            {
                extension = ".rar"; category = FileCategory.Archive; estimatedSize = 10485760; return true;
            }
            // 7z
            if (data[0] == 0x37 && data[1] == 0x7A && data[2] == 0xBC && data[3] == 0xAF && data[4] == 0x27 && data[5] == 0x1C)
            {
                extension = ".7z"; category = FileCategory.Archive; estimatedSize = 10485760; return true;
            }
            #endregion

            #region Video Formats
            // MP4
            if (data.Length > 8 && data[4] == 0x66 && data[5] == 0x74 && data[6] == 0x79 && data[7] == 0x70)
            {
                extension = ".mp4"; category = FileCategory.Video; estimatedSize = 20971520; return true;
            }
            // MOV
            if (data.Length > 12 && data[4] == 0x66 && data[5] == 0x74 && data[6] == 0x79 && data[7] == 0x70 &&
                data[8] == 0x71 && data[9] == 0x74 && data[10] == 0x20 && data[11] == 0x20)
            {
                extension = ".mov"; category = FileCategory.Video; estimatedSize = 31457280; return true;
            }
            // AVI
            if (data.Length > 11 && data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46 &&
                data[8] == 0x41 && data[9] == 0x56 && data[10] == 0x49 && data[11] == 0x20)
            {
                extension = ".avi"; category = FileCategory.Video; estimatedSize = 25165824; return true;
            }
            // MKV
            if (data[0] == 0x1A && data[1] == 0x45 && data[2] == 0xDF && data[3] == 0xA3)
            {
                extension = ".mkv"; category = FileCategory.Video; estimatedSize = 31457280; return true;
            }
            #endregion

            #region Audio Formats
            // MP3
            if (data[0] == 0x49 && data[1] == 0x44 && data[2] == 0x33)
            {
                extension = ".mp3"; category = FileCategory.Audio; estimatedSize = 5242880; return true;
            }
            // WAV
            if (data.Length > 11 && data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46 &&
                data[8] == 0x57 && data[9] == 0x41 && data[10] == 0x56 && data[11] == 0x45)
            {
                extension = ".wav"; category = FileCategory.Audio; estimatedSize = 10485760; return true;
            }
            // FLAC
            if (data[0] == 0x66 && data[1] == 0x4C && data[2] == 0x61 && data[3] == 0x43)
            {
                extension = ".flac"; category = FileCategory.Audio; estimatedSize = 20971520; return true;
            }
            // OGG
            if (data[0] == 0x4F && data[1] == 0x67 && data[2] == 0x67 && data[3] == 0x53)
            {
                extension = ".ogg"; category = FileCategory.Audio; estimatedSize = 5242880; return true;
            }
            #endregion

            return false;
        }

        private static FileCategory GetCategoryFromExtension(string ext)
        {
            ext = ext.ToLower();
            if (ext == ".jpg" || ext == ".png" || ext == ".bmp" || ext == ".gif" || ext == ".webp" || ext == ".tiff" || ext == ".heic" || ext == ".dng") return FileCategory.Picture;
            if (ext == ".pdf" || ext == ".doc" || ext == ".docx" || ext == ".xlsx" || ext == ".txt" || ext == ".rtf") return FileCategory.Document;
            if (ext == ".mp4" || ext == ".avi" || ext == ".mkv" || ext == ".mov") return FileCategory.Video;
            if (ext == ".mp3" || ext == ".wav" || ext == ".flac" || ext == ".ogg") return FileCategory.Audio;
            if (ext == ".zip" || ext == ".rar" || ext == ".7z" || ext == ".tar" || ext == ".gz") return FileCategory.Archive;
            return FileCategory.Other;
        }

        private static RecoveryIntegrityStatus EvaluateIntegrity(ReadOnlySpan<byte> slice, bool isTrimDevice)
        {
            if (isTrimDevice)
            {
                int nonZeroCount = 0;
                for (int i = 64; i < Math.Min(512, slice.Length); i++)
                {
                    if (slice[i] != 0x00) nonZeroCount++;
                }

                if (nonZeroCount < 20) return RecoveryIntegrityStatus.Unrecoverable;
            }
            return RecoveryIntegrityStatus.Recoverable;
        }

        #endregion
    }
}