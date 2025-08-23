using MermaidPad.Models;
using System.Diagnostics;
using System.Text.Json;

namespace MermaidPad.Services;

public sealed class MermaidUpdateService
{
    private string AssetDir { get; }
    private readonly AppSettings _settings;
    private static readonly HttpClient _http = new HttpClient();
    private const string MermaidMinJsFileName = "mermaid.min.js";

    public MermaidUpdateService(AppSettings settings, string assetDir)
    {
        _settings = settings;
        AssetDir = assetDir;

        SimpleLogger.Log($"MermaidUpdateService initialized with AssetDir: {AssetDir}");
        SimpleLogger.Log($"Auto-update enabled: {_settings.AutoUpdateMermaid}, Current bundled version: {_settings.BundledMermaidVersion}");
    }

    public string BundledMermaidPath => Path.Combine(AssetDir, MermaidMinJsFileName);

    public async Task CheckAndUpdateAsync()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        SimpleLogger.Log("=== Mermaid Update Check Started ===");

        if (!_settings.AutoUpdateMermaid)
        {
            SimpleLogger.Log("Auto-update disabled, skipping update check");
            return;
        }

        try
        {
            SimpleLogger.Log("Fetching latest Mermaid version from npm registry...");
            (string remoteVersion, string url) = await FetchLatestVersionAsync();

            _settings.LatestCheckedMermaidVersion = remoteVersion;
            SimpleLogger.Log($"Latest version check completed: {remoteVersion}");

            if (IsNewer(remoteVersion, _settings.BundledMermaidVersion))
            {
                SimpleLogger.Log($"Update available: {_settings.BundledMermaidVersion} -> {remoteVersion}");

                await DownloadAndInstallUpdateAsync(url, remoteVersion);

                stopwatch.Stop();
                SimpleLogger.LogTiming("Mermaid update (with download)", stopwatch.Elapsed, success: true);
                SimpleLogger.Log($"=== Mermaid Update Completed Successfully: {remoteVersion} ===");
            }
            else
            {
                stopwatch.Stop();
                SimpleLogger.LogTiming("Mermaid update (no download needed)", stopwatch.Elapsed, success: true);
                SimpleLogger.Log($"=== Mermaid Already Up-to-Date: {_settings.BundledMermaidVersion} ===");
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            SimpleLogger.LogTiming("Mermaid update", stopwatch.Elapsed, success: false);
            SimpleLogger.LogError("Mermaid update failed", ex);
            Debug.WriteLine($"Mermaid update failed: {ex}");
        }
    }

