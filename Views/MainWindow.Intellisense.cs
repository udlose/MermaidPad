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

using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using MermaidPad.Models.Editor;
using MermaidPad.ObjectPoolPolicies;
using MermaidPad.Services.Editor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace MermaidPad.Views;

/// <summary>
/// Represents the main application window that provides the editor interface and manages IntelliSense features for
/// authoring Mermaid diagrams.
/// </summary>
/// <remarks>The MainWindow class is responsible for initializing and coordinating code completion (IntelliSense)
/// within the editor, including handling user input events, displaying completion suggestions, and managing related
/// resources. It integrates with AvaloniaEdit to provide a responsive editing experience tailored for Mermaid diagram
/// syntax. This class is typically instantiated as the primary window of the application and should be used as the
/// entry point for editor-related functionality.</remarks>
[SuppressMessage("Style", "IDE0078:Use pattern matching", Justification = "Performance and code clarity")]
[SuppressMessage("ReSharper", "MergeIntoPattern", Justification = "Performance and code clarity")]
public partial class MainWindow
{
    private const int LookupTableSize = 128;
    private static readonly DrawingImage? _abcIcon = CreateAbcIcon();
    private CompletionWindow? _completionWindow;
    private static readonly ObjectPool<HashSet<string>> _nodeBufferPool =
        new DefaultObjectPool<HashSet<string>>(new HashSetPooledObjectPolicy());

    // Persists known strings to avoid re-allocating "MyNode" repeatedly
    private readonly HashSet<string> _stringInternPool = new HashSet<string>(StringComparer.Ordinal);

    private readonly Dictionary<string, IntellisenseCompletionData> _wrapperCache =
        new Dictionary<string, IntellisenseCompletionData>(StringComparer.Ordinal);

