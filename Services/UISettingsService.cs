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
using System.Text.Json;

namespace MermaidPad.Services;

/// <summary>
/// Provides loading and saving of UI-specific settings to a per-user configuration directory.
/// This service is separate from <see cref="SettingsService"/> to decouple UI state (window position,
/// dock layout, etc.) from application settings.
/// </summary>
public sealed class UISettingsService : SettingsBase
{
    /// <summary>
    /// <see cref="JsonSerializerOptions"/> used for (de)serializing <see cref="UISettings"/>.
    /// Configured to ignore case when matching property names and to write indented JSON.
    /// </summary>
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    /// <summary>
    /// Security service for validating file paths and creating secure file streams.
    /// </summary>
    private readonly SecurityService _securityService;

    /// <summary>
    /// Full path to the UI settings file used by this instance.
    /// </summary>
    private readonly string _settingsPath;

    /// <summary>
    /// The expected file name for persisted UI settings.
    /// </summary>
    private const string UISettingsFileName = "ui-settings.json";

    /// <summary>
    /// The in-memory UI settings instance. Consumers may read or modify properties
    /// and then call <see cref="Save"/> to persist changes.
    /// </summary>
    public UISettings Settings { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UISettingsService"/> class.
    /// Ensures the configuration directory exists and loads persisted UI settings if available.
    /// </summary>
    /// <param name="securityService">Security service for validating file paths and creating secure file streams.</param>
    /// <param name="logger">
    /// Optional <see cref="ILogger{UISettingsService}"/> for diagnostic messages. May be <see langword="null"/>.
    /// </param>
    public UISettingsService(SecurityService securityService, ILogger<UISettingsService>? logger = null)
        : base(logger)
    {
        _securityService = securityService;

        string baseDir = GetConfigDirectory();
        Directory.CreateDirectory(baseDir);
        _settingsPath = Path.Combine(baseDir, UISettingsFileName);
        Settings = Load();
    }

    /// <summary>
    /// Loads persisted <see cref="UISettings"/> from disk if a valid settings file exists.
    /// Performs validation of the file name and path using <see cref="SecurityService"/>.
    /// If loading fails or validation fails, returns a new default <see cref="UISettings"/> instance.
    /// </summary>
    /// <remarks>
    /// This method catches exceptions and logs errors via the injected logger when available.
    /// </remarks>
    /// <returns>The deserialized <see cref="UISettings"/> from disk, or a new default instance.</returns>
    private UISettings Load()
    {
        try
        {
            // Validate that the settings path is within the expected config directory
            string configDir = GetConfigDirectory();
            string fullSettingsPath = Path.GetFullPath(_settingsPath);

            // Additional validation: ensure the file name is exactly "ui-settings.json"
            if (Path.GetFileName(fullSettingsPath) != UISettingsFileName)
            {
                LogWarning(exception: null, $"{nameof(UISettings)} file name validation failed on load");
                return new UISettings();
            }

            if (File.Exists(fullSettingsPath))
            {
                // Use SecurityService for comprehensive validation
                // Note: Passing null logger to avoid circular dependency and timing issues during initialization
                (bool isSecure, string? reason) = _securityService.IsFilePathSecure(fullSettingsPath, configDir, isAssetFile: true);
                if (!isSecure && !string.IsNullOrEmpty(reason))
                {
                    LogError(exception: null, $"{nameof(UISettings)} file validation failed: {reason}");
                    return new UISettings();
                }

                // Use SecurityService for secure file stream creation
                string json;
                using (FileStream fs = _securityService.CreateSecureFileStream(fullSettingsPath, FileMode.Open, FileAccess.Read))
                using (StreamReader reader = new StreamReader(fs))
                {
                    json = reader.ReadToEnd();
                }

                UISettings? settings = JsonSerializer.Deserialize<UISettings>(json, _jsonOptions);
                if (settings is not null)
                {
                    LogInformation($"{nameof(UISettings)} loaded successfully from {fullSettingsPath}");
                    return settings;
                }
            }
            else
            {
                LogInformation($"{nameof(UISettings)} file not found, using defaults");
            }
        }
        catch (Exception ex)
        {
            LogError(ex, "UI settings load failed");
        }
        return new UISettings();
    }

    /// <summary>
    /// Persists the current <see cref="Settings"/> to the UI settings file.
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
                LogError(exception: null, $"{nameof(UISettings)} path validation failed on save");
                return;
            }

            // Additional validation: ensure the file name is exactly "ui-settings.json"
            if (Path.GetFileName(fullSettingsPath) != UISettingsFileName)
            {
                LogError(exception: null, $"{nameof(UISettings)} file name validation failed on save");
                return;
            }

            // Serialize and save the settings
            string json = JsonSerializer.Serialize(Settings, _jsonOptions);

            // Use File.Create to ensure we create a new file, or overwrite the existing one
            using FileStream fs = File.Create(fullSettingsPath);
            using StreamWriter writer = new StreamWriter(fs);
            writer.Write(json);
            writer.Flush();

            LogInformation($"{nameof(UISettings)} saved successfully to {fullSettingsPath}");
        }
        catch (Exception ex)
        {
            LogError(ex, "UI settings save failed");
        }
    }
}
