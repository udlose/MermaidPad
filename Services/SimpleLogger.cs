using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace MermaidPad.Services;
/// <summary>
/// Dead simple file logger for debugging WebView issues.
/// Uses lock file coordination to prevent conflicts between multiple instances.
/// </summary>
public static class SimpleLogger
{
    /// <summary>
    /// Path to the log file.
    /// </summary>
    private static readonly string _logPath;

    /// <summary>
    /// Path to the lock file used for inter-process coordination.
    /// </summary>
    private static readonly string _lockPath;

    /// <summary>
    /// Initial delay in milliseconds for retrying lock acquisition.
    /// </summary>
    private const int InitialRetryDelayMs = 25;

    /// <summary>
    /// Maximum delay in milliseconds for retrying lock acquisition.
    /// </summary>
    private const int MaxRetryDelayMs = 1_000;

    /// <summary>
    /// Static constructor. Initializes log and lock file paths, cleans up stale lock files, and writes the session header.
    /// </summary>
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

    /// <summary>
    /// Logs a simple message with timestamp, caller, and file information.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="caller">The calling member name (autopopulated).</param>
    /// <param name="file">The calling file path (autopopulated).</param>
    public static void Log(string message, [CallerMemberName] string? caller = null, [CallerFilePath] string? file = null)
    {
        string fileName = file is not null ? Path.GetFileNameWithoutExtension(file) : "Unknown";
        string entry = $"[{GetTimestamp()}] [{fileName}.{caller}] {message}";

        WriteEntry(entry);
        Debug.WriteLine(entry);
    }

    /// <summary>
    /// Logs an error message with exception details, timestamp, caller, and file information.
    /// </summary>
    /// <param name="message">The error message to log.</param>
    /// <param name="ex">The exception to log (optional).</param>
    /// <param name="caller">The calling member name (autopopulated).</param>
    /// <param name="file">The calling file path (autopopulated).</param>
    public static void LogError(string message, Exception? ex = null, [CallerMemberName] string? caller = null, [CallerFilePath] string? file = null)
    {
        string fileName = file is not null ? Path.GetFileNameWithoutExtension(file) : "Unknown";
        StringBuilder sb = new StringBuilder(512);
        sb.Append($"[{GetTimestamp()}] [ERROR] [{fileName}.{caller}] {message}");

        if (ex is not null)
        {
            sb.Append($"{Environment.NewLine}    Exception: {ex.GetType().Name}: {ex.Message}");
            if (!string.IsNullOrWhiteSpace(ex.StackTrace))
            {
                ReadOnlySpan<char> stackTraceSpan = ex.StackTrace.AsSpan();
                int idx = stackTraceSpan.IndexOf(Environment.NewLine.AsSpan());
                ReadOnlySpan<char> firstLine = idx >= 0 ? stackTraceSpan[..idx].Trim() : stackTraceSpan.Trim();
                sb.Append($"{Environment.NewLine}    Stack: {firstLine}");
            }
        }

        string entry = sb.ToString();
        WriteEntry(entry);
        Debug.WriteLine(entry);
    }

    /// <summary>
    /// Logs a WebView-specific event.
    /// </summary>
    /// <param name="eventName">The name of the WebView event.</param>
    /// <param name="details">Optional details about the event.</param>
    /// <param name="caller">The calling member name (autopopulated).</param>
    public static void LogWebView(string eventName, string? details = null, [CallerMemberName] string? caller = null)
    {
        string message = !string.IsNullOrWhiteSpace(details)
            ? $"WebView {eventName}: {details}"
            : $"WebView {eventName}";
        Log(message, caller);
    }

    /// <summary>
    /// Logs a JavaScript execution attempt.
    /// </summary>
    /// <param name="script">The JavaScript code executed.</param>
    /// <param name="success">True if execution succeeded; otherwise, false.</param>
    /// <param name="result">Optional result of the execution.</param>
    /// <param name="caller">The calling member name (autopopulated).</param>
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
    /// Logs performance timing for an operation.
    /// </summary>
    /// <param name="operation">The name of the operation.</param>
    /// <param name="elapsed">The elapsed time for the operation.</param>
    /// <param name="success">True if the operation succeeded; otherwise, false.</param>
    /// <param name="caller">The calling member name (autopopulated).</param>
    public static void LogTiming(string operation, TimeSpan elapsed, bool success = true, [CallerMemberName] string? caller = null)
    {
        string status = success ? "completed" : "failed";
        Log($"TIMING: {operation} {status} in {elapsed.TotalMilliseconds:F1}ms", caller);
    }

    /// <summary>
    /// Logs asset operations such as file loading.
    /// </summary>
    /// <param name="operation">The asset operation performed.</param>
    /// <param name="assetName">The name of the asset.</param>
    /// <param name="success">True if the operation succeeded; otherwise, false.</param>
    /// <param name="sizeBytes">Optional size of the asset in bytes.</param>
    /// <param name="caller">The calling member name (autopopulated).</param>
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
    /// Gets the current log file path.
    /// </summary>
    /// <returns>The path to the log file.</returns>
    public static string GetLogPath() => _logPath;