    /// <summary>
    /// Provides a static array of completion data for recognized keywords used in various Mermaid diagram types.
    /// </summary>
    /// <remarks>This array includes keywords relevant to multiple Mermaid diagram syntaxes, such as
    /// architecture, flowchart, sequence, class, state, ER, Gantt, pie, gitGraph, journey, mindmap, requirement, and C4
    /// diagrams. The array may contain duplicate entries for keywords that are valid in more than one diagram type. The
    /// data is intended for use in IntelliSense or code completion scenarios to assist users authoring Mermaid
    /// diagrams.</remarks>
    [SuppressMessage("Style", "IDE0305:Simplify collection initialization", Justification = "Explicit is clearer here.")]
    private static readonly IntellisenseCompletionData[] _staticKeywords =
        new List<IntellisenseCompletionData>()
        {
            #region Frontmatter keywords

            new IntellisenseCompletionData("%%", 1, _abcIcon),
            new IntellisenseCompletionData("mermaid", 1, _abcIcon),
            new IntellisenseCompletionData("defaultRenderer", 1, _abcIcon),
            new IntellisenseCompletionData("title:", 1, _abcIcon),
            new IntellisenseCompletionData("config:", 1, _abcIcon),
            new IntellisenseCompletionData("layout:", 1, _abcIcon),
            new IntellisenseCompletionData("dagre", 1, _abcIcon),
            new IntellisenseCompletionData("elk", 1, _abcIcon),
            new IntellisenseCompletionData("theme:", 1, _abcIcon),
            new IntellisenseCompletionData("themeVariables:", 1, _abcIcon),
            new IntellisenseCompletionData("logLevel:", 1, _abcIcon),
            new IntellisenseCompletionData("securityLevel:", 1, _abcIcon),
            new IntellisenseCompletionData("startOnLoad:", 1, _abcIcon),
            new IntellisenseCompletionData("secure:", 1, _abcIcon),
            new IntellisenseCompletionData("primaryColor:", 1, _abcIcon),
            new IntellisenseCompletionData("signalColor:", 1, _abcIcon),
            new IntellisenseCompletionData("signalTextColor:", 1, _abcIcon),
            new IntellisenseCompletionData("true", 1, _abcIcon),
            new IntellisenseCompletionData("false", 1, _abcIcon),
            new IntellisenseCompletionData("base", 1, _abcIcon),
            new IntellisenseCompletionData("forest", 1, _abcIcon),
            new IntellisenseCompletionData("default", 1, _abcIcon),
            new IntellisenseCompletionData("dark", 1, _abcIcon),
            new IntellisenseCompletionData("neutral", 1, _abcIcon),
            new IntellisenseCompletionData("useWidth:", 1, _abcIcon),
            new IntellisenseCompletionData("useMaxWidth:", 1, _abcIcon),

            #endregion Frontmatter keywords

            #region Theming General

            // Theming Config: https://github.com/mermaid-js/mermaid/blob/develop/docs/config/theming.md?plain=1
            new IntellisenseCompletionData("darkMode:", 1, _abcIcon),
            new IntellisenseCompletionData("background:", 1, _abcIcon),
            new IntellisenseCompletionData("fontFamily:", 1, _abcIcon),
            new IntellisenseCompletionData("fontSize:", 1, _abcIcon),
            new IntellisenseCompletionData("primaryColor:", 1, _abcIcon),
            new IntellisenseCompletionData("primaryTextColor:", 1, _abcIcon),
            new IntellisenseCompletionData("secondaryColor:", 1, _abcIcon),
            new IntellisenseCompletionData("primaryBorderColor:", 1, _abcIcon),
            new IntellisenseCompletionData("secondaryBorderColor:", 1, _abcIcon),
            new IntellisenseCompletionData("secondaryTextColor:", 1, _abcIcon),
            new IntellisenseCompletionData("tertiaryColor:", 1, _abcIcon),
            new IntellisenseCompletionData("tertiaryBorderColor:", 1, _abcIcon),
            new IntellisenseCompletionData("tertiaryTextColor:", 1, _abcIcon),
            new IntellisenseCompletionData("noteBkgColor:", 1, _abcIcon),
            new IntellisenseCompletionData("noteTextColor:", 1, _abcIcon),
            new IntellisenseCompletionData("noteBorderColor:", 1, _abcIcon),
            new IntellisenseCompletionData("lineColor:", 1, _abcIcon),
            new IntellisenseCompletionData("textColor:", 1, _abcIcon),
            new IntellisenseCompletionData("mainBkg:", 1, _abcIcon),
            new IntellisenseCompletionData("errorBkgColor:", 1, _abcIcon),
            new IntellisenseCompletionData("errorTextColor:", 1, _abcIcon),

            #endregion Theming General

            #region CSS Styles

            // CSS Styles
            new IntellisenseCompletionData("backgroundColor:", 1, _abcIcon),
            new IntellisenseCompletionData("borderColor:", 1, _abcIcon),
            new IntellisenseCompletionData("borderWidth:", 1, _abcIcon),
            new IntellisenseCompletionData("color:", 1, _abcIcon),
            new IntellisenseCompletionData("fill:", 1, _abcIcon),
            new IntellisenseCompletionData("fontFamily:", 1, _abcIcon),
            new IntellisenseCompletionData("fontSize:", 1, _abcIcon),
            new IntellisenseCompletionData("fontWeight:", 1, _abcIcon),
            new IntellisenseCompletionData("stroke:", 1, _abcIcon),
            new IntellisenseCompletionData("stroke-dasharray:", 1, _abcIcon),
            new IntellisenseCompletionData("stroke-dashoffset:", 1, _abcIcon),
            new IntellisenseCompletionData("stroke-width:", 1, _abcIcon),
            new IntellisenseCompletionData("style", 1, _abcIcon),
            new IntellisenseCompletionData("textColor:", 1, _abcIcon),
            new IntellisenseCompletionData("cssStyle", 1, _abcIcon),
            new IntellisenseCompletionData("default", 1, _abcIcon),

            #endregion CSS Styles

            #region Architecture Diagram

            // Architecture Diagram: https://mermaid.js.org/syntax/architecture.html
            new IntellisenseCompletionData("architecture-beta", 1, _abcIcon),
            new IntellisenseCompletionData("cloud", 1, _abcIcon),
            new IntellisenseCompletionData("database", 1, _abcIcon),
            new IntellisenseCompletionData("disk", 1, _abcIcon),
            new IntellisenseCompletionData("edge", 1, _abcIcon),
            new IntellisenseCompletionData("group", 1, _abcIcon),
            new IntellisenseCompletionData("internet", 1, _abcIcon),
            new IntellisenseCompletionData("junction", 1, _abcIcon),
            new IntellisenseCompletionData("server", 1, _abcIcon),
            new IntellisenseCompletionData("service", 1, _abcIcon),
            new IntellisenseCompletionData("L", 1, _abcIcon),
            new IntellisenseCompletionData("R", 1, _abcIcon),
            new IntellisenseCompletionData("T", 1, _abcIcon),
            new IntellisenseCompletionData("B", 1, _abcIcon),

            #endregion Architecture Diagram

            #region Block Diagram

            // Block Diagram: https://mermaid.js.org/syntax/block.html
            new IntellisenseCompletionData("block", 1, _abcIcon),
            new IntellisenseCompletionData("columns", 1, _abcIcon),
            new IntellisenseCompletionData("end", 1, _abcIcon),
            new IntellisenseCompletionData("space", 1, _abcIcon),

            #endregion Block Diagram

            #region C4 Diagram Elements

            //TODO: this type is still experimental and may change. Revisit at some point to adjust as needed.
            // C4: https://mermaid.js.org/syntax/c4.html
            new IntellisenseCompletionData("C4Component", 1, _abcIcon),
            new IntellisenseCompletionData("C4Container", 1, _abcIcon),
            new IntellisenseCompletionData("C4Context", 1, _abcIcon),
            new IntellisenseCompletionData("C4Deployment", 1, _abcIcon),
            new IntellisenseCompletionData("C4Dynamic", 1, _abcIcon),

            // C4 Layout Elements
            new IntellisenseCompletionData("Lay_U", 1, _abcIcon),
            new IntellisenseCompletionData("Lay_Up", 1, _abcIcon),
            new IntellisenseCompletionData("Lay_D", 1, _abcIcon),
            new IntellisenseCompletionData("Lay_Down", 1, _abcIcon),
            new IntellisenseCompletionData("Lay_L", 1, _abcIcon),
            new IntellisenseCompletionData("Lay_Left", 1, _abcIcon),
            new IntellisenseCompletionData("Lay_R", 1, _abcIcon),
            new IntellisenseCompletionData("Lay_Right", 1, _abcIcon),

            // C4 Experimental Elements
            new IntellisenseCompletionData("sprite", 1, _abcIcon),
            new IntellisenseCompletionData("tags", 1, _abcIcon),
            new IntellisenseCompletionData("link", 1, _abcIcon),
            new IntellisenseCompletionData("Legend", 1, _abcIcon),

            // C4 System Context Diagram Elements
            new IntellisenseCompletionData("Person", 1, _abcIcon),
            new IntellisenseCompletionData("Person_Ext", 1, _abcIcon),
            new IntellisenseCompletionData("System", 1, _abcIcon),
            new IntellisenseCompletionData("SystemDb", 1, _abcIcon),
            new IntellisenseCompletionData("SystemQueue", 1, _abcIcon),
            new IntellisenseCompletionData("System_Ext", 1, _abcIcon),
            new IntellisenseCompletionData("SystemDb_Ext", 1, _abcIcon),
            new IntellisenseCompletionData("SystemQueue_Ext", 1, _abcIcon),
            new IntellisenseCompletionData("Boundary", 1, _abcIcon),
            new IntellisenseCompletionData("Enterprise_Boundary", 1, _abcIcon),
            new IntellisenseCompletionData("System_Boundary", 1, _abcIcon),

            // C4 Container Diagram Elements
            new IntellisenseCompletionData("Container", 1, _abcIcon),
            new IntellisenseCompletionData("ContainerDb", 1, _abcIcon),
            new IntellisenseCompletionData("ContainerQueue", 1, _abcIcon),
            new IntellisenseCompletionData("Container_Ext", 1, _abcIcon),
            new IntellisenseCompletionData("ContainerDb_Ext", 1, _abcIcon),
            new IntellisenseCompletionData("ContainerQueue_Ext", 1, _abcIcon),
            new IntellisenseCompletionData("Container_Boundary", 1, _abcIcon),

            // C4 Component Diagram Elements
            new IntellisenseCompletionData("Component", 1, _abcIcon),
            new IntellisenseCompletionData("ComponentDb", 1, _abcIcon),
            new IntellisenseCompletionData("ComponentQueue", 1, _abcIcon),
            new IntellisenseCompletionData("Component_Ext", 1, _abcIcon),
            new IntellisenseCompletionData("ComponentDb_Ext", 1, _abcIcon),
            new IntellisenseCompletionData("ComponentQueue_Ext", 1, _abcIcon),

            // C4 Dynamic Diagram Elements
            new IntellisenseCompletionData("RelIndex", 1, _abcIcon),

            // C4 Deployment Diagram Elements
            new IntellisenseCompletionData("Deployment_Node", 1, _abcIcon),
            new IntellisenseCompletionData("Node", 1, _abcIcon),
            new IntellisenseCompletionData("Node_L", 1, _abcIcon),
            new IntellisenseCompletionData("Node_R", 1, _abcIcon),

            // C4 Relationship Types
            new IntellisenseCompletionData("Rel", 1, _abcIcon),
            new IntellisenseCompletionData("BiRel", 1, _abcIcon),
            new IntellisenseCompletionData("Rel_U", 1, _abcIcon),
            new IntellisenseCompletionData("Rel_Up", 1, _abcIcon),
            new IntellisenseCompletionData("Rel_D", 1, _abcIcon),
            new IntellisenseCompletionData("Rel_Down", 1, _abcIcon),
            new IntellisenseCompletionData("Rel_L", 1, _abcIcon),
            new IntellisenseCompletionData("Rel_Left", 1, _abcIcon),
            new IntellisenseCompletionData("Rel_R", 1, _abcIcon),
            new IntellisenseCompletionData("Rel_Right", 1, _abcIcon),
            new IntellisenseCompletionData("Rel_Back", 1, _abcIcon),
            new IntellisenseCompletionData("Rel_Index", 1, _abcIcon),

            // C4 Custom Tags
            new IntellisenseCompletionData("AddElementTag", 1, _abcIcon),
            new IntellisenseCompletionData("AddRelTag", 1, _abcIcon),
            new IntellisenseCompletionData("UpdateElementStyle", 1, _abcIcon),
            new IntellisenseCompletionData("UpdateRelStyle", 1, _abcIcon),
            new IntellisenseCompletionData("RoundedBoxShape", 1, _abcIcon),
            new IntellisenseCompletionData("EightSidedShape", 1, _abcIcon),
            new IntellisenseCompletionData("DashedLine", 1, _abcIcon),
            new IntellisenseCompletionData("DottedLine", 1, _abcIcon),
            new IntellisenseCompletionData("BoldLine", 1, _abcIcon),
            new IntellisenseCompletionData("UpdateLayoutConfig", 1, _abcIcon),

            #endregion C4 Diagram Elements

            #region Class Diagram

            // Class Diagram: https://mermaid.js.org/syntax/classDiagram.html
            new IntellisenseCompletionData("classDiagram", 1, _abcIcon),
            new IntellisenseCompletionData("classDiagram-v2", 1, _abcIcon),
            new IntellisenseCompletionData("class", 1, _abcIcon),
            new IntellisenseCompletionData("classDef", 1, _abcIcon),
            new IntellisenseCompletionData("interface", 1, _abcIcon),
            new IntellisenseCompletionData("namespace", 1, _abcIcon),
            new IntellisenseCompletionData("bool", 1, _abcIcon),
            new IntellisenseCompletionData("double", 1, _abcIcon),
            new IntellisenseCompletionData("float", 1, _abcIcon),
            new IntellisenseCompletionData("int", 1, _abcIcon),
            new IntellisenseCompletionData("long", 1, _abcIcon),
            new IntellisenseCompletionData("string", 1, _abcIcon),
            new IntellisenseCompletionData("<<interface>>", 1, _abcIcon),
            new IntellisenseCompletionData("<<abstract>>", 1, _abcIcon),
            new IntellisenseCompletionData("<<service>>", 1, _abcIcon),
            new IntellisenseCompletionData("<<enumeration>>", 1, _abcIcon),
            new IntellisenseCompletionData("link", 1, _abcIcon),
            new IntellisenseCompletionData("call", 1, _abcIcon),
            new IntellisenseCompletionData("callback", 1, _abcIcon),
            new IntellisenseCompletionData("direction", 1, _abcIcon),
            new IntellisenseCompletionData("note", 1, _abcIcon),
            new IntellisenseCompletionData("href", 1, _abcIcon),

            // Class Diagram Theming: https://github.com/mermaid-js/mermaid/blob/develop/docs/config/theming.md#class-colors
            new IntellisenseCompletionData("classText:", 1, _abcIcon),

            #endregion Class Diagram

            #region ER Diagram

            // ER Diagram: https://mermaid.js.org/syntax/entityRelationshipDiagram.html
            new IntellisenseCompletionData("erDiagram", 1, _abcIcon),
            new IntellisenseCompletionData("allows", 1, _abcIcon),
            new IntellisenseCompletionData("has", 1, _abcIcon),
            new IntellisenseCompletionData("TB", 1, _abcIcon),
            new IntellisenseCompletionData("BT", 1, _abcIcon),
            new IntellisenseCompletionData("RL", 1, _abcIcon),
            new IntellisenseCompletionData("LR", 1, _abcIcon),

            #endregion ER Diagram

            #region Flowchart

            // Graph / Flowchart: https://mermaid.js.org/syntax/flowchart.html
            new IntellisenseCompletionData("graph", 1, _abcIcon),
            new IntellisenseCompletionData("flowchart", 1, _abcIcon),
            new IntellisenseCompletionData("flowchart-elk", 1, _abcIcon),
            new IntellisenseCompletionData("subgraph", 1, _abcIcon),
            new IntellisenseCompletionData("end", 1, _abcIcon),
            new IntellisenseCompletionData("style", 1, _abcIcon),
            new IntellisenseCompletionData("class", 1, _abcIcon),
            new IntellisenseCompletionData("classDef", 1, _abcIcon),
            new IntellisenseCompletionData("click", 1, _abcIcon),
            new IntellisenseCompletionData("linkStyle", 1, _abcIcon),
            new IntellisenseCompletionData("end", 1, _abcIcon),
            new IntellisenseCompletionData("TB", 1, _abcIcon),
            new IntellisenseCompletionData("TD", 1, _abcIcon),
            new IntellisenseCompletionData("BT", 1, _abcIcon),
            new IntellisenseCompletionData("RL", 1, _abcIcon),
            new IntellisenseCompletionData("LR", 1, _abcIcon),
            new IntellisenseCompletionData("img:", 1, _abcIcon),
            new IntellisenseCompletionData("label:", 1, _abcIcon),
            new IntellisenseCompletionData("pos:", 1, _abcIcon),
            new IntellisenseCompletionData("constraint:", 1, _abcIcon),
            new IntellisenseCompletionData("animate:", 1, _abcIcon),
            new IntellisenseCompletionData("animation:", 1, _abcIcon),
            new IntellisenseCompletionData("markdownAutoWrap", 1, _abcIcon),
            new IntellisenseCompletionData("_blank", 1, _abcIcon),
            new IntellisenseCompletionData("icon:", 1, _abcIcon),
            new IntellisenseCompletionData("form:", 1, _abcIcon),
            new IntellisenseCompletionData("square", 1, _abcIcon),
            new IntellisenseCompletionData("circle", 1, _abcIcon),
            new IntellisenseCompletionData("rounded", 1, _abcIcon),

            // Arrows: https://mermaid.js.org/syntax/flowchart.html#new-arrow-types
            new IntellisenseCompletionData("--o", 1, _abcIcon),
            new IntellisenseCompletionData("-->", 1, _abcIcon),
            new IntellisenseCompletionData("--x", 1, _abcIcon),
            new IntellisenseCompletionData("---", 1, _abcIcon),
            new IntellisenseCompletionData("===", 1, _abcIcon),
            new IntellisenseCompletionData("==>", 1, _abcIcon),
            new IntellisenseCompletionData("-.-", 1, _abcIcon),
            new IntellisenseCompletionData("-.->", 1, _abcIcon),

            // Multi-directional arrows: https://mermaid.js.org/syntax/flowchart.html#multi-directional-arrows
            new IntellisenseCompletionData("o--o", 1, _abcIcon),
            new IntellisenseCompletionData("<-->", 1, _abcIcon),
            new IntellisenseCompletionData("x--x", 1, _abcIcon),

            // Stying line curves: https://mermaid.js.org/syntax/flowchart.html#styling-line-curves
            new IntellisenseCompletionData("curve", 1, _abcIcon),
            new IntellisenseCompletionData("basis", 1, _abcIcon),
            new IntellisenseCompletionData("bumpX", 1, _abcIcon),
            new IntellisenseCompletionData("bumpY", 1, _abcIcon),
            new IntellisenseCompletionData("cardinal", 1, _abcIcon),
            new IntellisenseCompletionData("catmullRom", 1, _abcIcon),
            new IntellisenseCompletionData("linear", 1, _abcIcon),
            new IntellisenseCompletionData("monotoneX", 1, _abcIcon),
            new IntellisenseCompletionData("monotoneY", 1, _abcIcon),
            new IntellisenseCompletionData("natural", 1, _abcIcon),
            new IntellisenseCompletionData("step", 1, _abcIcon),
            new IntellisenseCompletionData("stepAfter", 1, _abcIcon),
            new IntellisenseCompletionData("stepBefore", 1, _abcIcon),

            // Shapes: https://mermaid.js.org/syntax/flowchart.html#complete-list-of-new-shapes
            new IntellisenseCompletionData("bang", 1, _abcIcon),
            new IntellisenseCompletionData("bolt", 1, _abcIcon),
            new IntellisenseCompletionData("bow-rect", 1, _abcIcon),
            new IntellisenseCompletionData("bow-tie-rectangle", 1, _abcIcon),
            new IntellisenseCompletionData("brace", 1, _abcIcon),
            new IntellisenseCompletionData("brace-l", 1, _abcIcon),
            new IntellisenseCompletionData("brace-r", 1, _abcIcon),
            new IntellisenseCompletionData("braces", 1, _abcIcon),
            new IntellisenseCompletionData("card", 1, _abcIcon),
            new IntellisenseCompletionData("circ", 1, _abcIcon),
            new IntellisenseCompletionData("circle", 1, _abcIcon),
            new IntellisenseCompletionData("cloud", 1, _abcIcon),
            new IntellisenseCompletionData("collate", 1, _abcIcon),
            new IntellisenseCompletionData("com-link", 1, _abcIcon),
            new IntellisenseCompletionData("comment", 1, _abcIcon),
            new IntellisenseCompletionData("cross-circ", 1, _abcIcon),
            new IntellisenseCompletionData("crossed-circle", 1, _abcIcon),
            new IntellisenseCompletionData("curv-trap", 1, _abcIcon),
            new IntellisenseCompletionData("curved-trapezoid", 1, _abcIcon),
            new IntellisenseCompletionData("cyl", 1, _abcIcon),
            new IntellisenseCompletionData("cylinder", 1, _abcIcon),
            new IntellisenseCompletionData("das", 1, _abcIcon),
            new IntellisenseCompletionData("database", 1, _abcIcon),
            new IntellisenseCompletionData("db", 1, _abcIcon),
            new IntellisenseCompletionData("dbl-circ", 1, _abcIcon),
            new IntellisenseCompletionData("decision", 1, _abcIcon),
            new IntellisenseCompletionData("delay", 1, _abcIcon),
            new IntellisenseCompletionData("diam", 1, _abcIcon),
            new IntellisenseCompletionData("diamond", 1, _abcIcon),
            new IntellisenseCompletionData("disk", 1, _abcIcon),
            new IntellisenseCompletionData("display", 1, _abcIcon),
            new IntellisenseCompletionData("div-proc", 1, _abcIcon),
            new IntellisenseCompletionData("div-rect", 1, _abcIcon),
            new IntellisenseCompletionData("divided-process", 1, _abcIcon),
            new IntellisenseCompletionData("divided-rectangle", 1, _abcIcon),
            new IntellisenseCompletionData("doc", 1, _abcIcon),
            new IntellisenseCompletionData("docs", 1, _abcIcon),
            new IntellisenseCompletionData("document", 1, _abcIcon),
            new IntellisenseCompletionData("documents", 1, _abcIcon),
            new IntellisenseCompletionData("double-circle", 1, _abcIcon),
            new IntellisenseCompletionData("event", 1, _abcIcon),
            new IntellisenseCompletionData("extract", 1, _abcIcon),
            new IntellisenseCompletionData("f-circ", 1, _abcIcon),
            new IntellisenseCompletionData("filled-circle", 1, _abcIcon),
            new IntellisenseCompletionData("flag", 1, _abcIcon),
            new IntellisenseCompletionData("flip-tri", 1, _abcIcon),
            new IntellisenseCompletionData("flipped-triangle", 1, _abcIcon),
            new IntellisenseCompletionData("fork", 1, _abcIcon),
            new IntellisenseCompletionData("fr-circ", 1, _abcIcon),
            new IntellisenseCompletionData("fr-rect", 1, _abcIcon),
            new IntellisenseCompletionData("framed-circle", 1, _abcIcon),
            new IntellisenseCompletionData("framed-rectangle", 1, _abcIcon),
            new IntellisenseCompletionData("h-cyl", 1, _abcIcon),
            new IntellisenseCompletionData("half-rounded-rectangle", 1, _abcIcon),
            new IntellisenseCompletionData("hex", 1, _abcIcon),
            new IntellisenseCompletionData("hexagon", 1, _abcIcon),
            new IntellisenseCompletionData("horizontal-cylinder", 1, _abcIcon),
            new IntellisenseCompletionData("hourglass", 1, _abcIcon),
            new IntellisenseCompletionData("in-out", 1, _abcIcon),
            new IntellisenseCompletionData("internal-storage", 1, _abcIcon),
            new IntellisenseCompletionData("inv-trapezoid", 1, _abcIcon),
            new IntellisenseCompletionData("join", 1, _abcIcon),
            new IntellisenseCompletionData("junction", 1, _abcIcon),
            new IntellisenseCompletionData("lean-l", 1, _abcIcon),
            new IntellisenseCompletionData("lean-left", 1, _abcIcon),
            new IntellisenseCompletionData("lean-r", 1, _abcIcon),
            new IntellisenseCompletionData("lean-right", 1, _abcIcon),
            new IntellisenseCompletionData("lightning-bolt", 1, _abcIcon),
            new IntellisenseCompletionData("lin-cyl", 1, _abcIcon),
            new IntellisenseCompletionData("lin-doc", 1, _abcIcon),
            new IntellisenseCompletionData("lin-proc", 1, _abcIcon),
            new IntellisenseCompletionData("lin-rect", 1, _abcIcon),
            new IntellisenseCompletionData("lined-cylinder", 1, _abcIcon),
            new IntellisenseCompletionData("lined-document", 1, _abcIcon),
            new IntellisenseCompletionData("lined-process", 1, _abcIcon),
            new IntellisenseCompletionData("lined-rectangle", 1, _abcIcon),
            new IntellisenseCompletionData("loop-limit", 1, _abcIcon),
            new IntellisenseCompletionData("manual", 1, _abcIcon),
            new IntellisenseCompletionData("manual-file", 1, _abcIcon),
            new IntellisenseCompletionData("manual-input", 1, _abcIcon),
            new IntellisenseCompletionData("notch-pent", 1, _abcIcon),
            new IntellisenseCompletionData("notch-rect", 1, _abcIcon),
            new IntellisenseCompletionData("notched-pentagon", 1, _abcIcon),
            new IntellisenseCompletionData("notched-rectangle", 1, _abcIcon),
            new IntellisenseCompletionData("odd", 1, _abcIcon),
            new IntellisenseCompletionData("out-in", 1, _abcIcon),
            new IntellisenseCompletionData("paper-tape", 1, _abcIcon),
            new IntellisenseCompletionData("pill", 1, _abcIcon),
            new IntellisenseCompletionData("prepare", 1, _abcIcon),
            new IntellisenseCompletionData("priority", 1, _abcIcon),
            new IntellisenseCompletionData("proc", 1, _abcIcon),
            new IntellisenseCompletionData("process", 1, _abcIcon),
            new IntellisenseCompletionData("processes", 1, _abcIcon),
            new IntellisenseCompletionData("procs", 1, _abcIcon),
            new IntellisenseCompletionData("question", 1, _abcIcon),
            new IntellisenseCompletionData("rect", 1, _abcIcon),
            new IntellisenseCompletionData("rectangle", 1, _abcIcon),
            new IntellisenseCompletionData("rounded", 1, _abcIcon),
            new IntellisenseCompletionData("shaded-process", 1, _abcIcon),
            new IntellisenseCompletionData("sl-rect", 1, _abcIcon),
            new IntellisenseCompletionData("sloped-rectangle", 1, _abcIcon),
            new IntellisenseCompletionData("sm-circ", 1, _abcIcon),
            new IntellisenseCompletionData("small-circle", 1, _abcIcon),
            new IntellisenseCompletionData("st-doc", 1, _abcIcon),
            new IntellisenseCompletionData("st-rect", 1, _abcIcon),
            new IntellisenseCompletionData("stacked-document", 1, _abcIcon),
            new IntellisenseCompletionData("stacked-rectangle", 1, _abcIcon),
            new IntellisenseCompletionData("stadium", 1, _abcIcon),
            new IntellisenseCompletionData("start", 1, _abcIcon),
            new IntellisenseCompletionData("stop", 1, _abcIcon),
            new IntellisenseCompletionData("stored-data", 1, _abcIcon),
            new IntellisenseCompletionData("subproc", 1, _abcIcon),
            new IntellisenseCompletionData("subprocess", 1, _abcIcon),
            new IntellisenseCompletionData("subroutine", 1, _abcIcon),
            new IntellisenseCompletionData("summary", 1, _abcIcon),
            new IntellisenseCompletionData("tag-doc", 1, _abcIcon),
            new IntellisenseCompletionData("tag-proc", 1, _abcIcon),
            new IntellisenseCompletionData("tag-rect", 1, _abcIcon),
            new IntellisenseCompletionData("tagged-document", 1, _abcIcon),
            new IntellisenseCompletionData("tagged-process", 1, _abcIcon),
            new IntellisenseCompletionData("tagged-rectangle", 1, _abcIcon),
            new IntellisenseCompletionData("terminal", 1, _abcIcon),
            new IntellisenseCompletionData("text", 1, _abcIcon),
            new IntellisenseCompletionData("trap-b", 1, _abcIcon),
            new IntellisenseCompletionData("trap-t", 1, _abcIcon),
            new IntellisenseCompletionData("trapezoid", 1, _abcIcon),
            new IntellisenseCompletionData("trapezoid-bottom", 1, _abcIcon),
            new IntellisenseCompletionData("trapezoid-top", 1, _abcIcon),
            new IntellisenseCompletionData("tri", 1, _abcIcon),
            new IntellisenseCompletionData("triangle", 1, _abcIcon),
            new IntellisenseCompletionData("win-pane", 1, _abcIcon),
            new IntellisenseCompletionData("window-pane", 1, _abcIcon),

            // Flowchart Theming: https://github.com/mermaid-js/mermaid/blob/develop/docs/config/theming.md#flowchart-variables
            new IntellisenseCompletionData("nodeBorder:", 1, _abcIcon),
            new IntellisenseCompletionData("clusterBkg:", 1, _abcIcon),
            new IntellisenseCompletionData("clusterBorder:", 1, _abcIcon),
            new IntellisenseCompletionData("defaultLinkColor:", 1, _abcIcon),
            new IntellisenseCompletionData("titleColor:", 1, _abcIcon),
            new IntellisenseCompletionData("edgeLabelBackground:", 1, _abcIcon),
            new IntellisenseCompletionData("nodeTextColor:", 1, _abcIcon),

            #endregion Flowchart

            #region Gantt

            // Gantt: https://mermaid.js.org/syntax/gantt.html
            new IntellisenseCompletionData("gantt", 1, _abcIcon),
            new IntellisenseCompletionData("dateFormat", 1, _abcIcon),
            new IntellisenseCompletionData("axisFormat", 1, _abcIcon),
            new IntellisenseCompletionData("title", 1, _abcIcon),
            new IntellisenseCompletionData("section", 1, _abcIcon),
            new IntellisenseCompletionData("excludes", 1, _abcIcon),
            new IntellisenseCompletionData("vert", 1, _abcIcon),
            new IntellisenseCompletionData("monday", 1, _abcIcon),
            new IntellisenseCompletionData("tuesday", 1, _abcIcon),
            new IntellisenseCompletionData("wednesday", 1, _abcIcon),
            new IntellisenseCompletionData("thursday", 1, _abcIcon),
            new IntellisenseCompletionData("friday", 1, _abcIcon),
            new IntellisenseCompletionData("saturday", 1, _abcIcon),
            new IntellisenseCompletionData("sunday", 1, _abcIcon),
            new IntellisenseCompletionData("weekend", 1, _abcIcon),
            new IntellisenseCompletionData("weekday", 1, _abcIcon),
            new IntellisenseCompletionData("active", 1, _abcIcon),
            new IntellisenseCompletionData("done", 1, _abcIcon),
            new IntellisenseCompletionData("crit", 1, _abcIcon),
            new IntellisenseCompletionData("milestone", 1, _abcIcon),
            new IntellisenseCompletionData("after", 1, _abcIcon),
            new IntellisenseCompletionData("until", 1, _abcIcon),
            new IntellisenseCompletionData("isadded", 1, _abcIcon),
            new IntellisenseCompletionData("tickInterval", 1, _abcIcon),
            new IntellisenseCompletionData("millisecond", 1, _abcIcon),
            new IntellisenseCompletionData("second", 1, _abcIcon),
            new IntellisenseCompletionData("minute", 1, _abcIcon),
            new IntellisenseCompletionData("hour", 1, _abcIcon),
            new IntellisenseCompletionData("day", 1, _abcIcon),
            new IntellisenseCompletionData("week", 1, _abcIcon),
            new IntellisenseCompletionData("month", 1, _abcIcon),
            new IntellisenseCompletionData("year", 1, _abcIcon),
            new IntellisenseCompletionData("todayMarker", 1, _abcIcon),
            new IntellisenseCompletionData("titleTopMargin:", 1, _abcIcon),
            new IntellisenseCompletionData("barHeight:", 1, _abcIcon),
            new IntellisenseCompletionData("barGap:", 1, _abcIcon),
            new IntellisenseCompletionData("topPadding:", 1, _abcIcon),
            new IntellisenseCompletionData("rightPadding:", 1, _abcIcon),
            new IntellisenseCompletionData("leftPadding:", 1, _abcIcon),
            new IntellisenseCompletionData("gridLineStartPadding:", 1, _abcIcon),
            new IntellisenseCompletionData("sectionFontSize:", 1, _abcIcon),
            new IntellisenseCompletionData("numberSectionStyles:", 1, _abcIcon),
            new IntellisenseCompletionData("topAxis:", 1, _abcIcon),
            new IntellisenseCompletionData("displayMode:", 1, _abcIcon),
            new IntellisenseCompletionData("compact", 1, _abcIcon),
            new IntellisenseCompletionData("mirrorActor:", 1, _abcIcon),
            new IntellisenseCompletionData("bottomMarginAdj:", 1, _abcIcon),

            #endregion Gantt

            #region GitGraph

            // GitGraph: https://mermaid.js.org/syntax/gitgraph.html
            new IntellisenseCompletionData("gitGraph", 1, _abcIcon),
            new IntellisenseCompletionData("commit", 1, _abcIcon),
            new IntellisenseCompletionData("branch", 1, _abcIcon),
            new IntellisenseCompletionData("merge", 1, _abcIcon),
            new IntellisenseCompletionData("checkout", 1, _abcIcon),
            new IntellisenseCompletionData("cherry-pick", 1, _abcIcon),
            new IntellisenseCompletionData("reset", 1, _abcIcon),
            new IntellisenseCompletionData("switch", 1, _abcIcon),
            new IntellisenseCompletionData("main", 1, _abcIcon),
            new IntellisenseCompletionData("develop", 1, _abcIcon),
            new IntellisenseCompletionData("release", 1, _abcIcon),
            new IntellisenseCompletionData("NORMAL", 1, _abcIcon),
            new IntellisenseCompletionData("REVERSE", 1, _abcIcon),
            new IntellisenseCompletionData("HIGHLIGHT", 1, _abcIcon),
            new IntellisenseCompletionData("id:", 1, _abcIcon),
            new IntellisenseCompletionData("type:", 1, _abcIcon),
            new IntellisenseCompletionData("tag:", 1, _abcIcon),
            new IntellisenseCompletionData("parent:", 1, _abcIcon),
            new IntellisenseCompletionData("showBranches:", 1, _abcIcon),
            new IntellisenseCompletionData("showCommitLabel:", 1, _abcIcon),
            new IntellisenseCompletionData("mainBranchName:", 1, _abcIcon),
            new IntellisenseCompletionData("mainBranchOrder:", 1, _abcIcon),
            new IntellisenseCompletionData("parallelCommits:", 1, _abcIcon),
            new IntellisenseCompletionData("rotateCommitLabel:", 1, _abcIcon),
            new IntellisenseCompletionData("order:", 1, _abcIcon),
            new IntellisenseCompletionData("LR:", 1, _abcIcon),
            new IntellisenseCompletionData("TB:", 1, _abcIcon),
            new IntellisenseCompletionData("BT:", 1, _abcIcon),
            new IntellisenseCompletionData("git0", 1, _abcIcon),
            new IntellisenseCompletionData("git1", 1, _abcIcon),
            new IntellisenseCompletionData("git2", 1, _abcIcon),
            new IntellisenseCompletionData("git3", 1, _abcIcon),
            new IntellisenseCompletionData("git4", 1, _abcIcon),
            new IntellisenseCompletionData("git5", 1, _abcIcon),
            new IntellisenseCompletionData("git6", 1, _abcIcon),
            new IntellisenseCompletionData("git7", 1, _abcIcon),
            new IntellisenseCompletionData("gitBranchLabel0", 1, _abcIcon),
            new IntellisenseCompletionData("gitBranchLabel1", 1, _abcIcon),
            new IntellisenseCompletionData("gitBranchLabel2", 1, _abcIcon),
            new IntellisenseCompletionData("gitBranchLabel3", 1, _abcIcon),
            new IntellisenseCompletionData("gitBranchLabel4", 1, _abcIcon),
            new IntellisenseCompletionData("gitBranchLabel5", 1, _abcIcon),
            new IntellisenseCompletionData("gitBranchLabel6", 1, _abcIcon),
            new IntellisenseCompletionData("gitBranchLabel7", 1, _abcIcon),
            new IntellisenseCompletionData("gitInv0", 1, _abcIcon),
            new IntellisenseCompletionData("gitInv1", 1, _abcIcon),
            new IntellisenseCompletionData("gitInv2", 1, _abcIcon),
            new IntellisenseCompletionData("gitInv3", 1, _abcIcon),
            new IntellisenseCompletionData("gitInv4", 1, _abcIcon),
            new IntellisenseCompletionData("gitInv5", 1, _abcIcon),
            new IntellisenseCompletionData("gitInv6", 1, _abcIcon),
            new IntellisenseCompletionData("gitInv7", 1, _abcIcon),
            new IntellisenseCompletionData("commitLabelColor", 1, _abcIcon),
            new IntellisenseCompletionData("commitLabelBackground", 1, _abcIcon),
            new IntellisenseCompletionData("commitLabelFontSize", 1, _abcIcon),
            new IntellisenseCompletionData("tagLabelFontSize", 1, _abcIcon),
            new IntellisenseCompletionData("tagLabelColor", 1, _abcIcon),
            new IntellisenseCompletionData("tagLabelBackground", 1, _abcIcon),
            new IntellisenseCompletionData("tagLabelBorder", 1, _abcIcon),

            #endregion GitGraph

            #region Kanban

            // Kanban: https://mermaid.js.org/syntax/kanban.html
            new IntellisenseCompletionData("kanban", 1, _abcIcon),
            new IntellisenseCompletionData("todo", 1, _abcIcon),
            new IntellisenseCompletionData("assigned:", 1, _abcIcon),
            new IntellisenseCompletionData("ticket:", 1, _abcIcon),
            new IntellisenseCompletionData("priority:", 1, _abcIcon),
            new IntellisenseCompletionData("ticketBaseUrl:", 1, _abcIcon),

            #endregion Kanban

            #region Mindmap

            // Mindmap: https://mermaid.js.org/syntax/mindmap.html
            new IntellisenseCompletionData("mindmap", 1, _abcIcon),
            new IntellisenseCompletionData("mindmap-v2", 1, _abcIcon),
            new IntellisenseCompletionData("root", 1, _abcIcon),
            new IntellisenseCompletionData("tidy-tree", 1, _abcIcon),

            #endregion Mindmap

            #region Packet

            // Packet: https://mermaid.js.org/syntax/packet.html
            new IntellisenseCompletionData("packet", 1, _abcIcon),
            new IntellisenseCompletionData("title", 1, _abcIcon),
            new IntellisenseCompletionData("rowHeight:", 1, _abcIcon),
            new IntellisenseCompletionData("bitWidth:", 1, _abcIcon),
            new IntellisenseCompletionData("bitsPerRow:", 1, _abcIcon),
            new IntellisenseCompletionData("showBits:", 1, _abcIcon),
            new IntellisenseCompletionData("paddingX:", 1, _abcIcon),
            new IntellisenseCompletionData("paddingY:", 1, _abcIcon),

            #endregion Packet

            #region Pie Charts

            // Pie: https://mermaid.js.org/syntax/pie.html
            new IntellisenseCompletionData("pie", 1, _abcIcon),
            new IntellisenseCompletionData("showData", 1, _abcIcon),
            new IntellisenseCompletionData("title", 1, _abcIcon),
            new IntellisenseCompletionData("textPosition:", 1, _abcIcon),

            // Pie Chart Theming: https://github.com/mermaid-js/mermaid/blob/develop/docs/config/theming.md#pie-diagram-variables
            new IntellisenseCompletionData("pieTitleTextSize:", 1, _abcIcon),
            new IntellisenseCompletionData("pieTitleTextColor:", 1, _abcIcon),
            new IntellisenseCompletionData("pieSectionTextSize:", 1, _abcIcon),
            new IntellisenseCompletionData("pieSectionTextColor:", 1, _abcIcon),
            new IntellisenseCompletionData("pieLegendTextSize:", 1, _abcIcon),
            new IntellisenseCompletionData("pieLegendTextColor:", 1, _abcIcon),
            new IntellisenseCompletionData("pieStrokeColor:", 1, _abcIcon),
            new IntellisenseCompletionData("pieStrokeWidth:", 1, _abcIcon),
            new IntellisenseCompletionData("pieOuterStrokeWidth:", 1, _abcIcon),
            new IntellisenseCompletionData("pieOuterStrokeColor:", 1, _abcIcon),
            new IntellisenseCompletionData("pieOpacity:", 1, _abcIcon),

            #endregion Pie Charts

            #region Quadrant Diagrams

            // Quadrant: https://mermaid.js.org/syntax/quadrantChart.html
            new IntellisenseCompletionData("quadrantChart", 1, _abcIcon),
            new IntellisenseCompletionData("title", 1, _abcIcon),
            new IntellisenseCompletionData("x-axis", 1, _abcIcon),
            new IntellisenseCompletionData("y-axis", 1, _abcIcon),
            new IntellisenseCompletionData("quadrant-1", 1, _abcIcon),
            new IntellisenseCompletionData("quadrant-2", 1, _abcIcon),
            new IntellisenseCompletionData("quadrant-3", 1, _abcIcon),
            new IntellisenseCompletionData("quadrant-4", 1, _abcIcon),

            // Quadrant Theming: https://mermaid.js.org/syntax/quadrantChart.html#chart-configurations
            new IntellisenseCompletionData("chartWidth:", 1, _abcIcon),
            new IntellisenseCompletionData("chartHeight:", 1, _abcIcon),
            new IntellisenseCompletionData("titlePadding:", 1, _abcIcon),
            new IntellisenseCompletionData("titleFontSize:", 1, _abcIcon),
            new IntellisenseCompletionData("quadrantPadding:", 1, _abcIcon),
            new IntellisenseCompletionData("quadrantTextTopPadding:", 1, _abcIcon),
            new IntellisenseCompletionData("quadrantLabelFontSize:", 1, _abcIcon),
            new IntellisenseCompletionData("quadrantInternalBorderStrokeWidth:", 1, _abcIcon),
            new IntellisenseCompletionData("quadrantExternalBorderStrokeWidth:", 1, _abcIcon),
            new IntellisenseCompletionData("xAxisLabelPadding:", 1, _abcIcon),
            new IntellisenseCompletionData("xAxisLabelFontSize:", 1, _abcIcon),
            new IntellisenseCompletionData("xAxisPosition:", 1, _abcIcon),
            new IntellisenseCompletionData("yAxisLabelPadding:", 1, _abcIcon),
            new IntellisenseCompletionData("yAxisLabelFontSize:", 1, _abcIcon),
            new IntellisenseCompletionData("yAxisPosition:", 1, _abcIcon),
            new IntellisenseCompletionData("pointTextPadding:", 1, _abcIcon),
            new IntellisenseCompletionData("pointLabelFontSize:", 1, _abcIcon),
            new IntellisenseCompletionData("pointRadius:", 1, _abcIcon),
            new IntellisenseCompletionData("quadrant1Fill:", 1, _abcIcon),
            new IntellisenseCompletionData("quadrant2Fill:", 1, _abcIcon),
            new IntellisenseCompletionData("quadrant3Fill:", 1, _abcIcon),
            new IntellisenseCompletionData("quadrant4Fill:", 1, _abcIcon),
            new IntellisenseCompletionData("quadrant1TextFill:", 1, _abcIcon),
            new IntellisenseCompletionData("quadrant2TextFill:", 1, _abcIcon),
            new IntellisenseCompletionData("quadrant3TextFill:", 1, _abcIcon),
            new IntellisenseCompletionData("quadrant4TextFill:", 1, _abcIcon),
            new IntellisenseCompletionData("quadrantPointFill:", 1, _abcIcon),
            new IntellisenseCompletionData("quadrantPointTextFill:", 1, _abcIcon),
            new IntellisenseCompletionData("quadrantXAxisTextFill:", 1, _abcIcon),
            new IntellisenseCompletionData("quadrantYAxisTextFill:", 1, _abcIcon),
            new IntellisenseCompletionData("quadrantInternalBorderStrokeFill:", 1, _abcIcon),
            new IntellisenseCompletionData("quadrantExternalBorderStrokeFill:", 1, _abcIcon),
            new IntellisenseCompletionData("quadrantTitleFill:", 1, _abcIcon),

            new IntellisenseCompletionData("color:", 1, _abcIcon),
            new IntellisenseCompletionData("radius:", 1, _abcIcon),
            new IntellisenseCompletionData("stroke-width:", 1, _abcIcon),
            new IntellisenseCompletionData("stroke-color:", 1, _abcIcon),

            #endregion Quadrant Diagrams

            #region Radar Charts

            // Radar: https://mermaid.js.org/syntax/radar.html
            new IntellisenseCompletionData("radar-beta", 1, _abcIcon),
            new IntellisenseCompletionData("title", 1, _abcIcon),
            new IntellisenseCompletionData("axis", 1, _abcIcon),
            new IntellisenseCompletionData("curve", 1, _abcIcon),
            new IntellisenseCompletionData("showLegend", 1, _abcIcon),
            new IntellisenseCompletionData("max", 1, _abcIcon),
            new IntellisenseCompletionData("min", 1, _abcIcon),
            new IntellisenseCompletionData("graticule", 1, _abcIcon),
            new IntellisenseCompletionData("circle", 1, _abcIcon),
            new IntellisenseCompletionData("polygon", 1, _abcIcon),
            new IntellisenseCompletionData("ticks", 1, _abcIcon),

            // Radar Chart Theming: https://mermaid.js.org/syntax/radar.html#configuration
            new IntellisenseCompletionData("width:", 1, _abcIcon),
            new IntellisenseCompletionData("height:", 1, _abcIcon),
            new IntellisenseCompletionData("marginTop:", 1, _abcIcon),
            new IntellisenseCompletionData("marginBottom:", 1, _abcIcon),
            new IntellisenseCompletionData("marginLeft:", 1, _abcIcon),
            new IntellisenseCompletionData("marginRight:", 1, _abcIcon),
            new IntellisenseCompletionData("axisScaleFactor:", 1, _abcIcon),
            new IntellisenseCompletionData("axisLabelFactor:", 1, _abcIcon),
            new IntellisenseCompletionData("curveTension:", 1, _abcIcon),

            // Radar Chart Global Theming: https://mermaid.js.org/syntax/radar.html#global-theme-variables
            new IntellisenseCompletionData("fontSize:", 1, _abcIcon),
            new IntellisenseCompletionData("titleColor:", 1, _abcIcon),
            new IntellisenseCompletionData("cScale", 1, _abcIcon),

            new IntellisenseCompletionData("axisColor:", 1, _abcIcon),
            new IntellisenseCompletionData("axisStrokeWidth:", 1, _abcIcon),
            new IntellisenseCompletionData("axisLabelFontSize:", 1, _abcIcon),
            new IntellisenseCompletionData("curveOpacity:", 1, _abcIcon),
            new IntellisenseCompletionData("curveStrokeWidth:", 1, _abcIcon),
            new IntellisenseCompletionData("graticuleColor:", 1, _abcIcon),
            new IntellisenseCompletionData("graticuleOpacity:", 1, _abcIcon),
            new IntellisenseCompletionData("graticuleStrokeWidth:", 1, _abcIcon),
            new IntellisenseCompletionData("legendBoxSize:", 1, _abcIcon),
            new IntellisenseCompletionData("legendFontSize:", 1, _abcIcon),

            #endregion Radar Charts

            #region Requirement Diagram

            // Requirement: https://mermaid.js.org/syntax/requirementDiagram.html
            new IntellisenseCompletionData("requirementDiagram", 1, _abcIcon),
            new IntellisenseCompletionData("requirement", 1, _abcIcon),
            new IntellisenseCompletionData("element", 1, _abcIcon),
            new IntellisenseCompletionData("functionalRequirement", 1, _abcIcon),
            new IntellisenseCompletionData("interfaceRequirement", 1, _abcIcon),
            new IntellisenseCompletionData("performanceRequirement", 1, _abcIcon),
            new IntellisenseCompletionData("physicalRequirement", 1, _abcIcon),
            new IntellisenseCompletionData("designConstraint", 1, _abcIcon),
            new IntellisenseCompletionData("id:", 1, _abcIcon),
            new IntellisenseCompletionData("risk:", 1, _abcIcon),
            new IntellisenseCompletionData("text:", 1, _abcIcon),
            new IntellisenseCompletionData("type:", 1, _abcIcon),
            new IntellisenseCompletionData("verifymethod:", 1, _abcIcon),
            new IntellisenseCompletionData("docref:", 1, _abcIcon),
            new IntellisenseCompletionData("analysis", 1, _abcIcon),
            new IntellisenseCompletionData("inspection", 1, _abcIcon),
            new IntellisenseCompletionData("test", 1, _abcIcon),
            new IntellisenseCompletionData("demonstration", 1, _abcIcon),
            new IntellisenseCompletionData("direction", 1, _abcIcon),
            new IntellisenseCompletionData("TB", 1, _abcIcon),
            new IntellisenseCompletionData("BT", 1, _abcIcon),
            new IntellisenseCompletionData("LR", 1, _abcIcon),
            new IntellisenseCompletionData("RL", 1, _abcIcon),
            new IntellisenseCompletionData("simulation", 1, _abcIcon),
            new IntellisenseCompletionData("style", 1, _abcIcon),
            new IntellisenseCompletionData("class", 1, _abcIcon),
            new IntellisenseCompletionData("classDef", 1, _abcIcon),

            #endregion Requirement Diagram

            #region Sankey

            // Sankey Diagram: https://mermaid.js.org/syntax/sankey.html
            new IntellisenseCompletionData("sankey", 1, _abcIcon),
            new IntellisenseCompletionData("source", 1, _abcIcon),
            new IntellisenseCompletionData("target", 1, _abcIcon),
            new IntellisenseCompletionData("value", 1, _abcIcon),
            new IntellisenseCompletionData("width:", 1, _abcIcon),
            new IntellisenseCompletionData("height:", 1, _abcIcon),
            new IntellisenseCompletionData("linkColor:", 1, _abcIcon),
            new IntellisenseCompletionData("nodeAlignment:", 1, _abcIcon),
            new IntellisenseCompletionData("gradient", 1, _abcIcon),
            new IntellisenseCompletionData("justify", 1, _abcIcon),
            new IntellisenseCompletionData("center", 1, _abcIcon),
            new IntellisenseCompletionData("left", 1, _abcIcon),
            new IntellisenseCompletionData("right", 1, _abcIcon),

            #endregion Sankey

            #region Sequence Diagram

            // Sequence Diagram: https://mermaid.js.org/syntax/sequenceDiagram.html
            new IntellisenseCompletionData("sequenceDiagram", 1, _abcIcon),
            new IntellisenseCompletionData("participant", 1, _abcIcon),
            new IntellisenseCompletionData("actor", 1, _abcIcon),
            new IntellisenseCompletionData("boundary", 1, _abcIcon),
            new IntellisenseCompletionData("control", 1, _abcIcon),
            new IntellisenseCompletionData("entity", 1, _abcIcon),
            new IntellisenseCompletionData("database", 1, _abcIcon),
            new IntellisenseCompletionData("collections", 1, _abcIcon),
            new IntellisenseCompletionData("queue", 1, _abcIcon),
            new IntellisenseCompletionData("as", 1, _abcIcon),
            new IntellisenseCompletionData("create", 1, _abcIcon),
            new IntellisenseCompletionData("destroy", 1, _abcIcon),
            new IntellisenseCompletionData("activate", 1, _abcIcon),
            new IntellisenseCompletionData("deactivate", 1, _abcIcon),
            new IntellisenseCompletionData("note", 1, _abcIcon),
            new IntellisenseCompletionData("<br/>", 1, _abcIcon),

            // Arrows
            new IntellisenseCompletionData("->", 1, _abcIcon),
            new IntellisenseCompletionData("-->", 1, _abcIcon),
            new IntellisenseCompletionData("->>", 1, _abcIcon),
            new IntellisenseCompletionData("-->>", 1, _abcIcon),
            new IntellisenseCompletionData("<<->>", 1, _abcIcon),
            new IntellisenseCompletionData("<<-->>", 1, _abcIcon),
            new IntellisenseCompletionData("-x", 1, _abcIcon),
            new IntellisenseCompletionData("--x", 1, _abcIcon),
            new IntellisenseCompletionData("-)", 1, _abcIcon),
            new IntellisenseCompletionData("--)", 1, _abcIcon),

            new IntellisenseCompletionData("box", 1, _abcIcon),
            new IntellisenseCompletionData("loop", 1, _abcIcon),
            new IntellisenseCompletionData("end", 1, _abcIcon),
            new IntellisenseCompletionData("alt", 1, _abcIcon),
            new IntellisenseCompletionData("else", 1, _abcIcon),
            new IntellisenseCompletionData("opt", 1, _abcIcon),
            new IntellisenseCompletionData("par", 1, _abcIcon),
            new IntellisenseCompletionData("and", 1, _abcIcon),
            new IntellisenseCompletionData("critical", 1, _abcIcon),
            new IntellisenseCompletionData("break", 1, _abcIcon),
            new IntellisenseCompletionData("rect", 1, _abcIcon),
            new IntellisenseCompletionData("rgb", 1, _abcIcon),
            new IntellisenseCompletionData("rgba", 1, _abcIcon),
            new IntellisenseCompletionData("autonumber", 1, _abcIcon),
            new IntellisenseCompletionData("link", 1, _abcIcon),
            new IntellisenseCompletionData("links", 1, _abcIcon),

            // Styling: https://mermaid.js.org/syntax/sequenceDiagram.html#classes-used
            new IntellisenseCompletionData("actor", 1, _abcIcon),
            new IntellisenseCompletionData("actor-top", 1, _abcIcon),
            new IntellisenseCompletionData("actor-bottom", 1, _abcIcon),
            new IntellisenseCompletionData("text.actor", 1, _abcIcon),
            new IntellisenseCompletionData("text.actor-box", 1, _abcIcon),
            new IntellisenseCompletionData("text.actor-man", 1, _abcIcon),
            new IntellisenseCompletionData("actor-line", 1, _abcIcon),
            new IntellisenseCompletionData("messageLine0", 1, _abcIcon),
            new IntellisenseCompletionData("messageLine1", 1, _abcIcon),
            new IntellisenseCompletionData("messageText", 1, _abcIcon),
            new IntellisenseCompletionData("labelBox", 1, _abcIcon),
            new IntellisenseCompletionData("labelText", 1, _abcIcon),
            new IntellisenseCompletionData("loopText", 1, _abcIcon),
            new IntellisenseCompletionData("loopLine", 1, _abcIcon),
            new IntellisenseCompletionData("note", 1, _abcIcon),
            new IntellisenseCompletionData("noteText", 1, _abcIcon),

            // Configuration: https://mermaid.js.org/syntax/sequenceDiagram.html#configuration
            new IntellisenseCompletionData("diagramMarginX:", 1, _abcIcon),
            new IntellisenseCompletionData("diagramMarginY:", 1, _abcIcon),
            new IntellisenseCompletionData("boxTextMargin:", 1, _abcIcon),
            new IntellisenseCompletionData("noteMargin:", 1, _abcIcon),
            new IntellisenseCompletionData("messageMargin:", 1, _abcIcon),

            // https://mermaid.js.org/syntax/sequenceDiagram.html#possible-configuration-parameters
            new IntellisenseCompletionData("mirrorActors:", 1, _abcIcon),
            new IntellisenseCompletionData("bottomMarginAdj:", 1, _abcIcon),
            new IntellisenseCompletionData("actorFontSize:", 1, _abcIcon),
            new IntellisenseCompletionData("actorFontFamily:", 1, _abcIcon),
            new IntellisenseCompletionData("actorFontWeight:", 1, _abcIcon),
            new IntellisenseCompletionData("noteFontSize:", 1, _abcIcon),
            new IntellisenseCompletionData("noteFontFamily:", 1, _abcIcon),
            new IntellisenseCompletionData("noteFontWeight:", 1, _abcIcon),
            new IntellisenseCompletionData("noteAlign:", 1, _abcIcon),
            new IntellisenseCompletionData("messageFontSize:", 1, _abcIcon),
            new IntellisenseCompletionData("messageFontFamily:", 1, _abcIcon),
            new IntellisenseCompletionData("messageFontWeight:", 1, _abcIcon),

            // Sequence Diagram Theming: https://github.com/mermaid-js/mermaid/blob/develop/docs/config/theming.md#sequence-diagram-variables
            new IntellisenseCompletionData("actorBkg:", 1, _abcIcon),
            new IntellisenseCompletionData("actorBorder:", 1, _abcIcon),
            new IntellisenseCompletionData("actorTextColor:", 1, _abcIcon),
            new IntellisenseCompletionData("actorLineColor:", 1, _abcIcon),
            new IntellisenseCompletionData("signalColor:", 1, _abcIcon),
            new IntellisenseCompletionData("signalTextColor:", 1, _abcIcon),
            new IntellisenseCompletionData("labelBoxBkgColor:", 1, _abcIcon),
            new IntellisenseCompletionData("labelBoxBorderColor:", 1, _abcIcon),
            new IntellisenseCompletionData("labelTextColor:", 1, _abcIcon),
            new IntellisenseCompletionData("loopTextColor:", 1, _abcIcon),
            new IntellisenseCompletionData("activationBorderColor:", 1, _abcIcon),
            new IntellisenseCompletionData("activationBkgColor:", 1, _abcIcon),
            new IntellisenseCompletionData("sequenceNumberColor:", 1, _abcIcon),

            #endregion Sequence Diagram

            #region State Diagrams

            // State Diagram: https://mermaid.js.org/syntax/stateDiagram.html
            new IntellisenseCompletionData("stateDiagram", 1, _abcIcon),
            new IntellisenseCompletionData("stateDiagram-v2", 1, _abcIcon),
            new IntellisenseCompletionData("state", 1, _abcIcon),
            new IntellisenseCompletionData("as", 1, _abcIcon),
            new IntellisenseCompletionData("note", 1, _abcIcon),
            new IntellisenseCompletionData("end note", 1, _abcIcon),

            new IntellisenseCompletionData("[*]", 1, _abcIcon),
            new IntellisenseCompletionData("--", 1, _abcIcon),
            new IntellisenseCompletionData("-->", 1, _abcIcon),
            new IntellisenseCompletionData(":::", 1, _abcIcon),
            new IntellisenseCompletionData("<<choice>>", 1, _abcIcon),
            new IntellisenseCompletionData("<<fork>>", 1, _abcIcon),
            new IntellisenseCompletionData("<<join>>", 1, _abcIcon),
            new IntellisenseCompletionData("direction", 1, _abcIcon),
            new IntellisenseCompletionData("class", 1, _abcIcon),
            new IntellisenseCompletionData("classDef", 1, _abcIcon),

            new IntellisenseCompletionData("TB", 1, _abcIcon),
            new IntellisenseCompletionData("BT", 1, _abcIcon),
            new IntellisenseCompletionData("RL", 1, _abcIcon),
            new IntellisenseCompletionData("LR", 1, _abcIcon),

            // State Diagram Theming: https://github.com/mermaid-js/mermaid/blob/develop/docs/config/theming.md#state-colors
            new IntellisenseCompletionData("labelColor:", 1, _abcIcon),
            new IntellisenseCompletionData("altBackground:", 1, _abcIcon),

            #endregion State Diagrams

            #region Timeline

            // Timeline: https://mermaid.js.org/syntax/timeline.html
            new IntellisenseCompletionData("timeline", 1, _abcIcon),
            new IntellisenseCompletionData("title", 1, _abcIcon),
            new IntellisenseCompletionData("section", 1, _abcIcon),
            new IntellisenseCompletionData("<br/>", 1, _abcIcon),
            new IntellisenseCompletionData("disableMultiColor:", 1, _abcIcon),
            new IntellisenseCompletionData("cScale", 1, _abcIcon),
            new IntellisenseCompletionData("cScaleLabel", 1, _abcIcon),
            new IntellisenseCompletionData("section", 1, _abcIcon),

            #endregion Timeline

            #region Treemap

            // Treemap: https://mermaid.js.org/syntax/treemap.html
            new IntellisenseCompletionData("treemap", 1, _abcIcon),
            new IntellisenseCompletionData("title", 1, _abcIcon),
            new IntellisenseCompletionData("classDef", 1, _abcIcon),

            // Treemap Theming: https://mermaid.js.org/syntax/treemap.html#diagram-padding
            new IntellisenseCompletionData("diagramPadding:", 1, _abcIcon),

            // https://mermaid.js.org/syntax/treemap.html#configuration-options
            new IntellisenseCompletionData("useMaxWidth:", 1, _abcIcon),
            new IntellisenseCompletionData("padding:", 1, _abcIcon),
            new IntellisenseCompletionData("diagramPadding:", 1, _abcIcon),
            new IntellisenseCompletionData("showValues:", 1, _abcIcon),
            new IntellisenseCompletionData("nodeWidth:", 1, _abcIcon),
            new IntellisenseCompletionData("nodeHeight:", 1, _abcIcon),
            new IntellisenseCompletionData("borderWidth:", 1, _abcIcon),
            new IntellisenseCompletionData("valueFontSize:", 1, _abcIcon),
            new IntellisenseCompletionData("labelFontSize:", 1, _abcIcon),
            new IntellisenseCompletionData("valueFormat:", 1, _abcIcon),

            #endregion Treemap

            #region UserJourney

            // Journey: https://mermaid.js.org/syntax/userJourney.html
            new IntellisenseCompletionData("journey", 1, _abcIcon),
            new IntellisenseCompletionData("section", 1, _abcIcon),
            new IntellisenseCompletionData("title", 1, _abcIcon),

            // Journey Theming: https://github.com/mermaid-js/mermaid/blob/develop/docs/config/theming.md#user-journey-colors
            new IntellisenseCompletionData("fillType0:", 1, _abcIcon),
            new IntellisenseCompletionData("fillType1:", 1, _abcIcon),
            new IntellisenseCompletionData("fillType2:", 1, _abcIcon),
            new IntellisenseCompletionData("fillType3:", 1, _abcIcon),
            new IntellisenseCompletionData("fillType4:", 1, _abcIcon),
            new IntellisenseCompletionData("fillType5:", 1, _abcIcon),
            new IntellisenseCompletionData("fillType6:", 1, _abcIcon),
            new IntellisenseCompletionData("fillType7:", 1, _abcIcon),

            #endregion UserJourney

            #region XY Chart

            // XY Chart: https://mermaid.js.org/syntax/xyChart.html
            new IntellisenseCompletionData("xychart", 1, _abcIcon),
            new IntellisenseCompletionData("title", 1, _abcIcon),
            new IntellisenseCompletionData("x-axis", 1, _abcIcon),
            new IntellisenseCompletionData("y-axis", 1, _abcIcon),
            new IntellisenseCompletionData("series", 1, _abcIcon),
            new IntellisenseCompletionData("type", 1, _abcIcon),
            new IntellisenseCompletionData("line", 1, _abcIcon),
            new IntellisenseCompletionData("bar", 1, _abcIcon),
            new IntellisenseCompletionData("horizontal", 1, _abcIcon),
            new IntellisenseCompletionData("vertical", 1, _abcIcon),
            new IntellisenseCompletionData("max", 1, _abcIcon),
            new IntellisenseCompletionData("min", 1, _abcIcon),
            new IntellisenseCompletionData("-->", 1, _abcIcon),

            // XY Chart Config: https://mermaid.js.org/syntax/xyChart.html#chart-configurations
            new IntellisenseCompletionData("width:", 1, _abcIcon),
            new IntellisenseCompletionData("height:", 1, _abcIcon),
            new IntellisenseCompletionData("titlePadding:", 1, _abcIcon),
            new IntellisenseCompletionData("titleFontSize:", 1, _abcIcon),
            new IntellisenseCompletionData("showTitle:", 1, _abcIcon),
            new IntellisenseCompletionData("xAxis:", 1, _abcIcon),
            new IntellisenseCompletionData("yAxis:", 1, _abcIcon),
            new IntellisenseCompletionData("chartOrientation:", 1, _abcIcon),
            new IntellisenseCompletionData("plotReservedSpacePercent:", 1, _abcIcon),
            new IntellisenseCompletionData("showDataLabel:", 1, _abcIcon),

            // XY Chart AxisConfig: https://mermaid.js.org/syntax/xyChart.html#axisconfig
            new IntellisenseCompletionData("showLabel:", 1, _abcIcon),
            new IntellisenseCompletionData("labelFontSize:", 1, _abcIcon),
            new IntellisenseCompletionData("labelPadding:", 1, _abcIcon),
            new IntellisenseCompletionData("showTitle:", 1, _abcIcon),
            new IntellisenseCompletionData("titleFontSize:", 1, _abcIcon),
            new IntellisenseCompletionData("titlePadding:", 1, _abcIcon),
            new IntellisenseCompletionData("showTick:", 1, _abcIcon),
            new IntellisenseCompletionData("tickLength:", 1, _abcIcon),
            new IntellisenseCompletionData("tickWidth:", 1, _abcIcon),
            new IntellisenseCompletionData("showAxisLine:", 1, _abcIcon),
            new IntellisenseCompletionData("axisLineWidth:", 1, _abcIcon),

            // Chart Theme Variables: https://mermaid.js.org/syntax/xyChart.html#chart-theme-variables
            new IntellisenseCompletionData("backgroundColor:", 1, _abcIcon),
            new IntellisenseCompletionData("titleColor:", 1, _abcIcon),
            new IntellisenseCompletionData("xAxisLabelColor:", 1, _abcIcon),
            new IntellisenseCompletionData("xAxisTitleColor:", 1, _abcIcon),
            new IntellisenseCompletionData("xAxisTickColor:", 1, _abcIcon),
            new IntellisenseCompletionData("xAxisLineColor:", 1, _abcIcon),
            new IntellisenseCompletionData("yAxisLabelColor:", 1, _abcIcon),
            new IntellisenseCompletionData("yAxisTitleColor:", 1, _abcIcon),
            new IntellisenseCompletionData("yAxisTickColor:", 1, _abcIcon),
            new IntellisenseCompletionData("yAxisLineColor:", 1, _abcIcon),
            new IntellisenseCompletionData("plotColorPalette:", 1, _abcIcon),

            #endregion XY Chart
        }
        .DistinctBy(static x => x.Text)     // Avoid duplicates like "database", "section"
                                            // No need to sort here; we sort later in PopulateCompletionData
        .ToArray();                         // Using array for better memory locality and keeping this immutable

