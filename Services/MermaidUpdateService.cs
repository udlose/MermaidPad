using MermaidPad.Models;
using System.Diagnostics;
using System.Text.Json;

namespace MermaidPad.Services;

public sealed class MermaidUpdateService
{
    private readonly AppSettings _settings;
    private readonly string _assetDir;
    private static readonly HttpClient _http = new();

    public MermaidUpdateService(AppSettings settings, string assetDir)
    {
        _settings = settings;
        _assetDir = assetDir;
    }

    //TODO is this cross-platform?
    public string BundledMermaidPath => Path.Combine(_assetDir, "mermaid.min.js");

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
        // unpkg exposes package.json
        string pkgJson = await _http.GetStringAsync("https://unpkg.com/mermaid/package.json");
        using JsonDocument doc = JsonDocument.Parse(pkgJson);
        string version = doc.RootElement.GetProperty("version").GetString() ?? "0.0.0";
        const string jsUrl = "https://unpkg.com/mermaid/dist/mermaid.min.js";
        //const string cssUrl = "https://unpkg.com/mermaid/dist/mermaid.css";

        //const string jsUrl = "https://cdnjs.cloudflare.com/ajax/libs/mermaid/11.9.0/mermaid.min.js";
        //TODO add CSS? const string cssUrl = "https://cdnjs.cloudflare.com/ajax/libs/mermaid/11.9.0/mermaid.css";
        //Debug.WriteLine($"Latest Mermaid version: {version}, JS URL: {jsUrl}, CSS URL: {cssUrl}");
        Debug.WriteLine($"Latest Mermaid version: {version}, JS URL: {jsUrl}");
        _settings.LatestCheckedMermaidVersion = jsUrl;
        return (version, jsUrl);
    }

    private static bool IsNewer(string remote, string local)
    {
        return Version.TryParse(remote, out Version? rv) && Version.TryParse(local, out Version? lv) && rv > lv;
    }
}
