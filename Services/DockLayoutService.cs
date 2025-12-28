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

using Dock.Model.Core;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace MermaidPad.Services;

/// <summary>
/// Provides loading and saving of dock layout settings to a per-user configuration directory.
/// Handles secure file access and validates the layout file path and name prior to I/O operations.
/// </summary>
/// <remarks>
/// <para>
/// This service follows the same patterns as <see cref="SettingsService"/> for consistency:
/// <list type="bullet">
///     <item><description>Uses <see cref="SecurityService"/> for path validation</description></item>
///     <item><description>Stores files in %APPDATA%\MermaidPad</description></item>
///     <item><description>Handles exceptions gracefully with logging</description></item>
/// </list>
/// </para>
/// <para>
/// Layout serialization uses <see cref="IDockSerializer"/> (from Dock.Serializer.SystemTextJson)
/// to persist the complete dock layout including panel positions, sizes, and proportions.
/// The serializer is injected via DI for testability.
/// </para>
/// </remarks>
public sealed class DockLayoutService
{
    /// <summary>
    /// The expected file name for persisted dock layout settings.
    /// </summary>
    private const string LayoutFileName = "layout-settings.json";

    /// <summary>
    /// Full path to the layout settings file used by this instance.
    /// </summary>
    private readonly string _layoutPath;

    /// <summary>
    /// The security service for path validation.
    /// </summary>
    private readonly SecurityService _securityService;

    /// <summary>
    /// Optional logger instance for diagnostic messages.
    /// </summary>
    private readonly ILogger<DockLayoutService>? _logger;

    /// <summary>
    /// The dock serializer for JSON (de)serialization, injected via DI.
    /// </summary>
    private readonly IDockSerializer _serializer;