    /// <summary>
    /// Indicates, for each character code, whether the character is considered a trigger character.
    /// </summary>
    /// <remarks>A trigger character is one that initiates a specific action or behavior in the parsing or
    /// processing logic. The array is indexed by character code, and a value of <see langword="true"/> at a given index
    /// means the corresponding character is a trigger character.</remarks>
    private static readonly bool[] _completionTriggerFlags = InitializeCompletionTriggerFlags();

    /// <summary>
    /// Initializes event handlers required to enable IntelliSense functionality in the editor.
    /// </summary>
    /// <remarks>Call this method during editor setup to ensure that IntelliSense features, such as code
    /// completion, are available to users. This method should be invoked before the user begins editing to guarantee
    /// correct behavior.</remarks>
    private void InitializeIntellisense()
    {
        Editor.TextArea.TextEntered += TextArea_TextEntered;
        Editor.TextArea.TextEntering += TextArea_TextEntering;
    }

    /// <summary>
    /// Initializes and returns a Boolean array indicating which ASCII characters are considered valid
    /// trigger characters to invoke Intellisense completion.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The returned array can be used to efficiently check whether a given ASCII character is a
    /// valid trigger character by using its code as an index.
    /// </para>
    /// <para>
    ///     NOTE: This method looks identical to <see cref="IntellisenseScanner.InitializeValidIdentifierFlags"/> but it serves a different purpose.
    ///     This method identifies: What keystroke should start/trigger the autocomplete popup?
    /// </para></remarks>
    /// <returns>A Boolean array of length <see cref="LookupTableSize"/> where each element is <see langword="true"/> if the corresponding ASCII character
    /// is a letter, digit, underscore ('_'), greater-than sign ('>'), or space character; otherwise, <see langword="false"/>.
    /// </returns>
    private static bool[] InitializeCompletionTriggerFlags()
    {
        bool[] flags = new bool[LookupTableSize];
        for (int i = 0; i < LookupTableSize; i++)
        {
            char c = (char)i;
            if ((c >= 'a' && c <= 'z') ||
                (c >= 'A' && c <= 'Z') ||
                (c >= '0' && c <= '9') ||
                c == '_' ||
                c == '>' ||
                c == ' ')
            {
                flags[i] = true;
            }
        }
        return flags;
    }

