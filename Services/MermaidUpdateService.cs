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

using MermaidPad.Extensions;
using MermaidPad.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace MermaidPad.Services;

/// <summary>
/// Provides functionality to check for, download, and install updates for the Mermaid.js library.
/// </summary>
public sealed class MermaidUpdateService
{
    /// <summary>
    /// Gets the directory where Mermaid assets are stored.
    /// </summary>
    private string AssetDir { get; }

    private readonly AppSettings _settings;
    private readonly ILogger<MermaidUpdateService> _logger;
    private readonly HttpClient _httpClient;
    private const string MermaidMinJsFileName = "mermaid.min.js";

    /// <summary>
    /// Initializes a new instance of the <see cref="MermaidUpdateService"/> class.
    /// </summary>
    /// <param name="settings">Application settings containing Mermaid configuration.</param>
    /// <param name="assetDir">Directory path for storing Mermaid assets.</param>
    /// <param name="httpClientFactory">Factory to create HttpClient instances.</param>
    /// <param name="logger">Logger instance for structured logging.</param>
    public MermaidUpdateService(AppSettings settings, string assetDir, IHttpClientFactory httpClientFactory, ILogger<MermaidUpdateService> logger)
    {
        _settings = settings;
        AssetDir = assetDir;
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;

        _logger.LogInformation("MermaidUpdateService initialized with AssetDir: {AssetDir}", AssetDir);
        _logger.LogInformation("Auto-update enabled: {AutoUpdateEnabled}, Current bundled version: {BundledVersion}",
            _settings.AutoUpdateMermaid, _settings.BundledMermaidVersion);
    }

    /// <summary>
    /// Gets the full path to the bundled Mermaid.js file.
    /// </summary>
    public string BundledMermaidPath => Path.Combine(AssetDir, MermaidMinJsFileName);

    /// <summary>
    /// Checks for a newer version of Mermaid.js and updates the local copy if available.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task CheckAndUpdateAsync()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("=== Mermaid Update Check Started ===");

        if (!_settings.AutoUpdateMermaid)
        {
            _logger.LogInformation("Auto-update disabled, skipping update check");
            return;
        }

        try
        {
            _logger.LogInformation("Fetching latest Mermaid version from npm registry...");
            (string remoteVersion, string url) = await FetchLatestVersionAsync();

            _settings.LatestCheckedMermaidVersion = remoteVersion;
            _logger.LogInformation("Latest version check completed: {RemoteVersion}", remoteVersion);

            if (IsNewer(remoteVersion, _settings.BundledMermaidVersion))
            {
                _logger.LogInformation("Update available: {CurrentVersion} -> {NewVersion}", _settings.BundledMermaidVersion, remoteVersion);

                await DownloadAndInstallUpdateAsync(url, remoteVersion);

                stopwatch.Stop();
                _logger.LogTiming("Mermaid update (with download)", stopwatch.Elapsed, success: true);
                _logger.LogInformation("=== Mermaid Update Completed Successfully: {Version} ===", remoteVersion);
            }
            else
            {
                stopwatch.Stop();
                _logger.LogTiming("Mermaid update (no download needed)", stopwatch.Elapsed, success: true);
                _logger.LogInformation("=== Mermaid Already Up-to-Date: {Version} ===", _settings.BundledMermaidVersion);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogTiming("Mermaid update", stopwatch.Elapsed, success: false);
            _logger.LogError(ex, "Mermaid update failed");
            Debug.WriteLine($"Mermaid update failed: {ex}");
        }
    }

