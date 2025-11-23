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
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace MermaidPad.Services;

/// <summary>
/// Base class providing common settings-related utilities and logging helpers.
/// Implementations can use <see cref="GetConfigDirectory"/> to determine the
/// application's per-user configuration directory and use the protected logging
/// helpers to write warnings and errors consistently.
/// </summary>
public abstract class SettingsBase
{
    /// <summary>
    /// Category label used when emitting structured log events from settings code.
    /// </summary>
    private const string Category = "Settings";

    /// <summary>
    /// Optional logger instance provided by the host. May be <see langword="null"/>
    /// during early initialization when dependency injection has not yet supplied a logger.
    /// </summary>
    private readonly ILogger? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsBase"/> class.
    /// </summary>
    /// <param name="logger">
    /// An optional <see cref="ILogger"/> instance used to emit logs. If <see langword="null"/>,
    /// Serilog's static <see cref="Log"/> methods are used as a fallback.
    /// </param>
    protected SettingsBase(ILogger? logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Returns the per-user configuration directory path used by the application.
    /// </summary>
    /// <remarks>
    /// On Windows this typically resolves to an "AppData\Roaming" path such as
    /// "%APPDATA%\MermaidPad". The method composes the path using
    /// <see cref="Environment.GetFolderPath(Environment.SpecialFolder)"/>.
    /// </remarks>
    /// <returns>
    /// The full path to the application's configuration directory for the current user.
    /// If the directory does not exist; it is created.
    /// </returns>
    public static string GetConfigDirectory()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string configDirectory = Path.Combine(appData, "MermaidPad");
        if (!Directory.Exists(configDirectory))
        {
            Directory.CreateDirectory(configDirectory);
        }
        return configDirectory;
    }

    /// <summary>
    /// Logs an error-level message and optional exception using either the injected
    /// <see cref="ILogger"/> or Serilog as a fallback.
    /// </summary>
    /// <param name="exception">The exception to log; may be <see langword="null"/> if none.</param>
    /// <param name="message">A human-readable message describing the error condition.</param>
    protected void LogError(Exception? exception, string message)
    {
        if (_logger is not null)
        {
            _logger.LogError(exception, "{Category} {Message}", Category, message);
            return;
        }
        Log.Error(exception, "{Category} {Message}", Category, message);
    }

    /// <summary>
    /// Logs a warning-level message and optional exception using either the injected
    /// <see cref="ILogger"/> or Serilog as a fallback.
    /// </summary>
    /// <param name="exception">The exception to log; may be <see langword="null"/> if none.</param>
    /// <param name="message">A human-readable message describing the warning condition.</param>
    protected void LogWarning(Exception? exception, string message)
    {
        if (_logger is not null)
        {
            _logger.LogWarning(exception, "{Category} {Message}", Category, message);
            return;
        }
        Log.Warning(exception, "{Category} {Message}", Category, message);
    }

    /// <summary>
    /// Logs an informational message to the configured logging provider using the current category.
    /// </summary>
    /// <remarks>If a custom logger is configured, the message is sent to that logger; otherwise, it is logged
    /// using the default provider. The log entry includes the current category for context.</remarks>
    /// <param name="message">The message to log. This should provide relevant information for diagnostics or monitoring purposes.</param>
    protected void LogInformation(string message)
    {
        if (_logger is not null)
        {
            _logger.LogInformation("{Category} {Message}", Category, message);
            return;
        }
        Log.Information("{Category} {Message}", Category, message);
    }

    /// <summary>
    /// Writes a debug-level log entry with the specified message and category information.
    /// </summary>
    /// <remarks>This method logs the message using the configured logger if available; otherwise, it falls
    /// back to a default logging mechanism. Debug-level logs are typically used for diagnostic purposes and may not be
    /// recorded in production environments depending on log settings.</remarks>
    /// <param name="message">The message to include in the debug log entry. This value can be any string describing the event or state to be
    /// logged.</param>
    protected void LogDebug(string message)
    {
        if (_logger is not null)
        {
            _logger.LogDebug("{Category} {Message}", Category, message);
            return;
        }
        Log.Debug("{Category} {Message}", Category, message);
    }
}