    /// <summary>
    /// Clears the log file and writes a new session header.
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

    /// <summary>
    /// Writes a session header to the log file.
    /// </summary>
    private static void WriteSessionHeader()
    {
        string? version = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
        StringBuilder sb = new StringBuilder(256);
        sb.AppendLine("============================================");
        sb.AppendLine("MermaidPad Debug Session Started");
        sb.AppendLine($"Version: {version ?? "Unknown"}");
        sb.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"PID: {Environment.ProcessId}");
        sb.AppendLine($"OS: {Environment.OSVersion}");
        sb.AppendLine($".NET: {Environment.Version}");
        sb.AppendLine($"Working Directory: {Environment.CurrentDirectory}");
        sb.AppendLine($"Log File: {_logPath}");
        sb.AppendLine($"Lock File: {_lockPath}");
        sb.AppendLine("============================================");
        sb.AppendLine();

        WriteEntryWithLock(sb.ToString());
    }

    /// <summary>
    /// Writes a log entry to the log file, using lock file coordination.
    /// </summary>
    /// <param name="entry">The log entry to write.</param>
    private static void WriteEntry(string entry)
    {
        WriteEntryWithLock(entry + Environment.NewLine);
    }

    /// <summary>
    /// Writes content to the log file, acquiring a lock file for inter-process coordination.
    /// Retries up to three times with exponential backoff if the lock cannot be acquired.
    /// </summary>
    /// <param name="content">The content to write to the log file.</param>
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
                    delay = ExponentialBackoff(delay);
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
                delay = ExponentialBackoff(delay);
            }
        }

        // All retries failed, fall back to Debug.WriteLine
        Debug.WriteLine($"[LOG-FALLBACK] {content.Trim()}");
    }

    /// <summary>
    /// Calculates the next delay interval using an exponential backoff strategy.
    /// </summary>
    /// <param name="delay">The current delay interval, in milliseconds.</param>
    /// <returns>The next delay interval, in milliseconds, which is the smaller of twice the current delay or 1,000 milliseconds.</returns>
    /// <example>
    /// <code>
    /// int nextDelay = ExponentialBackoff(250); // nextDelay will be 500
    /// </code>
    /// </example>
    private static int ExponentialBackoff(int delay) => Math.Min(delay * 2, MaxRetryDelayMs);

    /// <summary>
    /// Validates that the log and lock file paths are within the specified base directory.
    /// </summary>
    /// <remarks>This method ensures that the log and lock file paths do not point to locations outside the
    /// base directory. If either path is outside the base directory, a <see cref="System.Security.SecurityException"/>
    /// is thrown.</remarks>
    /// <exception cref="SecurityException">Thrown if the log path or lock path is outside the base directory.</exception>
    private static void ValidateLogPaths()
    {
        // Validate log path
        string fullLogPath = Path.GetFullPath(_logPath);
        string fullBaseDir = Path.GetFullPath(_baseDir);

        if (!fullLogPath.StartsWith(fullBaseDir, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityException($"Log path '{fullLogPath}' is outside base directory");
        }

        // Validate lock path
        string fullLockPath = Path.GetFullPath(_lockPath);
        if (!fullLockPath.StartsWith(fullBaseDir, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityException($"Lock path '{fullLockPath}' is outside base directory");
        }
    }

    /// <summary>
    /// Attempts to acquire an exclusive lock on the log file by creating a lock file.
    /// Returns a FileStream if successful, or null if the lock is held by another process or access is denied.
    /// </summary>
    /// <returns>
    /// A FileStream representing the acquired lock, or null if the lock could not be obtained.
    /// </returns>
    private static FileStream? TryAcquireLogLock()
    {
        try
        {
            // Re-validate before each operation
            ValidateLogPaths();

            // Additional check for symbolic links
            if (File.Exists(_lockPath))
            {
                FileInfo lockInfo = new FileInfo(_lockPath);
                if ((lockInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    Debug.WriteLine("Lock file is a symbolic link - aborting");
                    return null;
                }
            }

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

    /// <summary>
    /// Releases the lock on the log file by disposing the FileStream and deleting the lock file.
    /// </summary>
    /// <param name="lockStream">The FileStream representing the acquired lock.</param>
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

    /// <summary>
    /// Writes the specified content to the log file.
    /// If the content is empty, the log file is cleared.
    /// Otherwise, the content is appended to the log file.
    /// </summary>
    /// <param name="content">The content to write to the log file.</param>
    private static void WriteToLogFile(string content)
    {
        // Validate before writing
        ValidateLogPaths();

        // Check for symbolic links
        if (File.Exists(_logPath))
        {
            FileInfo logInfo = new FileInfo(_logPath);
            if ((logInfo.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                Debug.WriteLine("Log file is a symbolic link - aborting write");
                return;
            }
        }

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

    /// <summary>
    /// Gets the current timestamp formatted as HH:mm:ss.fff.
    /// </summary>
    /// <returns>A string representing the current timestamp.</returns>
    private static string GetTimestamp() => DateTime.Now.ToString("HH:mm:ss.fff");

    /// <summary>
    /// Removes any stale lock file left over from a previous crash or abnormal termination.
    /// </summary>
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
