using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace EvolveOS_Optimizer.Utilities.Helpers;

internal static class ConsoleEncodingHelper
{
    private static readonly SemaphoreSlim SyncLock = new(1, 1);
    private static Encoding? _oemConsoleEncoding;

    private static readonly Regex _codePageRegex = new(@"(\d+)", RegexOptions.Compiled);

    private static readonly Lazy<string> CmdFullPath = new(() =>
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess ? "SysNative" : "System32",
            "cmd.exe");
    });

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetConsoleOutputCP();

    public static async Task<Encoding> GetOemConsoleEncodingAsync()
    {
        if (_oemConsoleEncoding != null) return _oemConsoleEncoding;

        await SyncLock.WaitAsync();
        try
        {
            if (_oemConsoleEncoding != null) return _oemConsoleEncoding;

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var codePage = (int)GetConsoleOutputCP();
            if (codePage == 0) codePage = await TryResolveCodePageFromChcpAsync();
            if (codePage == 0) codePage = GetFallbackOemCodePage();

            try
            {
                _oemConsoleEncoding = Encoding.GetEncoding(codePage);
            }
            catch
            {
                _oemConsoleEncoding = Encoding.UTF8;
            }

            return _oemConsoleEncoding;
        }
        finally
        {
            SyncLock.Release();
        }
    }

    private static async Task<int> TryResolveCodePageFromChcpAsync()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = CmdFullPath.Value,
                    Arguments = "/C \"chcp\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.ASCII
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            var match = _codePageRegex.Match(output);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var codePage))
            {
                return codePage;
            }
        }
        catch { }
        return 0;
    }

    private static int GetFallbackOemCodePage()
    {
        var ansiCodePage = CultureInfo.CurrentCulture.TextInfo.ANSICodePage;

        return ansiCodePage switch
        {
            1251 => 866,
            1252 => 437,
            1250 => 852,
            1253 => 737,
            1254 => 857,
            1255 => 862,
            1256 => 708,
            1257 => 775,
            1258 => 869,
            932 => 932,
            936 => 936,
            949 => 949,
            950 => 950,
            _ => 437
        };
    }
}