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

namespace MermaidPad.Models;

/// <summary>
/// Represents persisted application settings for MermaidPad.
/// </summary>
/// <remarks>
/// Instances of this type are intended to be serialized and deserialized to persist
/// user preferences and editor state between application sessions.
/// </remarks>
public sealed class AppSettings
{
    /// <summary>
    /// The raw text of the last edited Mermaid diagram.
    /// </summary>
    /// <value>
    /// May be <see langword="null"/> when no diagram has been saved or the setting has not been initialized.
    /// </value>
    public string? LastDiagramText { get; set; }

    /// <summary>
    /// The version of Mermaid bundled with the application.
    /// </summary>
    /// <remarks>
    /// Defaults to "11.12.0". Update this value when the embedded Mermaid runtime is changed.
    /// </remarks>
    public string BundledMermaidVersion { get; set; } = "11.12.0";

    /// <summary>
    /// The latest Mermaid version that was checked for updates.
    /// </summary>
    /// <value>
    /// May be <see langword="null"/> if an update check has never been performed.
    /// </value>
    public string? LatestCheckedMermaidVersion { get; set; }

    /// <summary>
    /// Indicates whether Mermaid auto-updates are enabled.
    /// </summary>
    public bool AutoUpdateMermaid { get; set; }

    /// <summary>
    /// Indicates whether live preview of diagrams is enabled in the editor.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>true</c>.
    /// </remarks>
    public bool LivePreviewEnabled { get; set; } = true;

    /// <summary>
    /// The zero-based start index of the editor selection.
    /// </summary>
    public int EditorSelectionStart { get; set; }

    /// <summary>
    /// The length (in characters) of the editor selection.
    /// </summary>
    public int EditorSelectionLength { get; set; }

    /// <summary>
    /// The caret offset within the editor document.
    /// </summary>
    public int EditorCaretOffset { get; set; }

    /// <summary>
    /// The file path of the currently open diagram file.
    /// </summary>
    /// <value>
    /// May be <see langword="null"/> when no file is open.
    /// </value>
    public string? CurrentFilePath { get; set; }

    /// <summary>
    /// A list of recently opened diagram file paths.
    /// </summary>
    /// <remarks>
    /// Initialized to an empty list by default.
    /// </remarks>
    public List<string> RecentFiles { get; set; } = [];
}
