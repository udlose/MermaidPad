using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace MermaidPad.Services;

/// <summary>
/// Dead simple file logger for debugging WebView issues.
/// Just appends to a single log file with timestamps.
/// </summary>
public static class SimpleLogger
{
    private static readonly string _logPath;
    private static readonly Lock _lockObject = new Lock();

    static SimpleLogger()
    {
        // Use same directory pattern as SettingsService
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string baseDir = Path.Combine(appData, "MermaidPad");
        Directory.CreateDirectory(baseDir);

        _logPath = Path.Combine(baseDir, "debug.log");

        // Write session header
        WriteSessionHeader();
    }

    /// <summary>
    /// Log a simple message with timestamp
    /// </summary>
    public static void Log(string message, [CallerMemberName] string? caller = null, [CallerFilePath] string? file = null)
    {
        string fileName = file is not null ? Path.GetFileNameWithoutExtension(file) : "Unknown";
        string entry = $"[{DateTime.Now:HH:mm:ss.fff}] [{fileName}.{caller}] {message}";

        lock (_lockObject)
        {
            File.AppendAllText(_logPath, entry + Environment.NewLine);
        }

        // Also show in Visual Studio output window
        Debug.WriteLine(entry);
    }

    /// <summary>
    /// Log an error with exception details
    /// </summary>
    public static void LogError(string message, Exception? ex = null, [CallerMemberName] string? caller = null, [CallerFilePath] string? file = null)
    {
        string fileName = file is not null ? Path.GetFileNameWithoutExtension(file) : "Unknown";
        string entry = $"[{DateTime.Now:HH:mm:ss.fff}] [ERROR] [{fileName}.{caller}] {message}";

        if (ex is not null)
        {
            entry += $"\n    Exception: {ex.GetType().Name}: {ex.Message}";
            if (ex.StackTrace is not null)
            {
                entry += $"\n    Stack: {ex.StackTrace.Split('\n').FirstOrDefault()?.Trim()}";
            }
        }

        lock (_lockObject)
        {
            File.AppendAllText(_logPath, entry + Environment.NewLine);
        }

        Debug.WriteLine(entry);
    }

    /// <summary>
    /// Log WebView specific events
    /// </summary>
    public static void LogWebView(string eventName, string? details = null, [CallerMemberName] string? caller = null)
    {
        string message = $"WebView {eventName}";
        if (!string.IsNullOrEmpty(details))
        {
            message += $": {details}";
        }
        Log(message, caller);
    }

    /// <summary>
    /// Log JavaScript execution attempts
    /// </summary>
    public static void LogJavaScript(string script, bool success, string? result = null, [CallerMemberName] string? caller = null)
    {
        string truncatedScript = script.Length > 100 ? script[..100] + "..." : script;
        string status = success ? "SUCCESS" : "FAILED";
        string message = $"JavaScript {status}: {truncatedScript}";

        if (!string.IsNullOrEmpty(result))
        {
            message += $" → {result}";
        }

        Log(message, caller);
    }

    /// <summary>
    /// Log performance timing
    /// </summary>
    public static void LogTiming(string operation, TimeSpan elapsed, bool success = true, [CallerMemberName] string? caller = null)
    {
        string status = success ? "completed" : "failed";
        Log($"TIMING: {operation} {status} in {elapsed.TotalMilliseconds:F1}ms", caller);
    }

    /// <summary>
    /// Log asset operations (file loading, etc.)
    /// </summary>
    public static void LogAsset(string operation, string assetName, bool success, long? sizeBytes = null, [CallerMemberName] string? caller = null)
    {
        string message = $"Asset {operation}: {assetName}";
        if (success)
        {
            if (sizeBytes.HasValue)
            {
                message += $" ({sizeBytes:N0} bytes)";
            }
            message += " ✓";
        }
        else
        {
            message += " ✗";
        }

        Log(message, caller);
    }

    /// <summary>
    /// Get the current log file path (useful for showing user where logs are)
    /// </summary>
    public static string GetLogPath() => _logPath;

    /// <summary>
    /// Clear the log file (useful for starting fresh)
    /// </summary>
    public static void ClearLog()
    {
        lock (_lockObject)
        {
            File.WriteAllText(_logPath, "");
            WriteSessionHeader();
        }
        Log("Log file cleared");
    }

    private static void WriteSessionHeader()
    {
        string header = $"""
============================================
MermaidPad Debug Session Started
Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
OS: {Environment.OSVersion}
.NET: {Environment.Version}
Working Directory: {Environment.CurrentDirectory}
Log File: {_logPath}
============================================

""";

        lock (_lockObject)
        {
            File.AppendAllText(_logPath, header);
        }
    }
}
