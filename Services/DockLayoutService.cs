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

using Dock.Model.Controls;
using Dock.Model.Core;
using Microsoft.Extensions.Logging;

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
///     <item><description>Stores files in <c>Environment.SpecialFolder.ApplicationData/MermaidPad</c></description></item>
///     <item><description>Handles exceptions gracefully with logging</description></item>
/// </list>
/// </para>
/// <para>
/// Layout serialization uses <see cref="IDockSerializer"/> (from Dock.Serializer.SystemTextJson)
/// with stream-based <see cref="IDockSerializer.Save"/> and <see cref="IDockSerializer.Load{T}"/>
/// methods to persist the complete dock layout including panel positions, sizes, and proportions.
/// </para>
/// <para>
/// The <see cref="IDockState"/> is used in conjunction with the serializer to capture and restore
/// the dock's internal state (focus, active dockables, etc.) across save/load operations.
/// </para>
/// </remarks>
public sealed class DockLayoutService
{
    /// <summary>
    /// Represents the default buffer size, in bytes, used for internal data operations.
    /// </summary>
    private const int DefaultBufferSize = 16_384;   // 16 KB

    /// <summary>
    /// The expected file name for persisted dock layout settings.
    /// </summary>
    private const string LayoutFileName = "layout-settings.json";

    /// <summary>
    /// Full path to the layout settings file used by this instance.
    /// </summary>
    private readonly string _layoutPath;

    /// <summary>
    /// The <see cref="SecurityService"/> for path validation.
    /// </summary>
    private readonly SecurityService _securityService;

    /// <summary>
    /// The <see cref="ILogger{DockLayoutService}"/> instance for diagnostic messages.
    /// </summary>
    private readonly ILogger<DockLayoutService> _logger;

    /// <summary>
    /// The <see cref="IDockSerializer"/> for stream-based (de)serialization.
    /// </summary>
    private readonly IDockSerializer _serializer;

    /// <summary>
    /// The <see cref="IDockState"/> manager for capturing and restoring layout state.
    /// </summary>
    private readonly IDockState _dockState;

    /// <summary>
    /// A <see cref="Lock"/> to synchronize file access for load/save operations.
    /// </summary>
    private readonly Lock _fileLock = new Lock();

    /// <summary>
    /// Initializes a new instance of the <see cref="DockLayoutService"/> class.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger{DockLayoutService}"/> instance for diagnostic messages.</param>
    /// <param name="securityService">The <see cref="SecurityService"/> for file path validation.</param>
    /// <param name="serializer">The <see cref="IDockSerializer"/> for stream-based (de)serialization.</param>
    /// <param name="dockState">The <see cref="IDockState"/> manager for capturing and restoring layout state.</param>
    public DockLayoutService(
        ILogger<DockLayoutService> logger,
        SecurityService securityService,
        IDockSerializer serializer,
        IDockState dockState)
    {
        _logger = logger;
        _securityService = securityService;
        _serializer = serializer;
        _dockState = dockState;

        string baseDir = GetConfigDirectory();
        Directory.CreateDirectory(baseDir);

        //TODO - DaveBlack: (refactor) getting layout path logic into AssetService or SecurityService or abstract ServicesBase
        _layoutPath = Path.Combine(baseDir, LayoutFileName);
    }

