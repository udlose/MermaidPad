using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace MermaidPad.Services;

/// <summary>
/// Dead simple file logger for debugging WebView issues.
/// Uses lock file coordination to prevent conflicts between multiple instances.
/// </summary>
public static class SimpleLogger
{
    private static readonly string _logPath;
    private static readonly string _lockPath;
    private const int InitialRetryDelayMs = 25; // Initial delay for retrying lock acquisition

    static SimpleLogger()
    {
        // Use same directory pattern as SettingsService
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string baseDir = Path.Combine(appData, "MermaidPad");
        Directory.CreateDirectory(baseDir);

        _logPath = Path.Combine(baseDir, "debug.log");
        _lockPath = Path.Combine(baseDir, "debug.log.lock");

        // Clean up any stale lock files from previous crashes
        CleanupStaleLockFile();

        // Write session header
        WriteSessionHeader();
    }

    public static void Log(string message, [CallerMemberName] string? caller = null, [CallerFilePath] string? file = null)
    {
        string fileName = file is not null ? Path.GetFileNameWithoutExtension(file) : "Unknown";
        string entry = $"[{GetTimestamp()}] [{fileName}.{caller}] {message}";

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
        sb.Append($"[{GetTimestamp()}] [ERROR] [{fileName}.{caller}] {message}");

        if (ex is not null)
        {
            sb.Append($"{Environment.NewLine}    Exception: {ex.GetType().Name}: {ex.Message}");
            if (ex.StackTrace is not null)
            {
                int idx = ex.StackTrace.AsSpan().IndexOf('\n');
                ReadOnlySpan<char> firstLine = idx >= 0 ? ex.StackTrace.AsSpan()[..idx].Trim() : ex.StackTrace.AsSpan().Trim();
                sb.Append($"{Environment.NewLine}    Stack: {firstLine}");
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
        string message = !string.IsNullOrEmpty(details)
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
            sb.Append($" => {result}");
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
        try
        {
            WriteEntryWithLock(""); // This will clear the file
            WriteSessionHeader();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to clear log: {ex.Message}");
        }

        Log("Log file cleared");
    }

    private static void WriteSessionHeader()
    {
        StringBuilder sb = new StringBuilder(256);
        sb.AppendLine("============================================");
        sb.AppendLine("MermaidPad Debug Session Started");
        sb.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Process ID: {Environment.ProcessId}");
        sb.AppendLine($"OS: {Environment.OSVersion}");
        sb.AppendLine($".NET: {Environment.Version}");
        sb.AppendLine($"Working Directory: {Environment.CurrentDirectory}");
        sb.AppendLine($"Log File: {_logPath}");
        sb.AppendLine($"Lock File: {_lockPath}");
        sb.AppendLine("============================================");
        sb.AppendLine();

        WriteEntryWithLock(sb.ToString());
    }

    private static void WriteEntry(string entry)
    {
        WriteEntryWithLock(entry + Environment.NewLine);
    }

    private static void WriteEntryWithLock(string content)
    {
        const int maxRetries = 3;
        int delay = InitialRetryDelayMs; // Start with initial delay

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            FileStream? lockStream = null;
            try
            {
                // Try to acquire the lock file
                lockStream = TryAcquireLogLock();
                if (lockStream is not null)
                {
                    try
                    {
                        // Successfully got the lock, now write to the log file
                        WriteToLogFile(content);
                        return; // Success!
                    }
                    finally
                    {
                        // Always release the lock
                        ReleaseLogLock(lockStream);
                    }
                }

                // Couldn't get the lock, wait before retrying (unless this is the last attempt)
                if (attempt < maxRetries - 1)
                {
                    Thread.Sleep(delay);
                    delay *= 2; // Exponential backoff: 25ms, 50ms, 100ms
                }
            }
            catch (Exception ex)
            {
                // If we got an exception after acquiring the lock, make sure to release it
                if (lockStream is not null)
                {
                    try
                    {
                        ReleaseLogLock(lockStream);
                    }
                    catch
                    {
                        // Ignore errors during lock release
                    }
                }

                // For the last attempt, don't retry
                if (attempt == maxRetries - 1)
                {
                    Debug.WriteLine($"Failed to write to log after {maxRetries} attempts: {ex.Message}");
                    break;
                }

                // Wait before retrying
                Thread.Sleep(delay);
                delay *= 2;
            }
        }

        // All retries failed, fall back to Debug.WriteLine
        Debug.WriteLine($"[LOG-FALLBACK] {content.Trim()}");
    }

    private static FileStream? TryAcquireLogLock()
    {
        try
        {
            // Try to create the lock file with exclusive access
            // FileShare.None ensures only one process can open it at a time
            FileStream lockStream = new FileStream(_lockPath, FileMode.Create, FileAccess.Write, FileShare.None);

            return lockStream;
        }
        catch (IOException)
        {
            // Another process has the lock, or file system error
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            // Permission denied
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Unexpected error acquiring log lock: {ex.Message}");
            return null;
        }
    }

    private static void ReleaseLogLock(FileStream lockStream)
    {
        try
        {
            // Close and dispose the lock stream
            lockStream.Dispose();

            // Clean up the lock file
            if (File.Exists(_lockPath))
            {
                File.Delete(_lockPath);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error cleaning up lock file: {ex.Message}");
        }
    }

    private static void WriteToLogFile(string content)
    {
        // If content is empty, clear the file instead of appending
        if (string.IsNullOrEmpty(content))
        {
            File.WriteAllText(_logPath, "");
        }
        else
        {
            File.AppendAllText(_logPath, content);
        }
    }

    private static string GetTimestamp() => DateTime.Now.ToString("HH:mm:ss.fff");

    private static void CleanupStaleLockFile()
    {
        try
        {
            if (File.Exists(_lockPath))
            {
                File.Delete(_lockPath);
                Debug.WriteLine("Cleaned up stale lock file from previous session");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to cleanup stale lock file: {ex.Message}");
            // Not critical - continue with normal operation
        }
    }
}