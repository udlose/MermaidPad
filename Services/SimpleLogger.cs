using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

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

        WriteEntry(entry);
        Debug.WriteLine(entry);
    }

    /// <summary>
    /// Log an error with exception details
    /// </summary>
    public static void LogError(string message, Exception? ex = null, [CallerMemberName] string? caller = null, [CallerFilePath] string? file = null)
    {
        string fileName = file is not null ? Path.GetFileNameWithoutExtension(file) : "Unknown";
        StringBuilder sb = new StringBuilder(512);
        sb.Append($"[{DateTime.Now:HH:mm:ss.fff}] [ERROR] [{fileName}.{caller}] {message}");

        if (ex is not null)
        {
            sb.Append($"\n    Exception: {ex.GetType().Name}: {ex.Message}");
            if (ex.StackTrace is not null)
            {
                int idx = ex.StackTrace.AsSpan().IndexOf('\n');
                ReadOnlySpan<char> firstLine = idx >= 0 ? ex.StackTrace.AsSpan()[..idx].Trim() : ex.StackTrace.AsSpan().Trim();
                sb.Append($"\n    Stack: {firstLine}");
            }
        }

        string entry = sb.ToString();
        WriteEntry(entry);
        Debug.WriteLine(entry);
    }

    /// <summary>
    /// Log WebView specific events
    /// </summary>
    public static void LogWebView(string eventName, string? details = null, [CallerMemberName] string? caller = null)
    {
        string message = details?.Length > 0
            ? $"WebView {eventName}: {details}"
            : $"WebView {eventName}";
        Log(message, caller);
    }

    /// <summary>
    /// Log JavaScript execution attempts
    /// </summary>
    public static void LogJavaScript(string script, bool success, string? result = null, [CallerMemberName] string? caller = null)
    {
        string truncatedScript = script.Length > 100 ? script[..100] + "..." : script;
        string status = success ? "SUCCESS" : "FAILED";
        StringBuilder sb = new StringBuilder(256);
        sb.Append($"JavaScript {status}: {truncatedScript}");
        if (!string.IsNullOrEmpty(result))
        {
            sb.Append($" → {result}");
        }
        Log(sb.ToString(), caller);
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
        StringBuilder sb = new StringBuilder(128);
        sb.Append($"Asset {operation}: {assetName}");
        if (success)
        {
            if (sizeBytes.HasValue)
            {
                sb.Append($" ({sizeBytes.Value:N0} bytes)");
            }
            sb.Append(" ✓");
        }
        else
        {
            sb.Append(" ✗");
        }
        Log(sb.ToString(), caller);
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
        StringBuilder sb = new StringBuilder(256);
        sb.AppendLine("============================================");
        sb.AppendLine("MermaidPad Debug Session Started");
        sb.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"OS: {Environment.OSVersion}");
        sb.AppendLine($".NET: {Environment.Version}");
        sb.AppendLine($"Working Directory: {Environment.CurrentDirectory}");
        sb.AppendLine($"Log File: {_logPath}");
        sb.AppendLine("============================================");
        sb.AppendLine();

        WriteEntry(sb.ToString());
    }

    private static void WriteEntry(string entry)
    {
        lock (_lockObject)
        {
            File.AppendAllText(_logPath, entry + Environment.NewLine);
        }
    }
}