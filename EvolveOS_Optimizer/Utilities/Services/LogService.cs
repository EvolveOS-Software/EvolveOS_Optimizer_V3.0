// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using System.Text;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Enums;

namespace EvolveOS_Optimizer.Utilities.Services;

/// <summary>
/// A thread-safe file logging service for the EvolveOS Optimizer application.
/// </summary>
/// <remarks>
/// <para>
/// <b>Storage Location:</b>
/// Logs are written to <c>Environment.SpecialFolder.CommonApplicationData</c> (ProgramData) 
/// to ensure they are accessible regardless of whether the app is running as a standard user or elevated administrator.
/// </para>
/// <para>
/// <b>Lifecycle & Maintenance:</b>
/// The service automatically cleans up old log files based on age (30 days) and file count (max 50) 
/// during initialization. It also captures a comprehensive snapshot of system telemetry 
/// (OS version, RAM, CPU, Secure Boot status, etc.) at the beginning of every log file to aid in debugging.
/// </para>
/// </remarks>
public class LogService : ILogService, IDisposable
{
    #region Fields

    private readonly string _logPath;
    private StreamWriter? _logWriter;
    private readonly object _lockObject = new object();
    private IInteractiveUserService? _interactiveUserService;
    private ISystemInfoProvider? _systemInfoProvider;

    #endregion

    #region Constructor

    public LogService()
    {
        _logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EvolveOS Optimizer", "Logs", $"EvolveOS_Optimizer_Log_{DateTime.Now:yyyyMMdd_HHmmss}.log");
    }

    #endregion

    #region Dependency Setters

    public void SetInteractiveUserService(IInteractiveUserService interactiveUserService)
    {
        _interactiveUserService = interactiveUserService;
    }

    public void SetSystemInfoProvider(ISystemInfoProvider systemInfoProvider)
    {
        _systemInfoProvider = systemInfoProvider;
    }

    #endregion

    #region Lifecycle Management (Start / Stop / Dispose)

    public void StartLog()
    {
        try
        {
            var logDirectory = Path.GetDirectoryName(_logPath);
            if (logDirectory != null)
            {
                Directory.CreateDirectory(logDirectory);
            }
            else
            {
                throw new InvalidOperationException("Log directory path is null.");
            }

            CleanupOldLogs(logDirectory, maxAgeDays: 30, maxFiles: 50);

            _logWriter = new StreamWriter(_logPath, false, Encoding.UTF8)
            {
                AutoFlush = true
            };

            if (_systemInfoProvider != null)
            {
                var info = _systemInfoProvider.Collect();
                LogInformation($"==== EvolveOS Optimizer (Optimizations & Customizations) {info.AppVersion} Log Started ====");
                LogInformation($"OS:            {info.OperatingSystem}");
                LogInformation($"Architecture:  {info.Architecture}");
                LogInformation($"Device Type:   {info.DeviceType}");
                LogInformation($"CPU:           {info.Cpu}");
                LogInformation($"RAM:           {info.Ram}");
                LogInformation($"GPU:           {info.Gpu}");
                LogInformation($".NET Runtime:  {info.DotNetRuntime}");
                LogInformation($"Elevation:     {info.Elevation}");
                LogInformation($"Firmware:      {info.FirmwareType}");
                LogInformation($"Secure Boot:   {info.SecureBoot}");
                LogInformation($"TPM:           {info.Tpm}");
                LogInformation($"Domain Joined: {info.DomainJoined}");
            }
            else
            {
                LogInformation("==== EvolveOS Optimizer Log Started ====");
                LogInformation("System info unavailable (provider not configured)");
            }
            LogInformation("=====================================");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to start log at '{_logPath}': {ex.Message}", ex);
        }
    }

    private void StopLog()
    {
        lock (_lockObject)
        {
            try
            {
                LogInformation("==== EvolveOS Optimizer Log Ended ====");
                _logWriter?.Close();
                _logWriter?.Dispose();
            }
            catch (Exception)
            {
                // Error stopping log
            }
        }
    }

    public void Dispose()
    {
        StopLog();
        GC.SuppressFinalize(this);
    }

    #endregion

    #region Core Logging API

    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        switch (level)
        {
            case LogLevel.Info:
                LogInformation(message);
                break;
            case LogLevel.Warning:
                LogWarning(message);
                break;
            case LogLevel.Error:
                LogError(message, exception);
                break;
            case LogLevel.Success:
                LogSuccess(message);
                break;
            case LogLevel.Debug:
                LogDebug(message);
                break;
            default:
                LogInformation(message);
                break;
        }
    }

    public void LogInformation(string message)
    {
        WriteLog(message, "INFO");
    }

    public void LogWarning(string message)
    {
        WriteLog(message, "WARNING");
    }

    public void LogError(string message, Exception? exception = null)
    {
        string fullMessage = exception != null
            ? $"{message} - Exception: {exception.Message}\n{exception.StackTrace}"
            : message;
        WriteLog(fullMessage, "ERROR");
    }

    public void LogDebug(string message)
    {
        WriteLog(message, "DEBUG");
    }

    private void LogSuccess(string message)
    {
        WriteLog(message, "SUCCESS");
    }

    #endregion

    #region File Management & Internal Helpers

    public string GetLogPath()
    {
        return _logPath;
    }

    private void WriteLog(string message, string level)
    {
        lock (_lockObject)
        {
            try
            {
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
                _logWriter?.WriteLine(logEntry);
            }
            catch (Exception)
            {
                // Logging failed, suppress to avoid crashing the application
            }
        }
    }

    internal static void CleanupOldLogs(string logDirectory, int maxAgeDays = 30, int maxFiles = 50)
    {
        try
        {
            if (!Directory.Exists(logDirectory))
                return;

            var logFiles = Directory.GetFiles(logDirectory, "EvolveOS_Optimizer_Log_*.log")
                .Select(f => new FileInfo(f))
                .OrderBy(f => f.CreationTimeUtc)
                .ToList();

            var cutoff = DateTime.UtcNow.AddDays(-maxAgeDays);

            for (int i = logFiles.Count - 1; i >= 0; i--)
            {
                if (logFiles[i].CreationTimeUtc < cutoff)
                {
                    try { logFiles[i].Delete(); logFiles.RemoveAt(i); }
                    catch { /* Cleanup */ }
                }
            }

            while (logFiles.Count > maxFiles)
            {
                try { logFiles[0].Delete(); }
                catch { /* Cleanup */ }
                logFiles.RemoveAt(0);
            }
        }
        catch
        {
            // Cleanup
        }

        #endregion
    }
}