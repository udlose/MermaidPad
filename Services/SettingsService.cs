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
                Debug.WriteLine("Settings file name validation failed on load.");
                return new AppSettings();
            }

            if (File.Exists(fullSettingsPath))
            {
                // Extra validation: ensure the file is not a symlink or reparse point
                FileInfo fileInfo = new FileInfo(fullSettingsPath);
                if ((fileInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    Debug.WriteLine("Settings file is a reparse point (symlink/junction), aborting read.");
                    return new AppSettings();
                }

                // SEC0112 fix: Use a whitelist approach to validate the file path before opening
                // Only allow reading if the path is exactly the expected settings.json in the config directory
                string expectedSettingsPath = Path.Combine(fullConfigDir, "settings.json");
                if (string.Equals(fullSettingsPath, expectedSettingsPath, StringComparison.OrdinalIgnoreCase))
                {
                    string json;

                    // Use File.OpenRead which is less error-prone and more restrictive than FileStream constructor
                    using (FileStream fs = File.OpenRead(expectedSettingsPath))
                    using (StreamReader reader = new StreamReader(fs))
                    {
                        json = reader.ReadToEnd();
                    }
                    return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
                }
                else
                {
                    Debug.WriteLine("Settings file path is not the expected config file, aborting read.");
                    return new AppSettings();
                }
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
            Debug.WriteLine($"Saving settings to: {fullSettingsPath}");
            Debug.WriteLine($"Settings JSON: {json}");

            // Use FileStream for better performance on large files
            using FileStream fs = new(fullSettingsPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using StreamWriter writer = new(fs);
            writer.Write(json);
            writer.Flush();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Settings save failed: {ex}");
        }
    }
}