    /// <summary>
    /// Handles the TextEntering event for the text area, processing user input and managing completion window behavior
    /// during text entry.
    /// </summary>
    /// <remarks>This method is typically used to coordinate code completion or intellisense features in
    /// response to user typing. If a completion window is open and the entered text is not an identifier character, the
    /// method may trigger insertion requests to the completion list. This helps ensure that completion suggestions are
    /// managed appropriately as the user types.</remarks>
    /// <param name="sender">The source of the event, typically the text area control where text is being entered.</param>
    /// <param name="e">An object containing information about the text input event, including the entered text.</param>
    private void TextArea_TextEntering(object? sender, TextInputEventArgs e)
    {
        if (e.Text?.Length > 0 && _completionWindow is not null)
        {
            // If the user types a non-identifier character while the window is open,
            // we might want to manually close it, though AvaloniaEdit handles much of this.
            char c = e.Text[0];

            // Check if char is NOT a valid identifier part (Letter, Digit, Dash, Underscore)
            if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
            {
                _completionWindow.CompletionList.RequestInsertion(e);
            }
        }
    }

    /// <summary>
    /// Handles the event when text is entered into the text area and triggers the completion window if appropriate.
    /// </summary>
    /// <remarks>The completion window is only shown if a single character is entered and no completion window
    /// is currently open. This method does not perform any action if the entered text is empty or if a completion
    /// window is already displayed.</remarks>
    /// <param name="sender">The source of the event, typically the text area control where text input occurred.</param>
    /// <param name="e">An object containing information about the text input event, including the entered text.</param>
    private void TextArea_TextEntered(object? sender, TextInputEventArgs e)
    {
        // If no text entered or completion window already open, do nothing
        if (string.IsNullOrEmpty(e.Text) || _completionWindow is not null)
        {
            return;
        }

        char typedChar = e.Text[0];

        // Check if we should open the window
        if (!ShouldTriggerCompletion(typedChar))
        {
            return;
        }

        ShowCompletionWindow();
    }

