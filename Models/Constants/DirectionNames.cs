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

namespace MermaidPad.Models.Constants;

/// <summary>
/// Provides constant string values representing direction names used in Mermaid diagrams.
/// </summary>
/// <remarks>These constants correspond to the direction codes recognized by the Mermaid diagramming syntax, such
/// as "TB" for top-to-bottom and "LR" for left-to-right. Use these values when specifying diagram flow directions to
/// ensure consistency and avoid hardcoding string literals.</remarks>
[SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Following Mermaid naming convention for clarity.")]
internal static class DirectionNames
{
    internal const string TopToBottom = "TB";
    internal const string TopToBottomTD = "TD";
    internal const string BottomToTop = "BT";
    internal const string LeftToRight = "LR";
    internal const string RightToLeft = "RL";
}
