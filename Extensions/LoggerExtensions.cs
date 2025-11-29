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

using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace MermaidPad.Extensions;

/// <summary>
/// Extension methods for ILogger to support specialized logging scenarios.
/// Provides structured logging for WebView, JavaScript, timing, and asset operations.
/// </summary>
public static class LoggerExtensions
{
    /// <summary>
    /// Logs detailed information about the current thread, synchronization context, and dispatcher access for
    /// diagnostic purposes.
    /// </summary>
    /// <remarks>This method is intended for debugging and diagnostic scenarios where understanding the
    /// execution context is important. It logs the physical thread ID, background status, thread name, current
    /// synchronization context, and dispatcher access status. The log entry is written at the debug level.</remarks>
    /// <param name="logger">The logger instance used to record the thread context information.</param>
    /// <param name="exception">An optional exception to include in the log entry. If specified, the exception details are logged alongside the
    /// thread context.</param>
    /// <param name="callerName">The name of the calling member. This is automatically supplied by the compiler and identifies the source of the
    /// log entry.</param>
    public static void LogThreadContext(
        this ILogger logger,
        Exception? exception = null,
        [CallerMemberName] string? callerName = null)
    {
        // Get Physical Thread Info
        Thread thread = Thread.CurrentThread;
        int threadId = Environment.CurrentManagedThreadId;
        bool isBackground = thread.IsBackground;

        // Get the Sync Context
        SynchronizationContext? context = SynchronizationContext.Current;
        string contextName = context?.GetType().Name ?? "NULL";

        // Get the Avalonia Dispatcher Status
        // CheckAccess() returns true if we are technically on the thread
        // that the Dispatcher "owns", regardless of Context state.
        bool hasDispatcherAccess = Dispatcher.UIThread.CheckAccess();

        logger.LogDebug(exception,
            """
            --- Caller: {CallerName} ---
            [Physical Thread]
            ID          : {ThreadId}
            IsBackground: {IsBackground}
            Name        : {ThreadName}

            [Logical Context]
            SyncContext        : {ContextName}
            HasDispatcherAccess: {HasDispatcherAccess}
            -----------------------------------------
            """, callerName, threadId, isBackground, thread.Name ?? "Unassigned", contextName, hasDispatcherAccess);
    }

    /// <summary>
    /// Logs a WebView event with optional details.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="eventName">The name of the WebView event.</param>
    /// <param name="details">Optional details about the event.</param>
    /// <param name="memberName">The calling member name (autopopulated).</param>
    public static void LogWebView(
        this ILogger logger,
        string eventName,
        string? details = null,
        [CallerMemberName] string? memberName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);

        string message = !string.IsNullOrWhiteSpace(details)
            ? $"WebView {eventName}: {details}"
            : $"WebView {eventName}";

        logger.LogDebug("[{MemberName}] {Message}", memberName, message);
    }

    /// <summary>
    /// Logs a JavaScript execution attempt.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="script">The JavaScript code executed.</param>
    /// <param name="success">True if execution succeeded; otherwise, false.</param>
    /// <param name="writeToDebug">True if the result should be written to debug output only.</param>
    /// <param name="result">Optional result from the JavaScript execution.</param>
    /// <param name="memberName">The calling member name (autopopulated).</param>
    public static void LogJavaScript(
        this ILogger logger,
        string script,
        bool success,
        bool writeToDebug,
        string? result = null,
        [CallerMemberName] string? memberName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(script);

        // Truncate long scripts for readability
        string truncatedScript = script.Length > 100 ? script[..100] + "..." : script;
        string status = success ? "SUCCESS" : "FAILED";
        string message = !string.IsNullOrEmpty(result)
            ? $"JavaScript {status}: {truncatedScript} => {result}"
            : $"JavaScript {status}: {truncatedScript}";

        // Note: writeToDebug parameter is legacy - with configurable logging,
        // users can control debug output via settings
        logger.LogDebug("[{MemberName}] {Message}", memberName, message);

        Debug.WriteLineIf(writeToDebug, message);
    }

    /// <summary>
    /// Logs performance timing for an operation.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="operation">The name of the operation.</param>
    /// <param name="elapsed">The elapsed time for the operation.</param>
    /// <param name="success">True if the operation succeeded; otherwise, false.</param>
    /// <param name="memberName">The calling member name (autopopulated).</param>
    public static void LogTiming(
        this ILogger logger,
        string operation,
        TimeSpan elapsed,
        bool success,
        [CallerMemberName] string? memberName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        string status = success ? "completed" : "failed";
        string message = $"TIMING: {operation} {status} in {elapsed.TotalMilliseconds:F1}ms";

        logger.LogDebug("[{MemberName}] {Message}", memberName, message);
    }

    /// <summary>
    /// Logs asset operations such as file loading.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="operation">The asset operation performed.</param>
    /// <param name="assetName">The name of the asset.</param>
    /// <param name="success">True if the operation succeeded; otherwise, false.</param>
    /// <param name="sizeBytes">Optional size of the asset in bytes.</param>
    /// <param name="memberName">The calling member name (autopopulated).</param>
    public static void LogAsset(
        this ILogger logger,
        string operation,
        string assetName,
        bool success,
        long? sizeBytes = null,
        [CallerMemberName] string? memberName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetName);

        if (sizeBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "Size in bytes cannot be negative");
        }

        string status = success ? "SUCCESS" : "FAILED";
        string sizeInfo = sizeBytes.HasValue ? $" ({sizeBytes.Value:N0} bytes)" : string.Empty;
        string message = $"Asset {operation}: {assetName} - {status}{sizeInfo}";

        logger.LogDebug("[{MemberName}] {Message}", memberName, message);
    }
}
