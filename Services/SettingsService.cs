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
    private const string SettingsFileName = "settings.json";

    public AppSettings Settings { get; }

    public SettingsService()
    {
        string baseDir = GetConfigDirectory();
        Directory.CreateDirectory(baseDir);
        _settingsPath = Path.Combine(baseDir, SettingsFileName);
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

            // Additional validation: ensure the file name is exactly "settings.json"
            if (Path.GetFileName(fullSettingsPath) != SettingsFileName)
            {
                Debug.WriteLine("Settings file name validation failed on load.");
                return new AppSettings();
            }

            if (File.Exists(fullSettingsPath))
            {
                // Use SecurityService for comprehensive validation
                (bool isSecure, string? reason) = SecurityService.IsFilePathSecure(fullSettingsPath, configDir, isAssetFile: true);
                if (!isSecure && !string.IsNullOrEmpty(reason))
                {
                    SimpleLogger.LogError($"Settings file validation failed: {reason}");
                    return new AppSettings();
                }

                // Use SecurityService for secure file stream creation
                string json;
                using (FileStream fs = SecurityService.CreateSecureFileStream(fullSettingsPath, FileMode.Open, FileAccess.Read))
                using (StreamReader reader = new StreamReader(fs))
                {
                    json = reader.ReadToEnd();
                }

                return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError($"Settings load failed: {ex}");
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
            using StreamWriter writer = new(fs);
            writer.Write(json);
            writer.Flush();
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError($"Settings save failed: {ex}");
        }
    }
}
