using MermaidPad.Models;
using System.Diagnostics;
using System.Text.Json;

namespace MermaidPad.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public AppSettings Settings { get; }

    public SettingsService()
    {
        string baseDir = GetConfigDirectory();
        Directory.CreateDirectory(baseDir);
        _settingsPath = Path.Combine(baseDir, "settings.json");
        Settings = Load();
    }

    private static string GetConfigDirectory()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "MermaidPad");
    }

    private AppSettings Load()
    {
        try
        {
            // Validate that the settings path is within the expected config directory
            string configDir = GetConfigDirectory();
            string fullSettingsPath = Path.GetFullPath(_settingsPath);
            string fullConfigDir = Path.GetFullPath(configDir);

            if (!fullSettingsPath.StartsWith(fullConfigDir, StringComparison.OrdinalIgnoreCase))
            {
                Debug.WriteLine("Settings path validation failed.");
                return new AppSettings();
            }

            // Additional validation: ensure the file name is exactly "settings.json"
            if (Path.GetFileName(fullSettingsPath) != "settings.json")
            {
                Debug.WriteLine("Settings file name validation failed on save.");
                return new AppSettings();
            }

            if (File.Exists(fullSettingsPath))
            {
                string json = File.ReadAllText(fullSettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Settings load failed: {ex}");
        }
        return new AppSettings();
    }

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
            if (Path.GetFileName(fullSettingsPath) != "settings.json")
            {
                Debug.WriteLine("Settings file name validation failed on save.");
                return;
            }

            // Serialize and save the settings
            string json = JsonSerializer.Serialize(Settings, _jsonOptions);
            File.WriteAllText(fullSettingsPath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Settings save failed: {ex}");
        }
    }
}
