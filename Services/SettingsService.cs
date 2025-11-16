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
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace MermaidPad.Services;

/// <summary>
/// Provides loading and saving of application settings to a per-user configuration directory.
/// Handles secure file access and validates the settings file path and name prior to I/O operations.
/// </summary>
public sealed class SettingsService
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
    /// Optional logger instance; may be <see langword="null"/> during early initialization.
    /// </summary>
    private readonly ILogger<SettingsService>? _logger;

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
    /// <param name="logger">
    /// Optional <see cref="ILogger{SettingsService}"/> for diagnostic messages. May be <see langword="null"/>.
    /// </param>
    public SettingsService(ILogger<SettingsService>? logger = null)
    {
        _logger = logger;
        string baseDir = GetConfigDirectory();
        Directory.CreateDirectory(baseDir);
        _settingsPath = Path.Combine(baseDir, SettingsFileName);
        Settings = Load();
    }

    /// <summary>
    /// Returns the per-user configuration directory path used by the application.
    /// Typically resolves to "%APPDATA%\MermaidPad" on Windows.
    /// </summary>
    /// <returns>The full path to the application's config directory for the current user.</returns>
    private static string GetConfigDirectory()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "MermaidPad");
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
                Debug.WriteLine("Settings file name validation failed on load.");
                return new AppSettings();
            }

            if (File.Exists(fullSettingsPath))
            {
                // Use SecurityService for comprehensive validation
                var securityService = new SecurityService(logger: null);
                (bool isSecure, string? reason) = securityService.IsFilePathSecure(fullSettingsPath, configDir, isAssetFile: true);
                if (!isSecure && !string.IsNullOrEmpty(reason))
                {
                    _logger?.LogError("Settings file validation failed: {Reason}", reason);
                    return new AppSettings();
                }

                // Use SecurityService for secure file stream creation
                string json;
                using (FileStream fs = securityService.CreateSecureFileStream(fullSettingsPath, FileMode.Open, FileAccess.Read))
                using (StreamReader reader = new StreamReader(fs))
                {
                    json = reader.ReadToEnd();
                }

                return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Settings load failed");
        }
        return new AppSettings();
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

            if (!fullSettingsPath.StartsWith(fullConfigDir, StringComparison.OrdinalIgnoreCase))
            {
                Debug.WriteLine("Settings path validation failed on save.");
                return;
            }

            // Additional validation: ensure the file name is exactly "settings.json"
            if (Path.GetFileName(fullSettingsPath) != SettingsFileName)
            {
                Debug.WriteLine("Settings file name validation failed on save.");
                return;
            }

            // Serialize and save the settings
            string json = JsonSerializer.Serialize(Settings, _jsonOptions);
            Debug.WriteLine($"Saving settings to: {fullSettingsPath}");
            Debug.WriteLine($"Settings JSON: {json}");

            // Use File.Create to ensure we create a new file, or overwrite the existing one. This is safer than FileStream
            using FileStream fs = File.Create(fullSettingsPath);
            using StreamWriter writer = new(fs);
            writer.Write(json);
            writer.Flush();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Settings save failed");
        }
    }
}
