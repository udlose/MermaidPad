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
/// Represents application settings that are persisted between runs of the application.
/// </summary>
/// <remarks>
/// Instances of this class store user preferences and editor state (such as the last open diagram,
/// editor selection and caret position, and mermaid version configuration). The settings are intended
/// to be serialized and deserialized when saving and loading application state.
/// </remarks>
public sealed class AppSettings
{
    /// <summary>
    /// Gets or sets the last diagram source text edited or opened by the user.
    /// </summary>
    /// <value>
    /// The raw mermaid diagram text. This value may be <c>null</c> when no diagram has been saved.
    /// </value>
    public string? LastDiagramText { get; set; }

    /// <summary>
    /// Gets or sets the version of the bundled Mermaid library shipped with the application.
    /// </summary>
    /// <value>
    /// A semantic version string representing the bundled mermaid release. Default: "11.12.0".
    /// </value>
    public string BundledMermaidVersion { get; set; } = "11.12.0";

    /// <summary>
    /// Gets or sets the latest Mermaid version string that was observed during the last update check.
    /// </summary>
    /// <value>
    /// A semantic version string of the latest known Mermaid release, or <c>null</c> if an update check
    /// has not been performed yet.
    /// </value>
    public string? LatestCheckedMermaidVersion { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether Mermaid should be automatically updated when a newer version is available.
    /// </summary>
    /// <value>
    /// <c>true</c> to enable automatic updates; otherwise, <c>false</c>.
    /// </value>
    public bool AutoUpdateMermaid { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether live preview of diagrams is enabled in the editor.
    /// </summary>
    /// <value>
    /// <c>true</c> when live preview is enabled; otherwise, <c>false</c>. Default is <c>true</c>.
    /// </value>
    public bool LivePreviewEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the zero-based start index of the editor selection when the application was last closed.
    /// </summary>
    /// <value>
    /// The selection start index (zero-based). A value of 0 indicates the beginning of the document.
    /// </value>
    public int EditorSelectionStart { get; set; }

    /// <summary>
    /// Gets or sets the length of the editor selection when the application was last closed.
    /// </summary>
    /// <value>
    /// The number of characters selected. A value of 0 indicates no selection.
    /// </value>
    public int EditorSelectionLength { get; set; }

    /// <summary>
    /// Gets or sets the caret offset within the editor when the application was last closed.
    /// </summary>
    /// <value>
    /// The zero-based caret offset position in the editor document.</value>
    public int EditorCaretOffset { get; set; }

    /// <summary>
    /// Gets or sets the saved zoom level for the diagram preview.
    /// Default is 1.0 (100%). Range: 0.1 (10%) to 5.0 (500%).
    /// </summary>
    public double ZoomLevel { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the saved horizontal pan offset for the diagram preview.
    /// </summary>
    public double PanOffsetX { get; set; }

    /// <summary>
    /// Gets or sets the saved vertical pan offset for the diagram preview.
    /// </summary>
    public double PanOffsetY { get; set; }
}
