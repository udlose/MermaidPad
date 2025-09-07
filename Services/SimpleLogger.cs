// MIT License
// Copyright (c) 2025 Dave Black
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using JetBrains.Annotations;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;

namespace MermaidPad.Services;
/// <summary>
/// Dead simple file logger for debugging WebView issues.
/// Uses lock file coordination to prevent conflicts between multiple instances.
/// </summary>
public static class SimpleLogger
{
    /// <summary>
    /// Represents the path to the application data directory for the current user.
    /// </summary>
    /// <remarks>This field is initialized to the value returned by Environment.GetFolderPath. It provides a convenient way to
    /// access the application data directory, which is typically used for storing user-specific application data.</remarks>
    private static readonly string _appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    /// <summary>
    /// Represents the base directory path for storing MermaidPad application data.
    /// </summary>
    /// <remarks>The directory path is constructed by combining the application data directory with the
    /// "MermaidPad" subdirectory. This field is read-only and cannot be modified at runtime.</remarks>
    private static readonly string _baseDir = Path.Combine(_appDataDir, "MermaidPad");

    /// <summary>
    /// Represents the file path for the debug log.
    /// </summary>
    /// <remarks>This field combines the base directory with the file name "debug.log" to create the full
    /// path. It is used internally for logging purposes.</remarks>
    private static readonly string _logPath = Path.Combine(_baseDir, "debug.log");

    /// <summary>
    /// Represents the file path for the lock file used to synchronize access to the debug log.
    /// </summary>
    /// <remarks>This path is constructed by combining the base directory with the file name "debug.log.lock".
    /// It is used to ensure that only one process can write to the debug log at a time.</remarks>
    private static readonly string _lockPath = Path.Combine(_baseDir, "debug.log.lock");

    /// <summary>
    /// Initial delay in milliseconds for retrying lock acquisition.
    /// </summary>
    private const int InitialRetryDelayMs = 25;

    /// <summary>
    /// Maximum delay in milliseconds for retrying lock acquisition.
    /// </summary>
    private const int MaxRetryDelayMs = 1_000;

    /// <summary>
    /// Static constructor. Validates log and lock file paths, cleans up stale lock files, and writes the session header.
    /// </summary>
    static SimpleLogger()
    {
        // Use same directory pattern as SettingsService
        Directory.CreateDirectory(_baseDir);

        ValidateLogPaths();

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
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
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
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

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
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);

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
        ArgumentException.ThrowIfNullOrWhiteSpace(script);

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
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

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
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetName);
        if (sizeBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "Size in bytes cannot be negative");
        }

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
    /// Attempts to write the specified content to the log file, using a locking mechanism to ensure exclusive access.
    /// Retries the operation up to a maximum number of attempts if the lock cannot be acquired.
    /// </summary>
    /// <remarks>This method uses a retry mechanism with exponential backoff to handle scenarios where the
    /// lock file is  temporarily unavailable. If all retry attempts fail, the content is written to the debug output as
    /// a fallback.</remarks>
    /// <param name="content">The content to write to the log file. Cannot be <see langword="null"/> or empty.</param>
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
    /// </summary>
    /// <remarks>This method attempts to create a lock file at the specified path to ensure that only one
    /// process can access the log file at a time. If the lock file already exists and is in use by another process, or
    /// if the file system or permissions prevent the lock from being acquired, the method returns <see
    /// langword="null"/>.  The method also validates the lock file path before attempting to acquire the lock and
    /// ensures that the lock file is not a symbolic link. If the lock file is a symbolic link, the method will abort
    /// and return <see langword="null"/>.</remarks>
    /// <returns>A <see cref="FileStream"/> representing the acquired lock if successful; otherwise, <see langword="null"/>.</returns>
    [MustDisposeResource]
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
            return new FileStream(_lockPath, FileMode.Create, FileAccess.Write, FileShare.None);
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
    /// Releases the lock on the log file by disposing of the provided file stream  and deleting the associated lock
    /// file, if it exists.
    /// </summary>
    /// <remarks>This method ensures that the lock file is cleaned up after the lock is released.  If an error
    /// occurs during the cleanup process, it is logged for debugging purposes  but does not propagate
    /// exceptions.</remarks>
    /// <param name="lockStream">The <see cref="FileStream"/> representing the lock on the log file.  This stream will be disposed as part of the
    /// release process.</param>
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
    /// Writes the specified content to the log file. If the content is empty or null, the log file is cleared instead
    /// of appending.
    /// </summary>
    /// <remarks>This method ensures that the log file is not a symbolic link before writing to it. If the log
    /// file is a symbolic link, the write operation is aborted.</remarks>
    /// <param name="content">The content to write to the log file. If <paramref name="content"/> is <see langword="null"/> or empty, the log
    /// file is cleared.</param>
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
    /// Removes the stale lock file from the file system, if it exists.
    /// </summary>
    /// <remarks>This method checks for the presence of a lock file at the predefined path and deletes it if
    /// found. Any exceptions encountered during the operation are logged for debugging purposes but do not interrupt
    /// the program's execution.</remarks>
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
