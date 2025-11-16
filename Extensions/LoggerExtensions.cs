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

    /// <summary>
    /// Logs a simple asset-related message.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="memberName">The calling member name (autopopulated).</param>
    public static void LogAsset(this ILogger logger, string message, [CallerMemberName] string? memberName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        logger.LogInformation("[{MemberName}] {Message}", memberName, message);
    }
}