    /// <summary>
    /// Downloads the specified Mermaid.js file and installs it, updating the local version.
    /// </summary>
    /// <param name="url">URL to download the Mermaid.js file from.</param>
    /// <param name="newVersion">The new version string to update to.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task DownloadAndInstallUpdateAsync(string url, string newVersion)
    {
        Stopwatch downloadStopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Downloading Mermaid.js from: {Url}", url);

        try
        {
            // Step 1: Download to temporary file
            string tmpPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            _logger.LogDebug("Using temporary file: {TempPath}", tmpPath);

            string jsContent = await _httpClient.GetStringAsync(url);
            downloadStopwatch.Stop();

            _logger.LogInformation("Download completed: {CharacterCount:N0} characters in {ElapsedMs}ms",
                jsContent.Length, downloadStopwatch.ElapsedMilliseconds);

            // Step 2: Write to temp file
            await File.WriteAllTextAsync(tmpPath, jsContent);
            _logger.LogDebug("Content written to temporary file");

            // Step 3: Backup existing file (if it exists)
            string backupPath = BundledMermaidPath + ".backup";
            if (File.Exists(BundledMermaidPath))
            {
                File.Copy(BundledMermaidPath, backupPath, overwrite: true);
                _logger.LogInformation("Existing {FileName} backed up to: {BackupPath}", MermaidMinJsFileName, backupPath);
            }

            // Step 4: Install new version
            File.Copy(tmpPath, BundledMermaidPath, overwrite: true);
            _logger.LogAsset("updated", MermaidMinJsFileName, true, new FileInfo(BundledMermaidPath).Length);

            // Step 5: Update version in settings
            //TODO - DaveBlack: re-enable this once the Update mechanism is back in place
            //_settings.BundledMermaidVersion = newVersion;
            _logger.LogInformation("Bundled version updated to: {NewVersion}", newVersion);

            // Step 6: Cleanup
            try
            {
                string tempDir = Path.GetFullPath(Path.GetTempPath());
                FileInfo tmpFileInfo = new FileInfo(tmpPath);
                string tmpFileDir = Path.GetFullPath(tmpFileInfo.DirectoryName ?? "");

                // Ensure the temp file is inside the temp directory and is a direct child (not a symlink or traversal)
                if (tmpFileDir == tempDir && tmpFileInfo.Exists && tmpFileInfo.FullName.StartsWith(tempDir, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(tmpFileInfo.FullName);
                    _logger.LogDebug("Temporary file cleaned up");
                }
                else
                {
                    _logger.LogError("Refusing to delete file outside temp directory or with suspicious path: {FilePath}", tmpFileInfo.FullName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during temp file cleanup");
            }

            // Step 7: Verify installation
            await VerifyInstallationAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download and install Mermaid update from {Url}", url);
            throw;
        }
    }

    /// <summary>
    /// Verifies the integrity and validity of the installed Mermaid.js file.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task VerifyInstallationAsync()
    {
        try
        {
            _logger.LogInformation("Verifying Mermaid installation...");

            if (!File.Exists(BundledMermaidPath))
            {
                throw new InvalidOperationException("Mermaid file does not exist after installation");
            }

            FileInfo fileInfo = new FileInfo(BundledMermaidPath);
            _logger.LogInformation("Installed file size: {FileSize:N0} bytes", fileInfo.Length);

            // Basic content validation
            string content = await File.ReadAllTextAsync(BundledMermaidPath);
            if (content.Length < 1000) // Mermaid should be much larger than 1KB
            {
                throw new InvalidOperationException($"Installed file appears to be too small ({content.Length} chars)");
            }

            //TODO: security: compare against known good hashes for specific versions to avoid tampering
            //TODO performance: try and see if we can compare a hash from the CDN instead of reading full content and searching for "mermaid". This will also make things more secure.
            if (!content.AsSpan().Contains("mermaid", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Installed file does not appear to contain Mermaid content");
            }

            _logger.LogInformation("Mermaid installation verified successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mermaid installation verification failed");
            throw;
        }
    }

    /// <summary>
    /// Fetches the latest Mermaid.js version and its download URL from the npm registry.
    /// </summary>
    /// <returns>
    /// A tuple containing the latest version string and the download URL for Mermaid.js.
    /// </returns>
    [SuppressMessage("Minor Code Smell", "S1075:URIs should not be hardcoded", Justification = "Hardcoded to specific unpkg.com URL")]
    private async Task<(string version, string jsUrl)> FetchLatestVersionAsync()
    {
        const string mermaidUrlPrefix = "https://unpkg.com/mermaid";
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Fetching package.json from unpkg.com...");

            // unpkg exposes package.json
            string pkgJson = await _httpClient.GetStringAsync($"{mermaidUrlPrefix}/package.json");
            stopwatch.Stop();

            _logger.LogInformation("Package.json fetched in {ElapsedMs}ms: {CharacterCount} characters",
                stopwatch.ElapsedMilliseconds, pkgJson.Length);

            using JsonDocument doc = JsonDocument.Parse(pkgJson);
            string version = doc.RootElement.GetProperty("version").GetString() ?? "0.0.0";
            const string jsUrl = $"{mermaidUrlPrefix}/dist/{MermaidMinJsFileName}";

            _logger.LogInformation("Latest Mermaid version discovered: {Version}", version);
            _logger.LogDebug("Download URL will be: {Url}", jsUrl);

            _settings.LatestCheckedMermaidVersion = version;
            return (version, jsUrl);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse package.json from unpkg.com");
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed while fetching version info");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching latest version");
            throw;
        }
    }

    /// <summary>
    /// Determines whether the remote Mermaid.js version is newer than the local version.
    /// </summary>
    /// <param name="remote">The remote version string.</param>
    /// <param name="local">The local version string.</param>
    /// <returns><c>true</c> if the remote version is newer; otherwise, <c>false</c>.</returns>
    private bool IsNewer(string remote, string local)
    {
        bool canParseRemote = Version.TryParse(remote, out Version? rv);
        bool canParseLocal = Version.TryParse(local, out Version? lv);

        _logger.LogDebug("Version comparison: remote='{RemoteVersion}' ({RemoteStatus}), local='{LocalVersion}' ({LocalStatus})",
            remote, canParseRemote ? "parsed" : "failed", local, canParseLocal ? "parsed" : "failed");

        if (!canParseRemote)
        {
            _logger.LogWarning("Cannot parse remote version '{RemoteVersion}', assuming not newer", remote);
            return false;
        }

        if (!canParseLocal)
        {
            _logger.LogWarning("Cannot parse local version '{LocalVersion}', assuming remote is newer", local);
            return true;
        }

        bool isNewer = rv! > lv!;
        _logger.LogDebug("Version comparison result: {RemoteVersion} > {LocalVersion} = {IsNewer}", rv, lv, isNewer);

        return isNewer;
    }
}
