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

using System.Reflection;

namespace MermaidPad;

/// <summary>
/// Provides convenient accessors for common assembly-level metadata for the running application.
/// Values are read lazily from the entry or executing assembly and fall back to sensible defaults
/// when attributes are not present.
/// </summary>
public static class AppMetadata
{
    /// <summary>
    /// Lazily caches the assembly used to read attributes.
    /// Uses <see cref="Assembly.GetEntryAssembly"/> and falls back to <see cref="Assembly.GetExecutingAssembly"/> when needed.
    /// </summary>
    private static readonly Lazy<Assembly>
        _asm = new Lazy<Assembly>(static () => Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly());

    /// <summary>
    /// Gets the recorded build date from assembly metadata using the "BuildDate" key.
    /// Returns <c>null</c> if the metadata key is not present.
    /// </summary>
    public static string? BuildDate => GetMetadata("BuildDate");

    /// <summary>
    /// Gets the comments or description associated with the assembly, as specified by the <see
    /// cref="AssemblyDescriptionAttribute"/>.
    /// </summary>
    /// <remarks>This property returns the value typically displayed as "Comments" in Windows Explorer for the
    /// assembly file. If the assembly does not define an <see cref="AssemblyDescriptionAttribute"/>, the property
    /// returns <see langword="null"/>.</remarks>
    public static string? Comments => _asm.Value.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description;

    /// <summary>
    /// Gets the commit SHA recorded in assembly metadata. Common keys are "CommitSha" and "SourceRevisionId".
    /// Returns <c>null</c> if no matching metadata key is present.
    /// </summary>
    public static string? CommitSha => GetMetadata("CommitSha") ?? GetMetadata("SourceRevisionId");

    /// <summary>
    /// Gets the file version of the currently executing assembly.
    /// </summary>
    /// <remarks>If the assembly does not define an explicit file version, this property returns the
    /// assembly's version number formatted as 'major.minor.build'. If neither is available, it returns
    /// "Unknown".</remarks>
    public static string FileVersion => _asm.Value.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
        ?? _asm.Value.GetName().Version?.ToString(3)
        ?? "Unknown";

    /// <summary>
    /// Gets the informational version from the assembly's <see cref="AssemblyInformationalVersionAttribute"/>.
    /// This typically includes Version+CommitSHA or other descriptive version info: e.g. "1.2.3+abcd1234".
    /// If that attribute is not present, falls back to the assembly version (major.minor.build). If neither is present, returns "Unknown".
    /// </summary>
    public static string InformationalVersion =>
        _asm.Value.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? _asm.Value.GetName().Version?.ToString(3)
            ?? "Unknown";

    /// <summary>
    /// Gets the product name from the assembly's <see cref="AssemblyProductAttribute"/>.
    /// If the attribute is not present, the assembly name is returned. If that is not available, returns "Unknown".
    /// </summary>
    public static string Product =>
        _asm.Value.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? _asm.Value.GetName().Name ?? "Unknown";

    /// <summary>
    /// Gets the descriptive tagline for the product, used for display in user interfaces and metadata.
    /// </summary>
    public static string Tagline =>
        Comments ?? Title ?? $"{Product} — a live Mermaid diagram editor with preview and export.";

    /// <summary>
    /// Gets the title of the assembly, as specified by the <see cref="AssemblyTitleAttribute"/>.
    /// </summary>
    public static string? Title => _asm.Value.GetCustomAttribute<AssemblyTitleAttribute>()?.Title;

    /// <summary>
    /// Retrieves an assembly metadata value for the specified <paramref name="key"/>, performing a case-insensitive comparison.
    /// </summary>
    /// <param name="key">The metadata key to look up (case-insensitive).</param>
    /// <returns>The corresponding metadata value, or <c>null</c> if no entry matches the key.</returns>
    private static string? GetMetadata(string key) =>
        _asm.Value.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase))
            ?.Value;
}