    /// <summary>
    /// Returns the per-user configuration directory path used by the application.
    /// Typically resolves to "%APPDATA%\MermaidPad" on Windows.
    /// </summary>
    /// <returns>The full path to the application's config directory for the current user.</returns>
    private static string GetConfigDirectory()
    {
        //TODO - DaveBlack: (refactor) getting config directory logic into AssetService or SecurityService or abstract ServicesBase
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "MermaidPad");
    }

    /// <summary>
    /// Captures the current dock state for later restoration.
    /// </summary>
    /// <param name="layout">The layout to capture state from.</param>
    /// <remarks>
    /// Call this method after creating the initial layout to enable state restoration
    /// when loading a saved layout. The captured state includes focus information,
    /// active dockables, and other internal dock state.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when layout is null.</exception>
    public void CaptureState(IRootDock layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        _dockState.Save(layout);
        _logger.LogDebug("Dock state captured for layout: {LayoutId}", layout.Id);
    }

    /// <summary>
    /// Restores the previously captured dock state to the specified layout.
    /// </summary>
    /// <param name="layout">The layout to restore state to.</param>
    /// <remarks>
    /// Call this method after loading a layout from file to restore the internal
    /// dock state (focus, active dockables, etc.) that was captured before saving.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when layout is null.</exception>
    public void RestoreState(IRootDock layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        _dockState.Restore(layout);
        _logger.LogDebug("Dock state restored for layout: {LayoutId}", layout.Id);
    }

    /// <summary>
    /// Loads the persisted dock layout from disk if a valid layout file exists.
    /// </summary>
    /// <returns>
    /// The deserialized layout if successful; otherwise, <see langword="null"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method uses the stream-based <see cref="IDockSerializer.Load{T}"/> method
    /// which handles circular references and NaN values correctly.
    /// </para>
    /// <para>
    /// After loading, call <see cref="RestoreState"/> to restore the internal dock state,
    /// then call <c>Factory.InitLayout</c> to initialize the layout.
    /// </para>
    /// </remarks>
    public IRootDock? Load()
    {
        try
        {
            //TODO - DaveBlack: (refactor) this path validation logic should be done once at startup in SecurityService or AssetService
            string fullLayoutPath = Path.GetFullPath(_layoutPath);

            // Validate file name
            if (Path.GetFileName(fullLayoutPath) != LayoutFileName)
            {
                _logger.LogWarning("Layout file name validation failed on load");
                return null;
            }

            if (!File.Exists(fullLayoutPath))
            {
                _logger.LogInformation("No saved layout file found at {Path}", fullLayoutPath);
                return null;
            }

            IRootDock? layout;
            lock (_fileLock)
            {
                // Use SecurityService for comprehensive validation
                using FileStream stream = _securityService.CreateSecureFileStream(fullLayoutPath, FileMode.Open, FileAccess.Read, FileShare.Read, DefaultBufferSize);
                layout = _serializer.Load<IRootDock>(stream);
            }

            if (layout is null)
            {
                _logger.LogWarning("Failed to deserialize layout - result was null");
                return null;
            }

            _logger.LogInformation("Dock layout loaded successfully from {Path}", fullLayoutPath);
            return layout;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dock layout");
            return null;
        }
    }

    /// <summary>
    /// Persists the specified dock layout to the layout settings file synchronously.
    /// </summary>
    /// <param name="layout">The dock layout to save. If null, the method returns without saving.</param>
    /// <returns><see langword="true"/> if the layout was saved successfully; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// <para>
    /// This method uses the stream-based <see cref="IDockSerializer.Save"/> method
    /// which handles circular references and NaN values correctly.
    /// </para>
    /// <para>
    /// Before saving, consider calling <see cref="CaptureState"/> to capture the current
    /// dock state for later restoration.
    /// </para>
    /// </remarks>
    public bool Save(IRootDock? layout)
    {
        if (layout is null)
        {
            _logger.LogWarning("Cannot save null layout");
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
                _logger.LogError("Layout path validation failed on save - path outside config directory");
                return false;
            }

            // Validate file name
            if (Path.GetFileName(fullLayoutPath) != LayoutFileName)
            {
                _logger.LogError("Layout file name validation failed on save");
                return false;
            }

            lock (_fileLock)
            {
                using FileStream stream = _securityService.CreateSecureFileStream(fullLayoutPath, FileMode.Create, FileAccess.Write, FileShare.None, DefaultBufferSize);
                _serializer.Save(stream, layout);
            }

            _logger.LogInformation("Dock layout saved successfully (synchronous) to {Path}", fullLayoutPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving dock layout synchronously");
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
            _logger.LogInformation("Deleted saved layout file at {Path}", fullLayoutPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting saved layout file");
            return false;
        }
    }
}