    /// <summary>
    /// Initializes a new instance of the <see cref="DockLayoutService"/> class.
    /// </summary>
    /// <param name="securityService">The security service for file path validation.</param>
    /// <param name="serializer">The dock serializer for JSON (de)serialization.</param>
    /// <param name="logger">Optional logger for diagnostic messages.</param>
    public DockLayoutService(SecurityService securityService, IDockSerializer serializer, ILogger<DockLayoutService>? logger = null)
    {
        _securityService = securityService;
        _serializer = serializer;
        _logger = logger;

        string baseDir = GetConfigDirectory();
        Directory.CreateDirectory(baseDir);
        _layoutPath = Path.Combine(baseDir, LayoutFileName);
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
    /// Determines whether a saved layout file exists.
    /// </summary>
    /// <returns><see langword="true"/> if a layout file exists and passes validation; otherwise, <see langword="false"/>.</returns>
    public bool HasSavedLayout()
    {
        try
        {


            //TODO @Claude why isn't this used?


            string fullLayoutPath = Path.GetFullPath(_layoutPath);

            // Validate file name
            if (Path.GetFileName(fullLayoutPath) != LayoutFileName)
            {
                return false;
            }

            if (!File.Exists(fullLayoutPath))
            {
                return false;
            }

            // Validate path security
            string configDir = GetConfigDirectory();
            (bool isSecure, string? reason) = _securityService.IsFilePathSecure(fullLayoutPath, configDir, isAssetFile: true);

            if (!isSecure)
            {
                _logger?.LogWarning("Layout file validation failed: {Reason}", reason);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error checking for saved layout");
            return false;
        }
    }

    /// <summary>
    /// Loads the persisted dock layout from disk if a valid layout file exists.
    /// </summary>
    /// <typeparam name="T">The type of the root dockable to deserialize.</typeparam>
    /// <returns>
    /// The deserialized layout if successful; otherwise, <see langword="null"/>.
    /// </returns>
    /// <remarks>
    /// This method catches exceptions and logs errors via the injected logger when available.
    /// If loading fails or validation fails, returns null so the caller can create a default layout.
    /// </remarks>
    public T? Load<T>() where T : class, IDockable
    {
        try
        {

            //TODO @Claude why isn't this used?


            string configDir = GetConfigDirectory();
            string fullLayoutPath = Path.GetFullPath(_layoutPath);

            // Validate file name
            if (Path.GetFileName(fullLayoutPath) != LayoutFileName)
            {
                _logger?.LogWarning("Layout file name validation failed on load");
                return null;
            }

            if (!File.Exists(fullLayoutPath))
            {
                _logger?.LogInformation("No saved layout file found at {Path}", fullLayoutPath);
                return null;
            }

            // Use SecurityService for comprehensive validation
            (bool isSecure, string? reason) = _securityService.IsFilePathSecure(fullLayoutPath, configDir, isAssetFile: true);
            if (!isSecure)
            {
                _logger?.LogError("Layout file validation failed: {Reason}", reason);
                return null;
            }

            // Use SecurityService for secure file stream creation
            string json;
            using (FileStream fs = _securityService.CreateSecureFileStream(fullLayoutPath, FileMode.Open, FileAccess.Read))
            using (StreamReader reader = new StreamReader(fs))
            {
                json = reader.ReadToEnd();
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                _logger?.LogWarning("Layout file is empty");
                return null;
            }

            T? layout = _serializer.Deserialize<T>(json);
            if (layout is null)
            {
                _logger?.LogWarning("Failed to deserialize layout - result was null");
                return null;
            }

            _logger?.LogInformation("Dock layout loaded successfully from {Path}", fullLayoutPath);
            return layout;
        }
        catch (JsonException jsonEx)
        {
            _logger?.LogError(jsonEx, "JSON deserialization error loading dock layout");
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading dock layout");
            return null;
        }
    }

    /// <summary>
    /// Persists the specified dock layout to the layout settings file.
    /// </summary>
    /// <typeparam name="T">The type of the root dockable to serialize.</typeparam>
    /// <param name="layout">The dock layout to save. If null, the method returns without saving.</param>
    /// <returns><see langword="true"/> if the layout was saved successfully; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// <para>
    /// Validates that the destination path resides within the application's config directory
    /// and that the file name matches the expected layout file name before writing.
    /// </para>
    /// <para>
    /// Any I/O or serialization exceptions are caught and logged via the injected logger.
    /// The method overwrites the existing file by creating a new file stream.
    /// </para>
    /// </remarks>
    public bool Save<T>(T? layout) where T : class, IDockable
    {
        if (layout is null)
        {
            _logger?.LogWarning("Cannot save null layout");
            return false;
        }

        try
        {
            string configDir = GetConfigDirectory();
            string fullLayoutPath = Path.GetFullPath(_layoutPath);
            string fullConfigDir = Path.GetFullPath(configDir);

            // Validate path is within config directory
            if (!fullLayoutPath.StartsWith(fullConfigDir, StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogError("Layout path validation failed on save - path outside config directory");
                Debug.WriteLine("Layout path validation failed on save.");
                return false;
            }

            // Validate file name
            if (Path.GetFileName(fullLayoutPath) != LayoutFileName)
            {
                _logger?.LogError("Layout file name validation failed on save");
                Debug.WriteLine("Layout file name validation failed on save.");
                return false;
            }

            // Serialize the layout
            string json = _serializer.Serialize(layout);

            if (string.IsNullOrWhiteSpace(json))
            {
                _logger?.LogWarning("Serialization produced empty result");
                return false;
            }

            _logger?.LogDebug("Saving layout to: {Path}", fullLayoutPath);

            // Use File.Create to ensure we create a new file, or overwrite the existing one


            //TODO @Claude shouldn't this use async i/o?


            using FileStream fs = File.Create(fullLayoutPath);
            using StreamWriter writer = new StreamWriter(fs);
            writer.Write(json);
            writer.Flush();

            _logger?.LogInformation("Dock layout saved successfully to {Path}", fullLayoutPath);
            return true;
        }
        catch (JsonException jsonEx)
        {
            _logger?.LogError(jsonEx, "JSON serialization error saving dock layout");
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error saving dock layout");
            return false;
        }
    }

    /// <summary>
    /// Deletes the saved layout file if it exists.
    /// </summary>
    /// <returns><see langword="true"/> if the file was deleted or didn't exist; <see langword="false"/> if deletion failed.</returns>
    /// <remarks>
    /// This method can be used to reset the layout to defaults by deleting the saved file.
    /// </remarks>
    public bool DeleteSavedLayout()
    {
        try
        {
            string fullLayoutPath = Path.GetFullPath(_layoutPath);
            if (!File.Exists(fullLayoutPath))
            {
                return true;
            }

            File.Delete(fullLayoutPath);
            _logger?.LogInformation("Deleted saved layout file at {Path}", fullLayoutPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting saved layout file");
            return false;
        }
    }
}
