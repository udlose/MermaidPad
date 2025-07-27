using MermaidPad.Models;
using System.Diagnostics;
using System.Text.Json;

namespace MermaidPad.Services;

public sealed class MermaidUpdateService
{
    private string AssetDir { get; }
    private readonly AppSettings _settings;
    private static readonly HttpClient _http = new HttpClient();

    public MermaidUpdateService(AppSettings settings, string assetDir)
    {
        _settings = settings;
        AssetDir = assetDir;
    }

    public string BundledMermaidPath => Path.Combine(AssetDir, "mermaid.min.js");

    public async Task CheckAndUpdateAsync()
    {
        if (!_settings.AutoUpdateMermaid)
        {
            return;
        }

        try
        {
            (string remoteVersion, string url) = await FetchLatestVersionAsync();
            _settings.LatestCheckedMermaidVersion = remoteVersion;

            if (IsNewer(remoteVersion, _settings.BundledMermaidVersion))
            {
                string tmp = Path.GetTempFileName();
                string jsContent = await _http.GetStringAsync(url);
                await File.WriteAllTextAsync(tmp, jsContent);
                File.Copy(tmp, BundledMermaidPath, overwrite: true);
                _settings.BundledMermaidVersion = remoteVersion;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Mermaid update failed: {ex}");
        }
    }

    private async Task<(string version, string jsUrl)> FetchLatestVersionAsync()
    {
        const string mermaidUrlPrefix = "https://unpkg.com/mermaid";

        // unpkg exposes package.json
        string pkgJson = await _http.GetStringAsync($"{mermaidUrlPrefix}/package.json");
        using JsonDocument doc = JsonDocument.Parse(pkgJson);
        string version = doc.RootElement.GetProperty("version").GetString() ?? "0.0.0";
        const string jsUrl = $"{mermaidUrlPrefix}/dist/mermaid.min.js";
        Debug.WriteLine($"Latest Mermaid version: {version}, JS URL: {jsUrl}");
        _settings.LatestCheckedMermaidVersion = jsUrl;
        return (version, jsUrl);
    }

    private static bool IsNewer(string remote, string local)
    {
        return Version.TryParse(remote, out Version? rv) && Version.TryParse(local, out Version? lv) && rv > lv;
    }
}
