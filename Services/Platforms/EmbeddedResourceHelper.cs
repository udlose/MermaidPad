using System.Diagnostics;
using System.Reflection;

namespace MermaidPad.Services.Platforms;

/// <summary>
/// Provides methods to extract embedded assets to the file system for WebView consumption.
/// Designed for single-file publishing scenarios where Content files are unreliable.
/// IL3000-safe: Does not use Assembly.Location for single-file compatibility.
/// </summary>
public static class EmbeddedResourceHelper
{
    private static readonly Assembly _currentAssembly = Assembly.GetExecutingAssembly();
    private const string IndexHtmlFilename = "index.html";
    private const string MermaidJsFilename = "mermaid.min.js";
    private const string EmbeddedResourcePrefix = "MermaidPad.Assets.";

    /// <summary>
    /// Extracts all required embedded assets to the specified directory.
    /// Always overwrites existing files to ensure current versions.
    /// Optimized for DI-level caching (called once per app session).
    /// </summary>
    /// <param name="targetDirectory">Directory where assets will be extracted</param>
    public static void ExtractEmbeddedAssets(string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);

        Debug.WriteLine($"Extracting embedded assets to: {targetDirectory}");

        // Extract index.html
        ExtractResource($"{EmbeddedResourcePrefix}{IndexHtmlFilename}",
                       Path.Combine(targetDirectory, IndexHtmlFilename));

        // Extract mermaid.min.js
        ExtractResource($"{EmbeddedResourcePrefix}{MermaidJsFilename}",
                       Path.Combine(targetDirectory, MermaidJsFilename));

        // Write version marker for future cache validation
        WriteVersionMarker(targetDirectory);

        Debug.WriteLine("Asset extraction completed");
    }

    /// <summary>
    /// Extracts a single embedded resource to the specified target path.
    /// </summary>
    /// <param name="resourceName">The full name of the embedded resource.</param>
    /// <param name="targetPath">The file system path to write the resource to.</param>
    /// <exception cref="InvalidOperationException">Thrown if the resource cannot be found.</exception>
    private static void ExtractResource(string resourceName, string targetPath)
    {
        try
        {
            using Stream? stream = _currentAssembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                string available = string.Join(", ", _currentAssembly.GetManifestResourceNames());
                throw new InvalidOperationException($"Resource '{resourceName}' not found. Available: {available}");
            }

            using FileStream fileStream = File.Create(targetPath);
            stream.CopyTo(fileStream);

            Debug.WriteLine($"Extracted: {Path.GetFileName(targetPath)} ({stream.Length:N0} bytes)");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to extract {resourceName}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Writes a version marker file to the assets directory for cache validation.
    /// </summary>
    /// <param name="assetsDirectory">The directory where the version marker will be written.</param>
    private static void WriteVersionMarker(string assetsDirectory)
    {
        try
        {
            string versionMarkerPath = Path.Combine(assetsDirectory, ".version");
            Version versionObj = _currentAssembly.GetName().Version ??
                throw new InvalidOperationException("Assembly version could not be determined. This may indicate a build or deployment issue.");

            string version = versionObj.ToString();
            File.WriteAllText(versionMarkerPath, version);
            Debug.WriteLine($"Version marker written: {version}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not write version marker: {ex.Message}");
        }
    }
}
