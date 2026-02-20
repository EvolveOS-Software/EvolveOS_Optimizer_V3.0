using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using static EvolveOS_Optimizer.Core.Structs;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    internal static class ConPtyProcessRunner
    {
        public static async Task<int> RunAsync(string commandLine, Action<string> outputCallback, CancellationToken ct = default, Action<int>? processIdCallback = null)
        {
            SafeFileHandle? inputRead = null, inputWrite = null, outputRead = null, outputWrite = null;
            var pty = IntPtr.Zero;
            var attrList = IntPtr.Zero;
            var proc = new PROCESS_INFORMATION();
            int processId;

            try
            {
                if (!Win32Helper.CreatePipe(out inputRead, out inputWrite, IntPtr.Zero, 0) ||
                    !Win32Helper.CreatePipe(out outputRead, out outputWrite, IntPtr.Zero, 0))
                {
                    throw new InvalidOperationException($"CreatePipe failed: {Marshal.GetLastWin32Error()}");
                }

                var hr = Win32Helper.CreatePseudoConsole(new COORD { X = 120, Y = 30 }, inputRead, outputWrite, 0, out pty);
                if (hr != 0)
                {
                    throw new InvalidOperationException($"CreatePseudoConsole failed: {hr}");
                }

                var size = IntPtr.Zero;
                Win32Helper.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
                attrList = Marshal.AllocHGlobal(size);

                if (!Win32Helper.InitializeProcThreadAttributeList(attrList, 1, 0, ref size) ||
                    !Win32Helper.UpdateProcThreadAttribute(attrList, 0, Win32Helper.PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE, pty, nint.Size, IntPtr.Zero, IntPtr.Zero))
                {
                    throw new InvalidOperationException($"Attribute list setup failed: {Marshal.GetLastWin32Error()}");
                }

                var si = new STARTUPINFOEX { lpAttributeList = attrList };
                si.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();

                if (!Win32Helper.CreateProcessW(null, commandLine, IntPtr.Zero, IntPtr.Zero, false, Win32Helper.EXTENDED_STARTUPINFO_PRESENT, IntPtr.Zero, null, ref si, out proc))
                {
                    throw new InvalidOperationException($"CreateProcessW failed: {Marshal.GetLastWin32Error()}");
                }

                processId = proc.dwProcessId;

                processIdCallback?.Invoke(processId);

                outputWrite.Dispose();
                outputWrite = null;
                inputRead.Dispose();
                inputRead = null;

                using var process = Process.GetProcessById(processId);

                var readTask = ReadOutputAsync(outputRead, outputCallback, ct);

                try
                {
                    await process.WaitForExitAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    await ProcessTerminator.KillProcessTreeAsync(processId);
                    throw;
                }

                if (pty != IntPtr.Zero)
                {
                    Win32Helper.ClosePseudoConsole(pty);
                    pty = IntPtr.Zero;
                }

                await Task.WhenAny(readTask, Task.Delay(1000));

                Win32Helper.GetExitCodeProcess(proc.hProcess, out var exitCode);
                return (int)exitCode;
            }
            finally
            {
                if (proc.hProcess != IntPtr.Zero) Win32Helper.CloseHandle(proc.hProcess);
                if (proc.hThread != IntPtr.Zero) Win32Helper.CloseHandle(proc.hThread);
                if (attrList != IntPtr.Zero) { Win32Helper.DeleteProcThreadAttributeList(attrList); Marshal.FreeHGlobal(attrList); }
                if (pty != IntPtr.Zero) Win32Helper.ClosePseudoConsole(pty);
                inputRead?.Dispose();
                inputWrite?.Dispose();
                outputRead?.Dispose();
                outputWrite?.Dispose();
            }
        }

        private static async Task ReadOutputAsync(SafeFileHandle handle, Action<string> callback, CancellationToken ct)
        {
            using var stream = new FileStream(handle, FileAccess.Read, 4096, false);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            var buffer = new char[256];
            var line = new StringBuilder();

            while (!ct.IsCancellationRequested)
            {
                int read;
                try { read = await reader.ReadAsync(buffer, 0, buffer.Length); }
                catch { break; }
                if (read == 0) break;

                for (var i = 0; i < read; i++)
                {
                    if (buffer[i] is '\r' or '\n')
                    {
                        if (line.Length > 0)
                        {
                            var cleaned = AnsiStripper.StripAnsiSequences(line.ToString());
                            if (!string.IsNullOrWhiteSpace(cleaned))
                            {
                                callback(cleaned);
                            }
                            line.Clear();
                        }
                    }
                    else
                    {
                        line.Append(buffer[i]);
                    }
                }
            }

            if (line.Length > 0)
            {
                var cleaned = AnsiStripper.StripAnsiSequences(line.ToString());
                if (!string.IsNullOrWhiteSpace(cleaned))
                {
                    callback(cleaned);
                }
            }
        }
    }
}