    private async Task DownloadAndInstallUpdateAsync(string url, string newVersion)
    {
        Stopwatch downloadStopwatch = Stopwatch.StartNew();
        SimpleLogger.Log($"Downloading Mermaid.js from: {url}");

        try
        {
            // Step 1: Download to temporary file
            string tmp = Path.GetTempFileName();
            SimpleLogger.Log($"Using temporary file: {tmp}");

            string jsContent = await _http.GetStringAsync(url);
            downloadStopwatch.Stop();

            SimpleLogger.Log($"Download completed: {jsContent.Length:N0} characters in {downloadStopwatch.ElapsedMilliseconds}ms");

            // Step 2: Write to temp file
            await File.WriteAllTextAsync(tmp, jsContent);
            SimpleLogger.Log("Content written to temporary file");

            // Step 3: Backup existing file (if it exists)
            string backupPath = BundledMermaidPath + ".backup";
            if (File.Exists(BundledMermaidPath))
            {
                File.Copy(BundledMermaidPath, backupPath, overwrite: true);
                SimpleLogger.Log($"Existing {MermaidMinJsFileName} backed up to: {backupPath}");
            }

            // Step 4: Install new version
            File.Copy(tmp, BundledMermaidPath, overwrite: true);
            SimpleLogger.LogAsset("updated", MermaidMinJsFileName, true, new FileInfo(BundledMermaidPath).Length);

            // Step 5: Update version in settings
            _settings.BundledMermaidVersion = newVersion;
            SimpleLogger.Log($"Bundled version updated to: {newVersion}");

            // Step 6: Cleanup
            // Use Path.GetTempFileName() output validation and FileInfo for strict checks
            try
            {
                var tempDir = Path.GetFullPath(Path.GetTempPath());
                var tmpFileInfo = new FileInfo(tmp);
                var tmpFileDir = Path.GetFullPath(tmpFileInfo.DirectoryName ?? "");

                // Ensure the temp file is inside the temp directory and is a direct child (not a symlink or traversal)
                if (tmpFileDir == tempDir && tmpFileInfo.Exists && tmpFileInfo.FullName.StartsWith(tempDir, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(tmpFileInfo.FullName);
                    SimpleLogger.Log("Temporary file cleaned up");
                }
                else
                {
                    SimpleLogger.LogError($"Refusing to delete file outside temp directory or with suspicious path: {tmpFileInfo.FullName}", null);
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError("Error during temp file cleanup", ex);
            }

            // Step 7: Verify installation
            await VerifyInstallationAsync(newVersion);
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError($"Failed to download and install Mermaid update from {url}", ex);
            throw;
        }
    }

    private async Task VerifyInstallationAsync(string _)
    {
        try
        {
            SimpleLogger.Log("Verifying Mermaid installation...");

            if (!File.Exists(BundledMermaidPath))
            {
                throw new InvalidOperationException("Mermaid file does not exist after installation");
            }

            FileInfo fileInfo = new FileInfo(BundledMermaidPath);
            SimpleLogger.Log($"Installed file size: {fileInfo.Length:N0} bytes");

            // Basic content validation
            string content = await File.ReadAllTextAsync(BundledMermaidPath);
            if (content.Length < 1000) // Mermaid should be much larger than 1KB
            {
                throw new InvalidOperationException($"Installed file appears to be too small ({content.Length} chars)");
            }

            if (!content.Contains("mermaid"))
            {
                throw new InvalidOperationException("Installed file does not appear to contain Mermaid content");
            }

            SimpleLogger.Log("Mermaid installation verified successfully");
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError("Mermaid installation verification failed", ex);
            throw;
        }
    }

    private async Task<(string version, string jsUrl)> FetchLatestVersionAsync()
    {
        const string mermaidUrlPrefix = "https://unpkg.com/mermaid";
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            SimpleLogger.Log("Fetching package.json from unpkg.com...");

            // unpkg exposes package.json
            string pkgJson = await _http.GetStringAsync($"{mermaidUrlPrefix}/package.json");
            stopwatch.Stop();

            SimpleLogger.Log($"Package.json fetched in {stopwatch.ElapsedMilliseconds}ms: {pkgJson.Length} characters");

            using JsonDocument doc = JsonDocument.Parse(pkgJson);
            string version = doc.RootElement.GetProperty("version").GetString() ?? "0.0.0";
            const string jsUrl = $"{mermaidUrlPrefix}/dist/{MermaidMinJsFileName}";

            SimpleLogger.Log($"Latest Mermaid version discovered: {version}");
            SimpleLogger.Log($"Download URL will be: {jsUrl}");

            _settings.LatestCheckedMermaidVersion = version;
            return (version, jsUrl);
        }
        catch (JsonException ex)
        {
            SimpleLogger.LogError("Failed to parse package.json from unpkg.com", ex);
            throw;
        }
        catch (HttpRequestException ex)
        {
            SimpleLogger.LogError("HTTP request failed while fetching version info", ex);
            throw;
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError("Unexpected error while fetching latest version", ex);
            throw;
        }
    }

    private static bool IsNewer(string remote, string local)
    {
        bool canParseRemote = Version.TryParse(remote, out Version? rv);
        bool canParseLocal = Version.TryParse(local, out Version? lv);

        SimpleLogger.Log($"Version comparison: remote='{remote}' ({(canParseRemote ? "parsed" : "failed")}), local='{local}' ({(canParseLocal ? "parsed" : "failed")})");

        if (!canParseRemote)
        {
            SimpleLogger.Log($"Cannot parse remote version '{remote}', assuming not newer");
            return false;
        }

        if (!canParseLocal)
        {
            SimpleLogger.Log($"Cannot parse local version '{local}', assuming remote is newer");
            return true;
        }

        bool isNewer = rv! > lv!;
        SimpleLogger.Log($"Version comparison result: {rv} > {lv} = {isNewer}");

        return isNewer;
    }
}