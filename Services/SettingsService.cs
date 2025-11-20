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
    /// Optional secure storage service for sensitive data.
    /// </summary>
    private readonly ISecureStorageService? _secureStorage;

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
    /// <param name="secureStorage">Optional <see cref="ISecureStorageService"/> for sensitive data.</param>
    public SettingsService(ILogger<SettingsService>? logger = null, ISecureStorageService? secureStorage = null)
    {
        _logger = logger;
        _secureStorage = secureStorage;
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
                // Note: Passing null logger to avoid circular dependency and timing issues during initialization
                SecurityService securityService = new SecurityService(logger: null);
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
            using StreamWriter writer = new StreamWriter(fs);
            writer.Write(json);
            writer.Flush();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Settings save failed");
        }
    }

    //TODO revisit these methods to finish this implementation

    /// <summary>
    /// Encrypts the specified API key using the configured secure storage service.
    /// </summary>
    /// <remarks>If the secure storage service is not available, the method returns an empty string and logs a
    /// warning. Any exceptions thrown during encryption are logged and rethrown.</remarks>
    /// <param name="plainKey">The plain text API key to encrypt. Cannot be null or empty.</param>
    /// <returns>A string containing the encrypted API key if encryption succeeds; otherwise, an empty string if the secure
    /// storage service is not configured.</returns>
    /// <exception cref="ArgumentException">Thrown when the <paramref name="plainKey"/> is null or empty.</exception>
    public string EncryptApiKey(string plainKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(plainKey);
        if (_secureStorage is null)
        {
            _logger?.LogWarning("Secure storage service is not configured yet; cannot encrypt API key.");
            return string.Empty;
        }

        try
        {
            return _secureStorage.Encrypt(plainKey);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to encrypt API key");
            throw;
        }
    }

    /// <summary>
    /// Decrypts the specified encrypted API key using the configured secure storage service.
    /// </summary>
    /// <remarks>If the secure storage service is not available, the method logs a warning and returns an
    /// empty string. Any exceptions encountered during decryption are logged and rethrown.</remarks>
    /// <param name="encryptedKey">The API key to decrypt. Must be a non-empty, encrypted string.</param>
    /// <returns>The decrypted API key as a string. Returns an empty string if the secure storage service is not configured.</returns>
    /// <exception cref="ArgumentException">Thrown when the <paramref name="encryptedKey"/> is null or empty.</exception>"
    public string DecryptApiKey(string encryptedKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(encryptedKey);
        if (_secureStorage is null)
        {
            _logger?.LogWarning("Secure storage service is not configured yet; cannot decrypt API key.");
            return string.Empty;
        }

        try
        {
            return _secureStorage.Decrypt(encryptedKey);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to decrypt API key");
            throw;
        }
    }
}
