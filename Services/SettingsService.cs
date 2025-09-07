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

            // Use SecurityService for all validation
            if (!SecurityService.IsPathWithinDirectory(fullSettingsPath, configDir))
            {
                Debug.WriteLine("Settings path validation failed.");
                return new AppSettings();
            }

            // Additional validation: ensure the file name is exactly "settings.json"
            if (Path.GetFileName(fullSettingsPath) != SettingsFileName)
            {
                Debug.WriteLine("Settings file name validation failed on load.");
                return new AppSettings();
            }

            if (File.Exists(fullSettingsPath))
            {
                // Use SecurityService for comprehensive validation
                if (!SecurityService.IsFilePathSecure(fullSettingsPath, configDir))
                {
                    Debug.WriteLine("Settings file failed security validation.");
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
    //private AppSettings Load()
    //{
    //    try
    //    {
    //        // Validate that the settings path is within the expected config directory
    //        string configDir = GetConfigDirectory();
    //        string fullSettingsPath = Path.GetFullPath(_settingsPath);
    //        string fullConfigDir = Path.GetFullPath(configDir);

    //        if (!fullSettingsPath.StartsWith(fullConfigDir, StringComparison.OrdinalIgnoreCase))
    //        {
    //            Debug.WriteLine("Settings path validation failed.");
    //            return new AppSettings();
    //        }

    //        // Additional validation: ensure the file name is exactly "settings.json"
    //        if (Path.GetFileName(fullSettingsPath) != SettingsFileName)
    //        {
    //            Debug.WriteLine("Settings file name validation failed on load.");
    //            return new AppSettings();
    //        }

    //        if (File.Exists(fullSettingsPath))
    //        {
    //            // Extra validation: ensure the file is not a symlink or reparse point
    //            FileInfo fileInfo = new FileInfo(fullSettingsPath);
    //            if ((fileInfo.Attributes & FileAttributes.ReparsePoint) != 0)
    //            {
    //                Debug.WriteLine("Settings file is a reparse point (symlink/junction), aborting read.");
    //                return new AppSettings();
    //            }

    //            // SEC0112 fix: Use a whitelist approach to validate the file path before opening
    //            // Only allow reading if the path is exactly the expected settings.json in the config directory
    //            string expectedSettingsPath = Path.Combine(fullConfigDir, SettingsFileName);
    //            if (string.Equals(fullSettingsPath, expectedSettingsPath, StringComparison.OrdinalIgnoreCase))
    //            {
    //                // Extra validation: ensure the file is not a symlink or reparse point (already done above)
    //                // Additional validation: ensure the file is not a hard link
    //                if (IsSingleLink(expectedSettingsPath))
    //                {
    //                    // Use File.OpenRead which is less error-prone and more restrictive than FileStream constructor
    //                    string json;
    //                    using (FileStream fs = File.OpenRead(expectedSettingsPath))
    //                    using (StreamReader reader = new StreamReader(fs))
    //                    {
    //                        json = reader.ReadToEnd();
    //                    }
    //                    return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
    //                }

    //                SimpleLogger.LogError("Settings file is a hard link, aborting read.");
    //                return new AppSettings();
    //            }

    //            SimpleLogger.LogError("Settings file path is not the expected config file, aborting read.");
    //            return new AppSettings();
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        SimpleLogger.LogError($"Settings load failed: {ex}");
    //    }
    //    return new AppSettings();
    //}

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

    private static bool IsSingleLink(string filePath)
    {
        // Add validation before using the path
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        // Ensure it's an absolute path without traversal
        if (!Path.IsPathRooted(filePath) || filePath.Contains(".."))
        {
            return false;
        }

        // Validate it's within expected directory
        string configDir = GetConfigDirectory();
        string fullPath = Path.GetFullPath(filePath);
        string fullConfigDir = Path.GetFullPath(configDir);

        if (!fullPath.StartsWith(fullConfigDir, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // On Windows, check if the file has only one hard link
        // This is a simple check to see if the file is not a symlink or reparse point
        if (OperatingSystem.IsWindows())
        {
            try
            {
                //SEC0112 fix: Use validated path
                FileInfo fileInfo = new FileInfo(fullPath);
                return fileInfo is { Exists: true, LinkTarget: null } && !fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint);
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError($"Error checking file links: {ex}");
                return false;
            }
        }

        // On non-Windows systems, we assume the file is not a symlink or reparse point
        // This is a simplification, as non-Windows systems may not have the same link semantics
        // Note: This may not be fully accurate for all non-Windows systems
        // but is a reasonable assumption for most use cases.
        return true;
    }
}