    /// <summary>
    /// Determines whether typing the specified character should trigger code completion in the editor.
    /// </summary>
    /// <remarks>Completion is triggered for certain immediate characters, such as '>' and space, or when a
    /// word threshold is met (e.g., after typing at least two valid identifier characters). The method does not trigger
    /// completion for non-trigger characters or when insufficient context is present.</remarks>
    /// <param name="typedChar">The character that was typed by the user.
    /// Must be a valid trigger character for completion to be considered.</param>
    /// <returns>true if code completion should be triggered for the specified character; otherwise, false.</returns>
    private bool ShouldTriggerCompletion(char typedChar)
    {
        // Fast fail guard (must go first)
        if (!IsTriggerChar(typedChar))
        {
            return false;
        }

        // Immediate Triggers: arrows, spaces
        if (typedChar == '>' || typedChar == ' ')
        {
            return true;
        }

        // Word Triggers (Threshold Logic) - require 2-char threshold to avoid noise
        // We know typedChar is Letter/Digit/_ because IsTriggerChar passed
        int offset = Editor.CaretOffset;
        if (offset < 2)
        {
            return false;
        }

        // Look at the char BEFORE the one we just typed (offset - 2)
        // Example: typed "b", text is "ab|". offset-1='b', offset-2='a'.
        // If previous char is also a valid identifier part, we have a "word" started.
        char prevChar = Editor.Document.GetCharAt(offset - 2);
        return char.IsLetterOrDigit(prevChar) || prevChar == '_';
    }

