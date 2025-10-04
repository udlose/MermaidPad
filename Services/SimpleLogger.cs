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
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
    /// Represents an array of command-line parameters commonly used to retrieve version information on Linux systems.
    /// </summary>
    /// <remarks>This array includes parameters such as <c>--version</c> and <c>-v</c>, which are typically
    /// used in Linux command-line tools to display version details.</remarks>
    private static readonly string[] _linuxParamArray = ["--version", "-v"];

    /// <summary>
    /// Represents the set of characters used to identify line breaks.
    /// </summary>
    /// <remarks>This array contains the carriage return <c>\r</c> and newline <c>\n</c> characters, which are
    /// commonly used to detect or handle line breaks in text processing.</remarks>
    private static readonly char[] _lineBreakChars = ['\r', '\n'];

    /// <summary>
    /// Initial delay in milliseconds for retrying lock acquisition.
    /// </summary>
    private const int InitialRetryDelayMs = 25;

    /// <summary>
    /// Maximum delay in milliseconds for retrying lock acquisition.
    /// </summary>
    private const int MaxRetryDelayMs = 1_000;

    /// <summary>
    /// The default timeout, in milliseconds, for executing commands.
    /// </summary>
    /// <remarks>This constant defines the maximum time a command is allowed to execute before timing out. It
    /// is intended for internal use and should not be modified at runtime.</remarks>
    private const int CommandTimeoutMs = 3_000;

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
        sb.AppendLine($"OS Version: {Environment.OSVersion}");
        sb.AppendLine($"64-bit OS: {Environment.Is64BitOperatingSystem}");
        sb.AppendLine($"64-bit Process: {Environment.Is64BitProcess}");
        sb.AppendLine($".NET: {Environment.Version}");
        sb.AppendLine($"Working Directory: {Environment.CurrentDirectory}");
        sb.AppendLine();
        sb.AppendLine($".NET Runtime Version: {RuntimeInformation.FrameworkDescription}");
        sb.AppendLine($"OS Architecture: {RuntimeInformation.OSArchitecture}");
        sb.AppendLine($"RuntimeInfo: {RuntimeInformation.OSDescription}");
        sb.AppendLine($"Process Architecture: {RuntimeInformation.ProcessArchitecture}");
        sb.AppendLine($"RID: {RuntimeInformation.RuntimeIdentifier}");
        sb.AppendLine();

        // New: Append platform dependency versions
        AppendPlatformDependencyInfo(sb);
        sb.AppendLine();

        sb.AppendLine($"Log File: {_logPath}");
        sb.AppendLine($"Lock File: {_lockPath}");
        sb.AppendLine("============================================");
        sb.AppendLine();

        WriteEntryWithLock(sb.ToString());
    }

    /// <summary>
    /// Appends platform-specific dependency information (versions and availability) to the session header.
    /// </summary>
    private static void AppendPlatformDependencyInfo(StringBuilder sb)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                sb.AppendLine("Platform Dependencies (Windows):");
                string webView2 = TryGetWindowsWebView2Version() ?? "Not found";
                sb.AppendLine($"  WebView2 Runtime: {webView2}");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                sb.AppendLine("Platform Dependencies (Linux):");
                string webkit = TryGetLinuxWebKitGtkVersion() ?? "Not found";
                sb.AppendLine($"  WebKitGTK (webkit2gtk): {webkit}");

                // Common graphical dialog helpers
                AppendLinuxDialogToolVersion(sb, "zenity", _linuxParamArray);
                AppendLinuxDialogToolVersion(sb, "kdialog", _linuxParamArray);
                AppendLinuxDialogToolVersion(sb, "yad", _linuxParamArray);
                AppendLinuxDialogToolVersion(sb, "Xdialog", _linuxParamArray);
                AppendLinuxDialogToolVersion(sb, "gxmessage", _linuxParamArray);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                sb.AppendLine("Platform Dependencies (macOS):");
                string webkit = TryGetMacWebKitFrameworkVersion() ?? "Unknown (system-provided)";
                sb.AppendLine($"  WebKit.framework: {webkit}");
            }
            else
            {
                sb.AppendLine("Platform Dependencies: Unknown OS");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Platform dependency detection error: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Attempts to retrieve the WebView2 Runtime version on Windows.
    /// Prefers the WebView2 API (if available), otherwise scans known install locations.
    /// </summary>
    private static string? TryGetWindowsWebView2Version()
    {
        try
        {
            // Try via WebView2 API without a hard compile-time dependency (reflection).
            // Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString()
            const string typeName = "Microsoft.Web.WebView2.Core.CoreWebView2Environment, Microsoft.Web.WebView2.Core";
            Type? envType = Type.GetType(typeName, throwOnError: false);
            if (envType is not null)
            {
                MethodInfo? method = envType.GetMethod("GetAvailableBrowserVersionString", Type.EmptyTypes);
                object? result = method?.Invoke(null, null);
                if (result is string s && !string.IsNullOrWhiteSpace(s))
                {
                    return s.Trim();
                }

                // Newer signature with optional parameter exists on some versions: string? GetAvailableBrowserVersionString(string? folder)
                MethodInfo? methodWithArg = envType.GetMethod("GetAvailableBrowserVersionString", new[] { typeof(string) });
                object? result2 = methodWithArg?.Invoke(null, new object?[] { null });
                if (result2 is string s2 && !string.IsNullOrWhiteSpace(s2))
                {
                    return s2.Trim();
                }
            }
        }
        catch
        {
            // Ignore and fallback to disk probing.
        }

        try
        {
            // Fallback: probe standard Evergreen runtime locations
            string? ver =
                ProbeWebView2FromInstallRoot(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)) ??
                ProbeWebView2FromInstallRoot(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
            return string.IsNullOrWhiteSpace(ver) ? null : ver;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Scans the Evergreen WebView2 Application folder for the highest version present.
    /// </summary>
    private static string? ProbeWebView2FromInstallRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return null;
        }

        string appDir = Path.Combine(root, "Microsoft", "EdgeWebView", "Application");
        if (!Directory.Exists(appDir))
        {
            return null;
        }

        Version? best = null;
        string? bestDir = null;

        foreach (string dir in Directory.GetDirectories(appDir))
        {
            string name = Path.GetFileName(dir);
            if (Version.TryParse(name, out Version? v) && (best is null || v > best))
            {
                best = v;
                bestDir = dir;
            }
        }

        if (bestDir is null)
        {
            return null;
        }

        // Prefer the file version of the actual executable if present
        string exePath = Path.Combine(bestDir, "msedgewebview2.exe");
        if (File.Exists(exePath))
        {
            try
            {
                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(exePath);
                if (!string.IsNullOrWhiteSpace(fvi.FileVersion))
                {
                    return fvi.FileVersion;
                }
            }
            catch
            {
                // ignore
            }
        }

        return best?.ToString();
    }

    /// <summary>
    /// Attempts to get the installed WebKitGTK (webkit2gtk) version on Linux using pkg-config, dpkg or rpm.
    /// </summary>
    private static string? TryGetLinuxWebKitGtkVersion()
    {
        // Try pkg-config first (most portable)
        string? version =
            RunCommandAndCapture("pkg-config", "--modversion webkit2gtk-4.1", CommandTimeoutMs) ??
            RunCommandAndCapture("pkg-config", "--modversion webkit2gtk-4.0", CommandTimeoutMs);
        if (!string.IsNullOrWhiteSpace(version))
        {
            return version;
        }

        // Debian/Ubuntu
        version =
            RunCommandAndCapture("dpkg-query", "-W -f=${Version} libwebkit2gtk-4.1-0", CommandTimeoutMs) ??
            RunCommandAndCapture("dpkg-query", "-W -f=${Version} libwebkit2gtk-4.0-37", CommandTimeoutMs);
        if (!string.IsNullOrWhiteSpace(version))
        {
            return version;
        }

        // RPM-based
        version =
            RunCommandAndCapture("rpm", "-q --qf %{VERSION}-%{RELEASE} webkit2gtk4.1", CommandTimeoutMs) ??
            RunCommandAndCapture("rpm", "-q --qf %{VERSION}-%{RELEASE} webkit2gtk4.0", CommandTimeoutMs);
        if (!string.IsNullOrWhiteSpace(version))
        {
            return version;
        }

        return null;
    }

    /// <summary>
    /// Appends a Linux dialog tool's version if available.
    /// </summary>
    private static void AppendLinuxDialogToolVersion(StringBuilder sb, string command, string[] argCandidates)
    {
        string? version = null;
        for (int i = 0; i < argCandidates.Length && string.IsNullOrWhiteSpace(version); i++)
        {
            version = RunCommandAndCapture(command, argCandidates[i], 1_500);
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            sb.AppendLine($"  {command}: Not found");
        }
        else
        {
            // Only keep the first line for readability
            int idx = version.IndexOfAny(_lineBreakChars);
            string firstLine = idx >= 0 ? version[..idx] : version;
            sb.AppendLine($"  {command}: {firstLine.Trim()}");
        }
    }

    /// <summary>
    /// Attempts to get the system WebKit.framework version on macOS using 'defaults read'.
    /// </summary>
    private static string? TryGetMacWebKitFrameworkVersion()
    {
        // Read CFBundleShortVersionString from the framework's Info.plist (handled by 'defaults' for binary or XML plists)
        const string path = "/System/Library/Frameworks/WebKit.framework/Versions/Current/Resources/Info";
        string? shortVer = RunCommandAndCapture("defaults", $"read {path} CFBundleShortVersionString", CommandTimeoutMs);
        string? bundleVer = RunCommandAndCapture("defaults", $"read {path} CFBundleVersion", CommandTimeoutMs);

        if (!string.IsNullOrWhiteSpace(shortVer) && !string.IsNullOrWhiteSpace(bundleVer))
        {
            return $"{shortVer.Trim()} ({bundleVer.Trim()})";
        }

        if (!string.IsNullOrWhiteSpace(shortVer))
        {
            return shortVer.Trim();
        }

        return null;
    }

    /// <summary>
    /// Runs a command and captures trimmed stdout if the process exits successfully.
    /// Returns null if the command cannot be started, times out, or exits non-zero.
    /// If stdout is empty but stderr has content, returns stderr (trimmed) as a best-effort fallback.
    /// </summary>
    private static string? RunCommandAndCapture(string fileName, string arguments, int timeoutMs)
    {
        try
        {
            using Process p = new Process();
            p.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true
            };

            if (!p.Start())
            {
                return null;
            }

            // Avoid lambda captures and detach handlers on exit.
            ProcOutputCollector collector = new ProcOutputCollector();
            p.OutputDataReceived += collector.OnOutput;
            p.ErrorDataReceived += collector.OnError;

            try
            {
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();

                bool exited = p.WaitForExit(timeoutMs);
                if (!exited)
                {
                    try { p.Kill(entireProcessTree: true); } catch { /* ignore */ }
                }

                // Ensure process exit and give async reads time to finish
                try { p.WaitForExit(); } catch { /* ignore */ }
                try { p.CancelOutputRead(); } catch { /* ignore */ }
                try { p.CancelErrorRead(); } catch { /* ignore */ }

                if (!exited)
                {
                    return null;
                }

                string outText = collector.Stdout.ToString().Trim();
                string errText = collector.Stderr.ToString().Trim();

                if (p.ExitCode != 0) return null;
                if (!string.IsNullOrWhiteSpace(outText)) return outText;
                if (!string.IsNullOrWhiteSpace(errText)) return errText; // some tools print version to stderr

                return null;
            }
            finally
            {
                // Detach handlers to satisfy analyzers and avoid retaining state longer than necessary.
                p.OutputDataReceived -= collector.OnOutput;
                p.ErrorDataReceived -= collector.OnError;
            }
        }
        catch
        {
            return null;
        }
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
    /// Validates that the specified path is within the specified base directory.
    /// </summary>
    /// <param name="path">The path to validate. This must be an absolute or relative path.</param>
    /// <param name="baseDir">The base directory against which the path is validated. This must be an absolute or relative path.</param>
    /// <param name="description">A description of the path being validated, used in the exception message if validation fails.</param>
    /// <exception cref="SecurityException">Thrown if the specified <paramref name="path"/> is outside the specified <paramref name="baseDir"/>.</exception>
    private static void ValidatePathWithinBaseDir(string path, string baseDir, string description)
    {
        string fullPath = Path.GetFullPath(path);
        string fullBaseDir = Path.GetFullPath(baseDir);
        if (!fullPath.StartsWith(fullBaseDir, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityException($"{description} '{fullPath}' is outside base directory");
        }
    }

    /// <summary>
    /// Validates that the log and lock paths are within the specified base directory.
    /// </summary>
    /// <remarks>This method ensures that the log and lock paths are securely contained within the base
    /// directory. If either path is outside the base directory, a <see cref="System.Security.SecurityException"/> is
    /// thrown.</remarks>
    /// <exception cref="SecurityException">Thrown if the log path or lock path is outside the base directory.</exception>
    private static void ValidateLogPaths()
    {
        // Validate log paths
        ValidatePathWithinBaseDir(_logPath, _baseDir, "Log path");
        ValidatePathWithinBaseDir(_lockPath, _baseDir, "Lock path");
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
    [SuppressMessage("Security", "SEC0016:Path Tampering: Unvalidated File Path", Justification = "Path is validated before use")]
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
    [SuppressMessage("Security", "SEC0016:Path Tampering: Unvalidated File Path", Justification = "Path is validated before use")]
    private static void CleanupStaleLockFile()
    {
        try
        {
            // Validate the lock path before deleting
            ValidatePathWithinBaseDir(_lockPath, _baseDir, "Lock path");

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

    /// <summary>
    /// Collects and stores the standard output and error streams of a process to avoid lambda captures.
    /// </summary>
    /// <remarks>This class provides mechanisms to capture and store the output and error data received from a
    /// process in real-time. The collected data is stored in separate <see cref="StringBuilder"/> instances for
    /// standard output and standard error.</remarks>
    private sealed class ProcOutputCollector
    {
        public readonly StringBuilder Stdout = new StringBuilder();
        public readonly StringBuilder Stderr = new StringBuilder();

        public void OnOutput(object? sender, DataReceivedEventArgs e)
        {
            if (e.Data is not null)
            {
                Stdout.AppendLine(e.Data);
            }
        }

        public void OnError(object? sender, DataReceivedEventArgs e)
        {
            if (e.Data is not null)
            {
                Stderr.AppendLine(e.Data);
            }
        }
    }
}
