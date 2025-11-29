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

using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.Models;

/// <summary>
/// Defines the available application UI themes.
/// Application themes control the colors of the window, toolbar, buttons, status bar, and other UI elements.
/// </summary>
/// <remarks>
/// This enum is separate from editor themes (TextMate syntax highlighting themes).
/// Application themes focus on the overall application appearance, while editor themes
/// control code syntax highlighting colors in the text editor.
/// </remarks>
[SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Enum names represent distinct themes and may not follow standard naming conventions.")]
public enum ApplicationTheme
{
    #region light themes

    /// <summary>
    /// Studio Light theme - inspired by Visual Studio 2022 light mode with purple accents.
    /// </summary>
    StudioLight,

    /// <summary>
    /// Professional Gray theme - inspired by VS Code light with blue accents.
    /// </summary>
    ProfessionalGray,

    /// <summary>
    /// Soft Contrast theme - warm, low-contrast light theme with teal accents.
    /// </summary>
    SoftContrast,

    #endregion light themes

    #region dark themes

    /// <summary>
    /// VS Dark theme - classic VS Code dark theme with blue accents.
    /// </summary>
    VSDark,

    /// <summary>
    /// Midnight Developer theme - blue undertones with orange accents.
    /// </summary>
    MidnightDeveloper,

    /// <summary>
    /// Charcoal Pro theme - warmer dark theme with green accents.
    /// </summary>
    CharcoalPro,

    /// <summary>
    /// VS 2022 Dark theme - replica of Visual Studio 2022 dark mode.
    /// This is the default dark theme when no theme is selected.
    /// </summary>
    VS2022Dark,

    #endregion dark themes

    #region Skeuomorphic light themes

    /// <summary>
    /// Studio Light 3D theme - skeuomorphic version with gradients, shadows, and depth.
    /// </summary>
    StudioLight3D,

    /// <summary>
    /// Professional Gray 3D theme - skeuomorphic version with gradients, shadows, and depth.
    /// </summary>
    ProfessionalGray3D,

    /// <summary>
    /// Soft Contrast 3D theme - skeuomorphic version with gradients, shadows, and depth.
    /// </summary>
    SoftContrast3D,

    #endregion Skeuomorphic light themes

    #region Skeuomorphic dark themes

    /// <summary>
    /// VS Dark 3D theme - skeuomorphic version with gradients, shadows, and depth.
    /// </summary>
    VSDark3D,

    /// <summary>
    /// Midnight Developer 3D theme - skeuomorphic version with gradients, shadows, and depth.
    /// </summary>
    MidnightDeveloper3D,

    /// <summary>
    /// Charcoal Pro 3D theme - skeuomorphic version with gradients, shadows, and depth.
    /// </summary>
    CharcoalPro3D,

    /// <summary>
    /// VS 2022 Dark 3D theme - skeuomorphic version with gradients, shadows, and depth.
    /// </summary>
    VS2022Dark3D

    #endregion Skeuomorphic dark themes
}