    /// <summary>
    /// Displays the code completion window at the current caret position in the editor, providing relevant completion
    /// suggestions based on the document content.
    /// </summary>
    /// <remarks>If the document is empty, the completion window is not shown. Exceptions encountered during
    /// the completion process are logged and do not interrupt the user experience.</remarks>
    private void ShowCompletionWindow()
    {
        HashSet<string> reusableSet = _nodeBufferPool.Get();
        try
        {
            // NOTE: Accessing .Text allocates a string. This is unavoidable with standard AvaloniaEdit API.
            string docText = Editor.Text ?? string.Empty;
            if (string.IsNullOrEmpty(docText))
            {
                return;
            }

            // Scan Document
            IntellisenseScanner scanner = new IntellisenseScanner(docText.AsSpan(), reusableSet, _stringInternPool);
            scanner.Scan();

            // Setup Window
            _completionWindow = new CompletionWindow(Editor.TextArea)
            {
                // Find the start of the word at the caret
                StartOffset = GetWordStartOffset(Editor.CaretOffset, docText)
            };

            IList<ICompletionData>? data = _completionWindow.CompletionList.CompletionData;
            PopulateCompletionData(data, reusableSet);

            _completionWindow.Show();
            _completionWindow.Closed += CompletionWindow_Closed;
        }
        catch (Exception ex)
        {
            // Log, but swallow exceptions to avoid crashing on completion errors
            _logger.LogDebug(ex, "Error showing completion window");
            Debug.Fail("Error showing completion window", ex.ToString());
        }
        finally
        {
            // Return reusable set to pool
            _nodeBufferPool.Return(reusableSet);
        }
    }

