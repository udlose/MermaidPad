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

using MermaidPad.Models;
using MermaidPad.Services.Platforms;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace MermaidPad.Services;

/// <summary>
/// Provides loading and saving of application settings to a per-user configuration directory.
/// Handles secure file access and validates the settings file path and name prior to I/O operations.
/// </summary>
public sealed class SettingsService : SettingsBase
{
    /// <summary>
    /// <see cref="JsonSerializerOptions"/> used for (de)serializing <see cref="AppSettings"/>.
    /// Configured to ignore case when matching property names and to write indented JSON.
    /// </summary>
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    /// <summary>
    /// Full path to the settings file used by this instance.
    /// </summary>
    private readonly string _settingsPath;

    /// <summary>
    /// Optional secure storage service for sensitive data.
    /// </summary>
    private readonly ISecureStorageService? _secureStorage;

    /// <summary>
    /// Security service for validating file paths and creating secure file streams.
    /// </summary>
    private readonly SecurityService _securityService;

    /// <summary>
    /// The expected file name for persisted settings.
    /// </summary>
    private const string SettingsFileName = "settings.json";

    /// <summary>
    /// The in-memory application settings instance. Consumers may read or modify properties
    /// and then call <see cref="Save"/> to persist changes.
    /// </summary>
    public AppSettings Settings { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsService"/> class.
    /// Ensures the configuration directory exists and loads persisted settings if available.
    /// </summary>
    /// <param name="securityService"><see cref="SecurityService"/> for file access validation.</param>
    /// <param name="logger">
    /// Optional <see cref="ILogger{SettingsService}"/> for diagnostic messages. May be <see langword="null"/>.
    /// </param>
    /// <param name="secureStorage">Optional <see cref="ISecureStorageService"/> for sensitive data.</param>
    public SettingsService(SecurityService securityService, ILogger<SettingsService>? logger = null, ISecureStorageService? secureStorage = null)
        : base(logger)
    {
        _securityService = securityService;
        _secureStorage = secureStorage;

        string baseDir = GetConfigDirectory();
        Directory.CreateDirectory(baseDir);
        _settingsPath = Path.Combine(baseDir, SettingsFileName);
        Settings = Load();
    }

    /// <summary>
    /// Loads persisted <see cref="AppSettings"/> from disk if a valid settings file exists.
    /// Performs validation of the file name and path using <see cref="SecurityService"/>.
    /// If loading fails or validation fails, returns a new default <see cref="AppSettings"/> instance.
    /// </summary>
    /// <remarks>
    /// This method catches exceptions and logs errors via the injected logger when available.
    /// </remarks>
    /// <returns>The deserialized <see cref="AppSettings"/> from disk, or a new default instance.</returns>
    private AppSettings Load()
    {
        try
        {
            // Validate that the settings path is within the expected config directory
            string configDir = GetConfigDirectory();
            string fullSettingsPath = Path.GetFullPath(_settingsPath);

            // Additional validation: ensure the file name is exactly "settings.json"
            if (Path.GetFileName(fullSettingsPath) != SettingsFileName)
            {
                LogError(exception: null, $"{nameof(AppSettings)} file name validation failed on load.");
                return new AppSettings();
            }

            if (File.Exists(fullSettingsPath))
            {
                // Use SecurityService for comprehensive validation
                // Note: Passing null logger to avoid circular dependency and timing issues during initialization
                (bool isSecure, string? reason) = _securityService.IsFilePathSecure(fullSettingsPath, configDir, isAssetFile: true);
                if (!isSecure && !string.IsNullOrEmpty(reason))
                {
                    LogError(exception: null, $"{nameof(AppSettings)} file validation failed: {reason}");
                    return new AppSettings();
                }

                // Use SecurityService for secure file stream creation
                string json;
                using (FileStream fs = _securityService.CreateSecureFileStream(fullSettingsPath, FileMode.Open, FileAccess.Read))
                using (StreamReader reader = new StreamReader(fs))
                {
                    json = reader.ReadToEnd();
                }

                return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            LogError(ex, $"{nameof(AppSettings)} load failed");
        }
        return new AppSettings();
    }

    /// <summary>
    /// Loads only the logging settings from the settings file without creating a full SettingsService instance.
    /// This is useful for bootstrapping logging configuration before the DI container is fully initialized.
    /// Maintains full security validation even during bootstrap.
    /// </summary>
    /// <returns>
    /// The <see cref="LoggingSettings"/> from the persisted settings file, or a new default instance if the file
    /// doesn't exist or cannot be read.
    /// </returns>
    public static LoggingSettings LoadLoggingSettings()
    {
        try
        {
            string configDir = GetConfigDirectory();
            string settingsPath = Path.Combine(configDir, SettingsFileName);
            string fullSettingsPath = Path.GetFullPath(settingsPath);

            // Validate file name
            if (Path.GetFileName(fullSettingsPath) != SettingsFileName)
            {
                Debug.WriteLine("Settings file name validation failed during logging settings load.");
                return new LoggingSettings();
            }

            if (File.Exists(fullSettingsPath))
            {
                // Use SecurityService for full validation even during bootstrap (null logger is acceptable)
                // This method is called during bootstrapping where DI is not yet available, so we have to create a SecurityService here
                SecurityService securityService = new SecurityService(logger: null);
                (bool isSecure, string? reason) = securityService.IsFilePathSecure(fullSettingsPath, configDir, isAssetFile: true);
                if (!isSecure && !string.IsNullOrEmpty(reason))
                {
                    Debug.WriteLine($"Settings file validation failed during bootstrap: {reason}");
                    return new LoggingSettings();
                }

                // Use secure file stream for reading
                string json;
                using (FileStream fs = securityService.CreateSecureFileStream(fullSettingsPath, FileMode.Open, FileAccess.Read))
                using (StreamReader reader = new StreamReader(fs))
                {
                    json = reader.ReadToEnd();
                }

                AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);
                return settings?.Logging ?? new LoggingSettings();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load logging settings during bootstrap: {ex.Message}");
            throw;  // fatal error if we can't load AppSettings during bootstrap
        }

        return new LoggingSettings();
    }

    /// <summary>
    /// Persists the current <see cref="Settings"/> to the settings file.
    /// Validates that the destination path resides within the application's config directory
    /// and that the file name matches the expected settings file name before writing.
    /// </summary>
    /// <remarks>
    /// Any I/O or serialization exceptions are caught and logged via the injected logger.
    /// The method overwrites the existing file by creating a new file stream via <see cref="File.Create(string)"/>.
    /// </remarks>
    public void Save()
    {
        try
        {
            // Validate that the settings path is within the expected config directory
            string configDir = GetConfigDirectory();
            string fullSettingsPath = Path.GetFullPath(_settingsPath);
            string fullConfigDir = Path.GetFullPath(configDir);

            if (!fullSettingsPath.AsSpan().StartsWith(fullConfigDir, StringComparison.OrdinalIgnoreCase))
            {
                LogError(exception: null, $"{nameof(AppSettings)} path validation failed on save.");
                return;
            }

            // Additional validation: ensure the file name is exactly "settings.json"
            if (Path.GetFileName(fullSettingsPath) != SettingsFileName)
            {
                LogError(exception: null, $"{nameof(AppSettings)} file name validation failed on save.");
                return;
            }

            // Serialize and save the settings
            string json = JsonSerializer.Serialize(Settings, _jsonOptions);
            Debug.WriteLine($"Saving settings to: {fullSettingsPath}");
            Debug.WriteLine($"Settings JSON: {json}");

            // Use File.Create to ensure we create a new file, or overwrite the existing one. This is safer than FileStream
            using FileStream fs = File.Create(fullSettingsPath);
            using StreamWriter writer = new StreamWriter(fs);
            writer.Write(json);
            writer.Flush();
        }
        catch (Exception ex)
        {
            LogError(ex, $"{nameof(AppSettings)} save failed");
        }
    }
}
