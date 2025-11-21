
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
/// Represents persisted UI-specific settings for MermaidPad.
/// </summary>
/// <remarks>
/// This class is separate from <see cref="AppSettings"/> to decouple UI state
/// (such as window position, dock layout, etc.) from application settings.
/// Instances of this type are serialized and deserialized to persist
/// UI state between application sessions.
/// </remarks>
public sealed class UISettings
{
    /// <summary>
    /// Gets or sets the serialized dock layout state.
    /// </summary>
    /// <value>
    /// A JSON string representing the dock layout, or null if no layout has been saved.
    /// </value>
    public string? DockLayout { get; set; }

    /// <summary>
    /// Gets or sets the window width.
    /// </summary>
    /// <value>
    /// The window width in pixels, or null to use the default.
    /// </value>
    public double? WindowWidth { get; set; }

    /// <summary>
    /// Gets or sets the window height.
    /// </summary>
    /// <value>
    /// The window height in pixels, or null to use the default.
    /// </value>
    public double? WindowHeight { get; set; }

    /// <summary>
    /// Gets or sets the window X position.
    /// </summary>
    /// <value>
    /// The window X position in pixels, or null to use the default.
    /// </value>
    public double? WindowX { get; set; }

    /// <summary>
    /// Gets or sets the window Y position.
    /// </summary>
    /// <value>
    /// The window Y position in pixels, or null to use the default.
    /// </value>
    public double? WindowY { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the window is maximized.
    /// </summary>
    public bool IsWindowMaximized { get; set; }
}