    /// <summary>
    /// Populates the specified completion data list with static keywords and wrappers for scanned node texts.
    /// </summary>
    /// <remarks>Static keywords are always added to the completion data. For each node text in <paramref
    /// name="scannedNodes"/>, a wrapper is added to the list; if a wrapper does not already exist in the cache, a new
    /// one is created and cached.</remarks>
    /// <param name="targetList">The list to which completion data items will be added. Must not be null.</param>
    /// <param name="scannedNodes">A set of node text strings to be included in the completion data.
    /// Each string represents a node to be wrapped and added.</param>
    [SuppressMessage("ReSharper", "SuggestBaseTypeForParameter", Justification = "AvaloniaEdit's CompletionList.CompletionData implements IList")]
    private void PopulateCompletionData(IList<ICompletionData> targetList, HashSet<string> scannedNodes)
    {
        // Create a temporary buffer for sorting
        // Size = Keywords + Scanned Nodes
        List<ICompletionData> tempList = new List<ICompletionData>(_staticKeywords.Length + scannedNodes.Count);

        // Add static keywords
        tempList.AddRange(_staticKeywords);


        // TODO de-duplicate scanned nodes against static keywords?

        // Add Scanned Nodes
        foreach (string nodeText in scannedNodes)
        {
            // Only allocate new wrapper if we've never seen this node before
            if (!_wrapperCache.TryGetValue(nodeText, out IntellisenseCompletionData? wrapper))
            {
                wrapper = new IntellisenseCompletionData(nodeText, 0, _abcIcon);
                _wrapperCache[nodeText] = wrapper;
            }
            tempList.Add(wrapper);
        }

        // Sort the combined list
        tempList.Sort(static (a, b) => string.Compare(a.Text, b.Text, StringComparison.OrdinalIgnoreCase));

        // Dump into the Window. Unfortunately CompletionData is not an implementation of List<T> that supports AddRange, so we have to iterate.
        foreach (ICompletionData item in tempList)
        {
            targetList.Add(item);
        }
    }

