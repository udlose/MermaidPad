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

namespace MermaidPad.Models.Constants;

/// <summary>
/// Defines the comment token constants used for commenting and uncommenting lines in Mermaid diagrams
/// and YAML frontmatter sections.
/// </summary>
/// <remarks>
/// <para>
/// Mermaid diagrams use <c>%%</c> as the line comment token, while YAML frontmatter uses <c>#</c>.
/// These tokens are used by the commenting strategy to add or remove comments from selected lines
/// based on the document context (frontmatter vs. diagram).
/// </para>
/// <para>
/// Comment tokens are applied one layer at a time:
/// <list type="bullet">
/// <item><description>Commenting an already-commented line adds another comment layer</description></item>
/// <item><description>Uncommenting removes only one comment layer at a time</description></item>
/// </list>
/// </para>
/// </remarks>
internal static class CommentTokens
{
    /// <summary>
    /// The line comment token for Mermaid diagram content.
    /// </summary>
    /// <remarks>
    /// This is a two-character token (<c>%%</c>) that marks a line as a comment in Mermaid syntax.
    /// Must be two characters to be considered a valid comment; a single <c>%</c> is not a comment.
    /// </remarks>
    internal const string Diagram = "%%";

    /// <summary>
    /// The line comment token for YAML frontmatter content.
    /// </summary>
    /// <remarks>
    /// This is a single-character token (<c>#</c>) that marks a line as a comment in YAML syntax.
    /// Used in the frontmatter section between the <c>---</c> delimiters.
    /// </remarks>
    internal const string Frontmatter = "#";
}
