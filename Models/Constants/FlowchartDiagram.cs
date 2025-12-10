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
/// Provides constants and nested types for configuring and identifying elements within a flowchart diagram.
/// </summary>
/// <remarks>This class is intended for internal use to support flowchart diagram generation and configuration. It
/// contains nested types that define string constants for block openers and configuration options used throughout the
/// flowchart rendering process.</remarks>
internal static class FlowchartDiagram
{
    /// <summary>
    /// Provides constant values for block opener names used in parsing or processing operations.
    /// </summary>
    internal static class BlockOpenerNames
    {
        public const string Subgraph = "subgraph";
    }

    /// <summary>
    /// Provides constant keys for configuration options used throughout the application.
    /// </summary>
    /// <remarks>This class contains string constants representing configuration setting names. These keys are
    /// intended for use when reading or writing configuration values related to diagram rendering and layout. The class
    /// is internal and static, and is not intended to be instantiated.</remarks>
    internal static class Config
    {
        public const string TitleTopMargin = "titleTopMargin:";
        public const string SubGraphTitleMargin = "subGraphTitleMargin:";
        public const string ArrowMarkerAbsolute = "arrowMarkerAbsolute:";
        public const string DiagramPadding = "diagramPadding:";
        public const string HtmlLabels = "htmlLabels:";
        public const string NodeSpacing = "nodeSpacing:";
        public const string RankSpacing = "rankSpacing:";
        public const string Curve = ShapeNames.Curve + ":";
        public const string Padding = "padding:";
        public const string DefaultRenderer = "defaultRenderer:";
        public const string WrappingWidth = "wrappingWidth:";
        public const string InheritDir = "inheritDir:";
    }
}
