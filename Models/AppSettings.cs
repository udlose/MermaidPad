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
    /// This indicates whether word wrap is enabled or not.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>false</c>.
    /// </remarks>
    public bool IsWordWrapEnabled { get; set; } = false;

    /// <summary>
    /// This indicates whether line numbers are shown in the editor.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>true</c>.
    /// </remarks>
    public bool ShowLineNumbers { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether rectangular selection is enabled in the editor.
    /// </summary>
    /// <remarks>
    /// When enabled, users can select text in a rectangular block rather than by lines. This feature
    /// is commonly used for column editing or selecting text across multiple lines and columns.
    /// Defaults to <c>true</c>.
    /// </remarks>
    public bool EnableRectangularSelection { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether text drag-and-drop operations are enabled.
    /// </summary>
    /// <remarks>
    /// When enabled, users can drag and drop text within the control or between compatible controls.
    /// Disabling this property prevents text from being moved or copied via drag-and-drop interactions.
    /// Defaults to <c>true</c>.
    /// </remarks>
    public bool EnableTextDragDrop { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the cursor is hidden while typing in the editor.
    /// </summary>
    /// <remarks>
    /// When enabled, the cursor will automatically be hidden during text input to reduce visual
    /// distraction. This setting is commonly used in text editors to improve focus while typing.
    /// Defaults to <c>true</c>.
    /// </remarks>
    public bool HideCursorWhileTyping { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the current line is visually highlighted in the editor.
    /// </summary>
    /// <remarks>
    /// When enabled, the current line is highlighted to improve visibility and focus.
    /// Defaults to <c>true</c>.
    /// </remarks>
    public bool HighlightCurrentLine { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether column rulers are displayed in the editor.
    /// </summary>
    /// <remarks>
    /// When enabled, vertical rulers are shown at specified column positions to help with
    /// alignment and code readability. Defaults to <c>false</c>.
    /// </remarks>
    public bool ShowColumnRulers { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether whitespace characters (spaces, tabs) are shown in the editor.
    /// </summary>
    /// <remarks>
    /// When enabled, whitespace characters are visually represented in the editor, which can help
    /// with identifying and fixing formatting issues. Defaults to <c>false</c>.
    /// </remarks>
    public bool ShowSpaces { get; set; }

    /// <summary>
    /// Gets or sets the zero-based index of the first character in the current text selection within the editor.
    /// </summary>
    /// <remarks>If no text is selected, this property typically indicates the position of the caret. Setting
    /// this property updates the selection start position; ensure the value is within the bounds of the editor's text
    /// length.</remarks>
    public int EditorSelectionStart { get; set; }

    /// <summary>
    /// Gets or sets the number of characters currently selected in the editor.
    /// </summary>
    public int EditorSelectionLength { get; set; }

    /// <summary>
    /// Gets or sets the zero-based offset position of the caret within the editor content.
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

    /// <summary>
    /// Logging configuration settings.
    /// </summary>
    public LoggingSettings Logging { get; set; } = new();
}

/// <summary>
/// Configuration settings for application logging.
/// </summary>
public sealed class LoggingSettings
{
    /// <summary>
    /// Enables file-based logging to disk.
    /// </summary>
    public bool EnableFileLogging { get; set; } = true;

    /// <summary>
    /// Enables debug output (Visual Studio output window, console).
    /// </summary>
    public bool EnableDebugOutput { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether diagnostic information for docking operations is enabled.
    /// </summary>
    /// <remarks>When enabled, additional diagnostic data may be collected or displayed to assist with
    /// troubleshooting docking behavior. This setting is typically used during development or debugging
    /// scenarios.</remarks>
    public bool EnableDockDiagnosticsLogging { get; set; }

    /// <summary>
    /// Minimum log level: Debug, Information, Warning, Error, Fatal.
    /// </summary>
    public string MinimumLogLevel { get; set; } = "Warning";

    /// <summary>
    /// File size limit in bytes before rolling (default 2MB).
    /// </summary>
    public long FileSizeLimitBytes { get; set; } = 2_097_152; // 2MB

    /// <summary>
    /// Number of log files to retain (default 5).
    /// </summary>
    public int RetainedFileCountLimit { get; set; } = 5;

    /// <summary>
    /// Custom log file path (null = default %APPDATA%/MermaidPad/debug.log).
    /// </summary>
    public string? CustomLogFilePath { get; set; }
}