    /// <summary>
    /// Finds the zero-based index of the first character of the word that precedes or contains the specified caret
    /// position within the given text.
    /// </summary>
    /// <remarks>Word characters are considered to be letters, digits, or underscores. Separators such as
    /// spaces, brackets, and punctuation mark word boundaries.</remarks>
    /// <param name="caretOffset">The zero-based caret position in the text for which to locate
    /// the start of the word. Must be between 0 and the length of <paramref name="text"/>.</param>
    /// <param name="text">The text in which to search for the word start. Cannot be null.</param>
    /// <returns>The zero-based index of the first character of the word at or before the specified
    /// caret position. Returns 0 if the caret is at the start of the text or if no word characters precede the caret.
    /// </returns>
    private static int GetWordStartOffset(int caretOffset, string text)
    {
        int i = caretOffset - 1;
        while (i >= 0)
        {
            char c = text[i];

            // Stop if we hit a separator (space, symbols, etc.)
            if (!char.IsLetterOrDigit(c) && c != '_')
            {
                return i + 1; // The character AFTER the separator is the start
            }

            i--;
        }
        return 0; // Start of document
    }

    /// <summary>
    /// Handles the Closed event of the completion window, performing necessary cleanup when the window is closed.
    /// </summary>
    /// <param name="sender">The source of the event, typically the completion window that was closed.</param>
    /// <param name="e">An EventArgs instance containing event data.</param>
    private void CompletionWindow_Closed(object? sender, EventArgs e)
    {
        if (_completionWindow is not null)
        {
            _completionWindow.Closed -= CompletionWindow_Closed;
            _completionWindow = null;
        }
    }

    /// <summary>
    /// Determines whether the specified character is recognized as a trigger character for code completion.
    /// </summary>
    /// <param name="c">The character to evaluate as a potential trigger for completion.</param>
    /// <returns>true if the character is a completion trigger; otherwise, false.</returns>
    private static bool IsTriggerChar(char c) => c < LookupTableSize && _completionTriggerFlags[c];

    /// <summary>
    /// Creates a vector-based icon displaying the text "abc" in the VS Code purple color.
    /// </summary>
    /// <remarks>The icon uses the Segoe UI font in bold style and a small font size suitable for use as an
    /// icon. The color is chosen to provide good visibility in both light and dark themes.</remarks>
    /// <returns>A <see cref="DrawingImage"/> containing the formatted "abc" icon.</returns>
    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Actual name of font family")]
    private static DrawingImage CreateAbcIcon()
    {
        // Use VS Code Purple color which works in both ThemeModes (light and dark)
        SolidColorBrush vsCodePurpleBrush = SolidColorBrush.Parse("#B180D7");
        Typeface segoeUINormalBoldTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyle.Normal, FontWeight.Bold);

        // Create formatted vector text
        FormattedText formattedText = new FormattedText(
            "abc",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            segoeUINormalBoldTypeface,
            10,             // Font size (small, icon-sized)
            vsCodePurpleBrush
        );

        Geometry? geometry = formattedText.BuildGeometry(new Point(0, 0));
        GeometryDrawing drawing = new GeometryDrawing
        {
            Brush = vsCodePurpleBrush,
            Geometry = geometry
        };

        return new DrawingImage { Drawing = drawing };
    }

    /// <summary>
    /// Unsubscribes event handlers related to Intellisense functionality from the editor and completion window.
    /// </summary>
    /// <remarks>Call this method to detach Intellisense-related event handlers when the editor or completion
    /// window is no longer needed, such as during cleanup or disposal. This helps prevent memory leaks and unintended
    /// behavior from lingering event subscriptions.</remarks>
    private void UnsubscribeIntellisenseEventHandlers()
    {
        if (Editor is not null)
        {
            Editor.TextArea.TextEntered -= TextArea_TextEntered;
            Editor.TextArea.TextEntering -= TextArea_TextEntering;
        }

        if (_completionWindow is not null)
        {
            _completionWindow.Closed -= CompletionWindow_Closed;
            _completionWindow.Close();
            _completionWindow = null;
        }
    }
}
