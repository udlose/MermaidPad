using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

namespace MermaidPad.Services;
/// <summary>
/// Dead simple file logger for debugging WebView issues.
/// Just appends to a single log file with timestamps.
/// Uses named Mutex for inter-process coordination.
/// </summary>
public static class SimpleLogger
{
    private static readonly string _logPath;
    private static readonly Mutex _logMutex;
    private static readonly string _mutexName;
    private const int MaxRetries = 3;
    private const int BaseDelayInMs = 50;
    private const int TimeoutInMs = 5_000;          // 5 second timeout for inter-process coordination
    private const int ContentPreviewLength = 50;    // Number of characters to show in debug output when mutex acquisition fails

    static SimpleLogger()
    {
        // Use same directory pattern as SettingsService
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string baseDir = Path.Combine(appData, "MermaidPad");
        Directory.CreateDirectory(baseDir);

        _logPath = Path.Combine(baseDir, "debug.log");

        // Create a named mutex for inter-process coordination
        // Use a hash of the log path to ensure uniqueness per user/path
        _mutexName = $"Local\\MermaidPad_Logger_{GetStableHash(_logPath):X8}";

        try
        {
            _logMutex = new Mutex(false, _mutexName);
        }
        catch (Exception ex)
        {
            // Fallback to unnamed mutex if named mutex fails (e.g., insufficient privileges)
            Debug.WriteLine($"Failed to create named mutex, using unnamed: {ex.Message}");
            _logMutex = new Mutex(false);
        }

        // Write session header
        WriteSessionHeaderInternal();
    }

    /// <summary>
    /// Log a simple message with timestamp
    /// </summary>
    public static void Log(string message, [CallerMemberName] string? caller = null, [CallerFilePath] string? file = null)
    {
        string fileName = file is not null ? Path.GetFileNameWithoutExtension(file) : "Unknown";
        string entry = $"[{DateTime.Now:HH:mm:ss.fff}] [{fileName}.{caller}] {message}";

        WriteEntryWithMutex(entry + Environment.NewLine);
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
        WriteEntryWithMutex(entry + Environment.NewLine);
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
        try
        {
            if (_logMutex.WaitOne(TimeoutInMs))
            {
                try
                {
                    // Clear the file without holding any locks during internal calls
                    ClearLogFileInternal();
                    WriteSessionHeaderInternal();
                }
                finally
                {
                    _logMutex.ReleaseMutex();
                }
            }
            else
            {
                Debug.WriteLine($"Failed to acquire log mutex within {TimeoutInMs}ms for clear operation");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to clear log: {ex.Message}");
        }

        // Log the clear operation (this will acquire its own mutex)
        Log("Log file cleared");
    }

    private static void WriteSessionHeaderInternal()
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
        sb.AppendLine($"Mutex: {_mutexName}");
        sb.AppendLine("============================================");
        sb.AppendLine();

        WriteEntryWithMutex(sb.ToString());
    }

    private static void ClearLogFileInternal()
    {
        // This method assumes the mutex is already held
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                File.WriteAllText(_logPath, "");
                return; // Success
            }
            catch (IOException ex) when (IsFileLockException(ex) && attempt < MaxRetries - 1)
            {
                int delay = BaseDelayInMs * (int)Math.Pow(2, attempt);
                Debug.WriteLine($"Log file locked during clear (attempt {attempt + 1}/{MaxRetries}), retrying in {delay}ms");
                Thread.Sleep(delay);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to clear log file: {ex.Message}");
                return;
            }
        }
    }

    private static void WriteEntryWithMutex(string content)
    {
        try
        {
            if (_logMutex.WaitOne(TimeoutInMs))
            {
                try
                {
                    // Retry logic for file I/O within the mutex
                    for (int attempt = 0; attempt < MaxRetries; attempt++)
                    {
                        try
                        {
                            // Use FileStream with proper sharing to allow concurrent read access while writing
                            using FileStream fs = new FileStream(_logPath, FileMode.Append, FileAccess.Write, FileShare.Read);
                            using StreamWriter writer = new StreamWriter(fs, Encoding.UTF8);
                            writer.Write(content);
                            writer.Flush();
                            return; // Success - exit retry loop
                        }
                        catch (IOException ex) when (IsFileLockException(ex) && attempt < MaxRetries - 1)
                        {
                            // File is locked, wait and retry
                            int delay = BaseDelayInMs * (int)Math.Pow(2, attempt);
                            Debug.WriteLine($"Log file locked (attempt {attempt + 1}/{MaxRetries}), retrying in {delay}ms: {ex.Message}");
                            Thread.Sleep(delay);
                        }
                        catch (UnauthorizedAccessException ex) when (attempt < MaxRetries - 1)
                        {
                            // Access denied, wait and retry
                            int delay = BaseDelayInMs * (int)Math.Pow(2, attempt);
                            Debug.WriteLine($"Log file access denied (attempt {attempt + 1}/{MaxRetries}), retrying in {delay}ms: {ex.Message}");
                            Thread.Sleep(delay);
                        }
                        catch (Exception ex)
                        {
                            // For any other exception, write to Debug and give up
                            Debug.WriteLine($"Failed to write to log file: {ex.Message}");
                            return;
                        }
                    }

                    // If we get here, all retries failed
                    Debug.WriteLine($"Failed to write to log file after {MaxRetries} attempts");
                }
                finally
                {
                    _logMutex.ReleaseMutex();
                }
            }
            else
            {
                // Could not acquire mutex within timeout
                Debug.WriteLine($"Failed to acquire log mutex within {TimeoutInMs}ms. Content: {content.AsSpan(0, Math.Min(ContentPreviewLength, content.Length))}...");
            }
        }
        catch (AbandonedMutexException ex)
        {
            // Another process holding the mutex crashed - we now own it
            Debug.WriteLine($"Acquired abandoned mutex: {ex.Message}");
            try
            {
                // Try to write anyway since we now own the mutex
                WriteDirectToFile(content);
            }
            finally
            {
                _logMutex.ReleaseMutex();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Mutex error: {ex.Message}");
        }
    }

    private static void WriteDirectToFile(string content)
    {
        try
        {
            using FileStream fs = new FileStream(_logPath, FileMode.Append, FileAccess.Write, FileShare.Read);
            using StreamWriter writer = new StreamWriter(fs, Encoding.UTF8);
            writer.Write(content);
            writer.Flush();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Direct file write failed: {ex.Message}");
        }
    }

    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Error codes are defined by Windows API")]
    private static bool IsFileLockException(IOException ioex)
    {
        // Check for specific error codes that indicate file is in use
        // See https://learn.microsoft.com/en-us/windows/win32/debug/system-error-codes--0-499-
        const int ERROR_SHARING_VIOLATION = 32;   // Win32 32 (0x20) ERROR_SHARING_VIOLATION
        const int ERROR_LOCK_VIOLATION = 33;      // Win32 33 (0x21) ERROR_LOCK_VIOLATION
        const int hrResultMask = 0xFFFF;      // Mask to get the error code from HResult

        int errorCode = ioex.HResult & hrResultMask;
        return errorCode is ERROR_SHARING_VIOLATION or ERROR_LOCK_VIOLATION;
    }

    private static uint GetStableHash(string input)
    {
        // Simple hash function for mutex naming (consistent across processes)
        uint hash = 2166136261;
        foreach (char c in input)
        {
            hash = (hash ^ c) * 16777619;
        }
        return hash;
    }
}