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

namespace MermaidPad.Models.Editor;

/// <summary>
/// Specifies the context of a line within a document, such as frontmatter
/// delimiters, frontmatter content, or diagram content.
/// </summary>
/// <remarks>Use this enumeration to identify the structural role of a line when
/// parsing documents that may contain YAML frontmatter or embedded Mermaid diagrams.</remarks>
internal enum DocumentContext
{
    /// <summary>
    /// The line is the opening frontmatter delimiter (first ---).
    /// </summary>
    FrontmatterStart,

    /// <summary>
    /// The line is within the YAML frontmatter section.
    /// </summary>
    Frontmatter,

    /// <summary>
    /// The line is the closing frontmatter delimiter (second ---).
    /// </summary>
    FrontmatterEnd,

    /// <summary>
    /// The line is within the Mermaid diagram content.
    /// </summary>
    Diagram
}
