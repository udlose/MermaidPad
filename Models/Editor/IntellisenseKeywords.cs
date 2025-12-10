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

using Avalonia.Media;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.Models.Editor;

/// <summary>
/// Provides keyword collections for Mermaid diagram intellisense and completion data.
/// </summary>
/// <remarks>
/// This class contains static keyword arrays for all supported Mermaid diagram types,
/// including frontmatter, theming, and diagram-specific keywords. The keywords are used
/// to populate intellisense completion lists in the editor.
/// Contains approximately 815+ keywords across 25 different diagram types and categories.
/// </remarks>
[SuppressMessage("Maintainability", "S1192:String literals should not be duplicated", Justification = "Easier to maintain as separate arrays for each diagram type - even if there are duplicates.")]
internal static class IntellisenseKeywords
{
    #region Keyword String Arrays

    #region Frontmatter keywords

    /// <summary>
    /// Contains keywords for YAML frontmatter configuration in Mermaid diagrams.
    /// </summary>
    /// <remarks>
    /// These keywords are used in the frontmatter section of Mermaid diagrams (delimited by ---).
    /// Includes configuration options, theme settings, and boolean values.
    /// </remarks>
    internal static readonly string[] FrontmatterKeywords =
    [
        "%%",
        "mermaid",
        "defaultRenderer",
        "title:",
        "config:",
        "layout:",
        "dagre",
        "elk",
        "theme:",
        "themeVariables:",
        "logLevel:",
        "securityLevel:",
        "startOnLoad:",
        "secure:",
        "primaryColor:",
        "signalColor:",
        "signalTextColor:",
        "true",
        "false",
        "base",
        "forest",
        "default",
        "dark",
        "neutral",
        "useWidth:",
        "useMaxWidth:"
    ];

    #endregion Frontmatter keywords

    #region Theming General

    /// <summary>
    /// Contains general theming configuration keywords for Mermaid diagrams.
    /// See: https://github.com/mermaid-js/mermaid/blob/develop/docs/config/theming.md?plain=1
    /// </summary>
    internal static readonly string[] ThemingGeneralKeywords =
    [
        "darkMode:",
        "background:",
        "fontFamily:",
        "fontSize:",
        "primaryColor:",
        "primaryTextColor:",
        "secondaryColor:",
        "primaryBorderColor:",
        "secondaryBorderColor:",
        "secondaryTextColor:",
        "tertiaryColor:",
        "tertiaryBorderColor:",
        "tertiaryTextColor:",
        "noteBkgColor:",
        "noteTextColor:",
        "noteBorderColor:",
        "lineColor:",
        "textColor:",
        "mainBkg:",
        "errorBkgColor:",
        "errorTextColor:"
    ];

    #endregion Theming General

    #region CSS Styles

    /// <summary>
    /// Contains CSS styling keywords applicable to Mermaid diagram elements.
    /// </summary>
    internal static readonly string[] CssStylesKeywords =
    [
        "backgroundColor:",
        "borderColor:",
        "borderWidth:",
        "color:",
        "fill:",
        "fontFamily:",
        "fontSize:",
        "fontWeight:",
        "stroke:",
        "stroke-dasharray:",
        "stroke-dashoffset:",
        "stroke-width:",
        "style",
        "textColor:",
        "cssStyle",
        "default"
    ];

    #endregion CSS Styles

    #region Architecture Diagram

    /// <summary>
    /// Contains keywords specific to Architecture diagrams (architecture-beta).
    /// See: https://mermaid.js.org/syntax/architecture.html
    /// </summary>
    internal static readonly string[] ArchitectureDiagramKeywords =
    [
        "architecture-beta",
        "cloud",
        "database",
        "disk",
        "edge",
        "group",
        "internet",
        "junction",
        "server",
        "service",
        "L",
        "R",
        "T",
        "B"
    ];

    #endregion Architecture Diagram

    #region Block Diagram

    /// <summary>
    /// Contains keywords specific to Block diagrams (block).
    /// See: https://mermaid.js.org/syntax/block.html
    /// </summary>
    internal static readonly string[] BlockDiagramKeywords =
    [
        "block",
        "columns",
        "end",
        "space"
    ];

    #endregion Block Diagram

    #region C4 Diagram Elements

    /// <summary>
    /// Contains keywords specific to C4 diagrams (Context, Container, Component, Dynamic, Deployment).
    /// See: https://mermaid.js.org/syntax/c4.html
    /// </summary>
    //TODO: this type is still experimental and may change. Revisit at some point to adjust as needed.
    internal static readonly string[] C4DiagramKeywords =
    [
        "C4Component",
        "C4Container",
        "C4Context",
        "C4Deployment",
        "C4Dynamic",
        // C4 Layout Elements
        "Lay_U",
        "Lay_Up",
        "Lay_D",
        "Lay_Down",
        "Lay_L",
        "Lay_Left",
        "Lay_R",
        "Lay_Right",
        // C4 Experimental Elements
        "sprite",
        "tags",
        "link",
        "Legend",
        // C4 System Context Diagram Elements
        "Person",
        "Person_Ext",
        "System",
        "SystemDb",
        "SystemQueue",
        "System_Ext",
        "SystemDb_Ext",
        "SystemQueue_Ext",
        "Boundary",
        "Enterprise_Boundary",
        "System_Boundary",
        // C4 Container Diagram Elements
        "Container",
        "ContainerDb",
        "ContainerQueue",
        "Container_Ext",
        "ContainerDb_Ext",
        "ContainerQueue_Ext",
        "Container_Boundary",
        // C4 Component Diagram Elements
        "Component",
        "ComponentDb",
        "ComponentQueue",
        "Component_Ext",
        "ComponentDb_Ext",
        "ComponentQueue_Ext",
        // C4 Dynamic Diagram Elements
        "RelIndex",
        // C4 Deployment Diagram Elements
        "Deployment_Node",
        "Node",
        "Node_L",
        "Node_R",
        // C4 Relationship Types
        "Rel",
        "BiRel",
        "Rel_U",
        "Rel_Up",
        "Rel_D",
        "Rel_Down",
        "Rel_L",
        "Rel_Left",
        "Rel_R",
        "Rel_Right",
        "Rel_Back",
        "Rel_Index",
        // C4 Custom Tags
        "AddElementTag",
        "AddRelTag",
        "UpdateElementStyle",
        "UpdateRelStyle",
        "RoundedBoxShape",
        "EightSidedShape",
        "DashedLine",
        "DottedLine",
        "BoldLine",
        "UpdateLayoutConfig",
    ];

    #endregion C4 Diagram Elements

    #region Class Diagram

    /// <summary>
    /// Contains keywords specific to Class diagrams (classDiagram, classDiagram-v2).
    /// See: https://mermaid.js.org/syntax/classDiagram.html
    /// </summary>
    internal static readonly string[] ClassDiagramKeywords =
    [
        "classDiagram",
        "classDiagram-v2",
        "class",
        "classDef",
        "interface",
        "namespace",
        "bool",
        "double",
        "float",
        "int",
        "long",
        "string",
        "<<interface>>",
        "<<abstract>>",
        "<<service>>",
        "<<enumeration>>",
        "link",
        "call",
        "callback",
        "direction",
        "note",
        "href",
        "classText:",
    ];

    #endregion Class Diagram

    #region ER Diagram

    /// <summary>
    /// Contains keywords specific to Entity Relationship (ER) diagrams.
    /// See: https://mermaid.js.org/syntax/entityRelationshipDiagram.html
    /// </summary>
    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Following Mermaid naming convention for clarity.")]
    internal static readonly string[] ERDiagramKeywords =
    [
        "erDiagram",
        "allows",
        "has",
        "TB",
        "BT",
        "RL",
        "LR",
        "|o",
        "||",
        "}o",
        "}|",
        "o|",
        "o{",
        "|{",
    ];

    #endregion ER Diagram

    #region Flowchart

    /// <summary>
    /// Contains keywords specific to Flowchart/Graph diagrams, including all shape types.
    /// See: https://mermaid.js.org/syntax/flowchart.html
    /// </summary>
    internal static readonly string[] FlowchartKeywords =
    [
        "graph",
        "flowchart",
        "flowchart-elk",
        "subgraph",
        "end",
        "style",
        "class",
        "classDef",
        "click",
        "linkStyle",
        "end",
        "TB",
        "TD",
        "BT",
        "RL",
        "LR",
        "img:",
        "label:",
        "pos:",
        "constraint:",
        "animate:",
        "animation:",
        "markdownAutoWrap",
        "_blank",
        "icon:",
        "form:",
        "square",
        "circle",
        "rounded",
        // Arrows: https://mermaid.js.org/syntax/flowchart.html#new-arrow-types
        "--o",
        "-->",
        "--x",
        "---",
        "===",
        "==>",
        "-.-",
        "-.->",
        // Multi-directional arrows: https://mermaid.js.org/syntax/flowchart.html#multi-directional-arrows
        "o--o",
        "<-->",
        "x--x",
        // Stying line curves: https://mermaid.js.org/syntax/flowchart.html#styling-line-curves
        "curve",
        "basis",
        "bumpX",
        "bumpY",
        "cardinal",
        "catmullRom",
        "linear",
        "monotoneX",
        "monotoneY",
        "natural",
        "step",
        "stepAfter",
        "stepBefore",
        // Shapes: https://mermaid.js.org/syntax/flowchart.html#complete-list-of-new-shapes
        "bang",
        "bolt",
        "bow-rect",
        "bow-tie-rectangle",
        "brace",
        "brace-l",
        "brace-r",
        "braces",
        "card",
        "circ",
        "circle",
        "cloud",
        "collate",
        "com-link",
        "comment",
        "cross-circ",
        "crossed-circle",
        "curv-trap",
        "curved-trapezoid",
        "cyl",
        "cylinder",
        "das",
        "database",
        "db",
        "dbl-circ",
        "decision",
        "delay",
        "diam",
        "diamond",
        "disk",
        "display",
        "div-proc",
        "div-rect",
        "divided-process",
        "divided-rectangle",
        "doc",
        "docs",
        "document",
        "documents",
        "double-circle",
        "event",
        "extract",
        "f-circ",
        "filled-circle",
        "flag",
        "flip-tri",
        "flipped-triangle",
        "fork",
        "fr-circ",
        "fr-rect",
        "framed-circle",
        "framed-rectangle",
        "h-cyl",
        "half-rounded-rectangle",
        "hex",
        "hexagon",
        "horizontal-cylinder",
        "hourglass",
        "in-out",
        "internal-storage",
        "inv-trapezoid",
        "join",
        "junction",
        "lean-l",
        "lean-left",
        "lean-r",
        "lean-right",
        "lightning-bolt",
        "lin-cyl",
        "lin-doc",
        "lin-proc",
        "lin-rect",
        "lined-cylinder",
        "lined-document",
        "lined-process",
        "lined-rectangle",
        "loop-limit",
        "manual",
        "manual-file",
        "manual-input",
        "notch-pent",
        "notch-rect",
        "notched-pentagon",
        "notched-rectangle",
        "odd",
        "out-in",
        "paper-tape",
        "pill",
        "prepare",
        "priority",
        "proc",
        "process",
        "processes",
        "procs",
        "question",
        "rect",
        "rectangle",
        "rounded",
        "shaded-process",
        "sl-rect",
        "sloped-rectangle",
        "sm-circ",
        "small-circle",
        "st-doc",
        "st-rect",
        "stacked-document",
        "stacked-rectangle",
        "stadium",
        "start",
        "stop",
        "stored-data",
        "subproc",
        "subprocess",
        "subroutine",
        "summary",
        "tag-doc",
        "tag-proc",
        "tag-rect",
        "tagged-document",
        "tagged-process",
        "tagged-rectangle",
        "terminal",
        "text",
        "trap-b",
        "trap-t",
        "trapezoid",
        "trapezoid-bottom",
        "trapezoid-top",
        "tri",
        "triangle",
        "win-pane",
        "window-pane",
        // Flowchart Theming: https://github.com/mermaid-js/mermaid/blob/develop/docs/config/theming.md#flowchart-variables
        "nodeBorder:",
        "clusterBkg:",
        "clusterBorder:",
        "defaultLinkColor:",
        "titleColor:",
        "edgeLabelBackground:",
        "nodeTextColor:",
    ];

    #endregion Flowchart

    #region Gantt

    /// <summary>
    /// Contains keywords specific to Gantt charts.
    /// See: https://mermaid.js.org/syntax/gantt.html
    /// </summary>
    internal static readonly string[] GanttChartKeywords =
    [
        "gantt",
        "dateFormat",
        "axisFormat",
        "title",
        "section",
        "excludes",
        "vert",
        "monday",
        "tuesday",
        "wednesday",
        "thursday",
        "friday",
        "saturday",
        "sunday",
        "weekend",
        "weekday",
        "active",
        "done",
        "crit",
        "milestone",
        "after",
        "until",
        "isadded",
        "tickInterval",
        "millisecond",
        "second",
        "minute",
        "hour",
        "day",
        "week",
        "month",
        "year",
        "todayMarker",
        "titleTopMargin:",
        "barHeight:",
        "barGap:",
        "topPadding:",
        "rightPadding:",
        "leftPadding:",
        "gridLineStartPadding:",
        "sectionFontSize:",
        "numberSectionStyles:",
        "topAxis:",
        "displayMode:",
        "compact",
        "mirrorActor:",
        "bottomMarginAdj:",
    ];

    #endregion Gantt

    #region GitGraph

    /// <summary>
    /// Contains keywords specific to Git Graph diagrams.
    /// See: https://mermaid.js.org/syntax/gitgraph.html
    /// </summary>
    internal static readonly string[] GitGraphKeywords =
    [
        "gitGraph",
        "commit",
        "branch",
        "merge",
        "checkout",
        "cherry-pick",
        "reset",
        "switch",
        "main",
        "develop",
        "release",
        "NORMAL",
        "REVERSE",
        "HIGHLIGHT",
        "id:",
        "type:",
        "tag:",
        "parent:",
        "showBranches:",
        "showCommitLabel:",
        "mainBranchName:",
        "mainBranchOrder:",
        "parallelCommits:",
        "rotateCommitLabel:",
        "order:",
        "LR:",
        "TB:",
        "BT:",
        "git0",
        "git1",
        "git2",
        "git3",
        "git4",
        "git5",
        "git6",
        "git7",
        "gitBranchLabel0",
        "gitBranchLabel1",
        "gitBranchLabel2",
        "gitBranchLabel3",
        "gitBranchLabel4",
        "gitBranchLabel5",
        "gitBranchLabel6",
        "gitBranchLabel7",
        "gitInv0",
        "gitInv1",
        "gitInv2",
        "gitInv3",
        "gitInv4",
        "gitInv5",
        "gitInv6",
        "gitInv7",
        "commitLabelColor",
        "commitLabelBackground",
        "commitLabelFontSize",
        "tagLabelFontSize",
        "tagLabelColor",
        "tagLabelBackground",
        "tagLabelBorder",
    ];

    #endregion GitGraph

    #region Kanban

    /// <summary>
    /// Contains keywords specific to Kanban diagrams.
    /// See: https://mermaid.js.org/syntax/kanban.html
    /// </summary>
    internal static readonly string[] KanbanKeywords =
    [
        "kanban",
        "todo",
        "assigned:",
        "ticket:",
        "priority:",
        "ticketBaseUrl:"
    ];

    #endregion Kanban

    #region Mindmap

    /// <summary>
    /// Contains keywords specific to Mindmap diagrams.
    /// See: https://mermaid.js.org/syntax/mindmap.html
    /// </summary>
    internal static readonly string[] MindmapKeywords =
    [
        "mindmap",
        "mindmap-v2",
        "root",
        "tidy-tree"
    ];

    #endregion Mindmap

    #region Packet

    /// <summary>
    /// Contains keywords specific to Packet diagrams.
    /// See: https://mermaid.js.org/syntax/packet.html
    /// </summary>
    internal static readonly string[] PacketKeywords =
    [
        "packet",
        "title",
        "rowHeight:",
        "bitWidth:",
        "bitsPerRow:",
        "showBits:",
        "paddingX:",
        "paddingY:"
    ];

    #endregion Packet

    #region Pie Charts

    /// <summary>
    /// Contains keywords specific to Pie charts.
    /// See: https://mermaid.js.org/syntax/pie.html
    /// </summary>
    internal static readonly string[] PieChartKeywords =
    [
        "pie",
        "showData",
        "title",
        "textPosition:",
        // Pie Chart Theming: https://github.com/mermaid-js/mermaid/blob/develop/docs/config/theming.md#pie-diagram-variables
        "pieTitleTextSize:",
        "pieTitleTextColor:",
        "pieSectionTextSize:",
        "pieSectionTextColor:",
        "pieLegendTextSize:",
        "pieLegendTextColor:",
        "pieStrokeColor:",
        "pieStrokeWidth:",
        "pieOuterStrokeWidth:",
        "pieOuterStrokeColor:",
        "pieOpacity:",
    ];

    #endregion Pie Charts

    #region Quadrant Diagrams

    /// <summary>
    /// Contains keywords specific to Quadrant Chart diagrams.
    /// See: https://mermaid.js.org/syntax/quadrantChart.html
    /// </summary>
    internal static readonly string[] QuadrantChartKeywords =
    [
        "quadrantChart",
        "title",
        "x-axis",
        "y-axis",
        "quadrant-1",
        "quadrant-2",
        "quadrant-3",
        "quadrant-4",
        // Quadrant Theming: https://mermaid.js.org/syntax/quadrantChart.html#chart-configurations
        "chartWidth:",
        "chartHeight:",
        "titlePadding:",
        "titleFontSize:",
        "quadrantPadding:",
        "quadrantTextTopPadding:",
        "quadrantLabelFontSize:",
        "quadrantInternalBorderStrokeWidth:",
        "quadrantExternalBorderStrokeWidth:",
        "xAxisLabelPadding:",
        "xAxisLabelFontSize:",
        "xAxisPosition:",
        "yAxisLabelPadding:",
        "yAxisLabelFontSize:",
        "yAxisPosition:",
        "pointTextPadding:",
        "pointLabelFontSize:",
        "pointRadius:",
        "quadrant1Fill:",
        "quadrant2Fill:",
        "quadrant3Fill:",
        "quadrant4Fill:",
        "quadrant1TextFill:",
        "quadrant2TextFill:",
        "quadrant3TextFill:",
        "quadrant4TextFill:",
        "quadrantPointFill:",
        "quadrantPointTextFill:",
        "quadrantXAxisTextFill:",
        "quadrantYAxisTextFill:",
        "quadrantInternalBorderStrokeFill:",
        "quadrantExternalBorderStrokeFill:",
        "quadrantTitleFill:",
        "color:",
        "radius:",
        "stroke-width:",
        "stroke-color:",
    ];

    #endregion Quadrant Diagrams

    #region Radar Charts

    /// <summary>
    /// Contains keywords specific to Radar charts (radar-beta).
    /// See: https://mermaid.js.org/syntax/radar.html
    /// </summary>
    internal static readonly string[] RadarChartKeywords =
    [
        "radar-beta",
        "title",
        "axis",
        "curve",
        "showLegend",
        "max",
        "min",
        "graticule",
        "circle",
        "polygon",
        "ticks",
        // Radar Chart Theming: https://mermaid.js.org/syntax/radar.html#configuration
        "width:",
        "height:",
        "marginTop:",
        "marginBottom:",
        "marginLeft:",
        "marginRight:",
        "axisScaleFactor:",
        "axisLabelFactor:",
        "curveTension:",
        // Radar Chart Global Theming: https://mermaid.js.org/syntax/radar.html#global-theme-variables
        "fontSize:",
        "titleColor:",
        "cScale",
        "axisColor:",
        "axisStrokeWidth:",
        "axisLabelFontSize:",
        "curveOpacity:",
        "curveStrokeWidth:",
        "graticuleColor:",
        "graticuleOpacity:",
        "graticuleStrokeWidth:",
        "legendBoxSize:",
        "legendFontSize:",
    ];

    #endregion Radar Charts

    #region Requirement Diagram

    /// <summary>
    /// Contains keywords specific to Requirement diagrams.
    /// See: https://mermaid.js.org/syntax/requirementDiagram.html
    /// </summary>
    internal static readonly string[] RequirementDiagramKeywords =
    [
        "requirementDiagram",
        "requirement",
        "element",
        "functionalRequirement",
        "interfaceRequirement",
        "performanceRequirement",
        "physicalRequirement",
        "designConstraint",
        "id:",
        "risk:",
        "text:",
        "type:",
        "verifymethod:",
        "docref:",
        "analysis",
        "inspection",
        "test",
        "demonstration",
        "direction",
        "TB",
        "BT",
        "LR",
        "RL",
        "simulation",
        "style",
        "class",
        "classDef",
    ];

    #endregion Requirement Diagram

    #region Sankey

    /// <summary>
    /// Contains keywords specific to Sankey diagrams (sankey).
    /// See: https://mermaid.js.org/syntax/sankey.html
    /// </summary>
    internal static readonly string[] SankeyKeywords =
    [
        "sankey",
        "source",
        "target",
        "value",
        "width:",
        "height:",
        "linkColor:",
        "nodeAlignment:",
        "gradient",
        "justify",
        "center",
        "left",
        "right"
    ];

    #endregion Sankey

    #region Sequence Diagram

    /// <summary>
    /// Contains keywords specific to Sequence diagrams.
    /// See: https://mermaid.js.org/syntax/sequenceDiagram.html
    /// </summary>
    internal static readonly string[] SequenceDiagramKeywords =
    [
        "sequenceDiagram",
        "participant",
        "actor",
        "boundary",
        "control",
        "entity",
        "database",
        "collections",
        "queue",
        "as",
        "create",
        "destroy",
        "activate",
        "deactivate",
        "note",
        "<br/>",
        // Arrows: https://mermaid.js.org/syntax/sequenceDiagram.html#messages
        "->",
        "-->",
        "->>",
        "-->>",
        "<<->>",
        "<<-->>",
        "-x",
        "--x",
        "-)",
        "--)",
        "box",
        "loop",
        "end",
        "alt",
        "else",
        "opt",
        "par",
        "and",
        "critical",
        "break",
        "rect",
        "rgb",
        "rgba",
        "autonumber",
        "link",
        "links",
        // Styling: https://mermaid.js.org/syntax/sequenceDiagram.html#classes-used
        "actor",
        "actor-top",
        "actor-bottom",
        "text.actor",
        "text.actor-box",
        "text.actor-man",
        "actor-line",
        "messageLine0",
        "messageLine1",
        "messageText",
        "labelBox",
        "labelText",
        "loopText",
        "loopLine",
        "note",
        "noteText",
        // Configuration: https://mermaid.js.org/syntax/sequenceDiagram.html#configuration
        "diagramMarginX:",
        "diagramMarginY:",
        "boxTextMargin:",
        "noteMargin:",
        "messageMargin:",
        // https://mermaid.js.org/syntax/sequenceDiagram.html#possible-configuration-parameters
        "mirrorActors:",
        "bottomMarginAdj:",
        "actorFontSize:",
        "actorFontFamily:",
        "actorFontWeight:",
        "noteFontSize:",
        "noteFontFamily:",
        "noteFontWeight:",
        "noteAlign:",
        "messageFontSize:",
        "messageFontFamily:",
        "messageFontWeight:",
        // Sequence Diagram Theming: https://github.com/mermaid-js/mermaid/blob/develop/docs/config/theming.md#sequence-diagram-variables
        "actorBkg:",
        "actorBorder:",
        "actorTextColor:",
        "actorLineColor:",
        "signalColor:",
        "signalTextColor:",
        "labelBoxBkgColor:",
        "labelBoxBorderColor:",
        "labelTextColor:",
        "loopTextColor:",
        "activationBorderColor:",
        "activationBkgColor:",
        "sequenceNumberColor:",
    ];

    #endregion Sequence Diagram

    #region State Diagrams

    /// <summary>
    /// Contains keywords specific to State diagrams (stateDiagram, stateDiagram-v2).
    /// See: https://mermaid.js.org/syntax/stateDiagram.html
    /// </summary>
    internal static readonly string[] StateDiagramKeywords =
    [
        "stateDiagram",
        "stateDiagram-v2",
        "state",
        "as",
        "note",
        "end note",
        "[*]",
        "--",
        "-->",
        ":::",
        "<<choice>>",
        "<<fork>>",
        "<<join>>",
        "direction",
        "class",
        "classDef",
        "TB",
        "BT",
        "RL",
        "LR",
        // State Diagram Theming: https://github.com/mermaid-js/mermaid/blob/develop/docs/config/theming.md#state-colors
        "labelColor:",
        "altBackground:",
    ];

    #endregion State Diagrams

    #region Timeline

    /// <summary>
    /// Contains keywords specific to Timeline diagrams.
    /// See: https://mermaid.js.org/syntax/timeline.html
    /// </summary>
    internal static readonly string[] TimelineKeywords =
    [
        "timeline",
        "title",
        "section",
        "<br/>",
        "disableMultiColor:",
        "cScale",
        "cScaleLabel"
    ];

    #endregion Timeline

    #region Treemap

    /// <summary>
    /// Contains keywords specific to Treemap diagrams (treemap).
    /// See: https://mermaid.js.org/syntax/treemap.html
    /// </summary>
    internal static readonly string[] TreemapKeywords =
    [
        "treemap",
        "title",
        "classDef",
        // Treemap Theming: https://mermaid.js.org/syntax/treemap.html#diagram-padding
        "diagramPadding:",
        // https://mermaid.js.org/syntax/treemap.html#configuration-options
        "useMaxWidth:",
        "padding:",
        "showValues:",
        "nodeWidth:",
        "nodeHeight:",
        "borderWidth:",
        "valueFontSize:",
        "labelFontSize:",
        "valueFormat:"
    ];

    #endregion Treemap

    #region UserJourney

    /// <summary>
    /// Contains keywords specific to User Journey diagrams.
    /// See: https://mermaid.js.org/syntax/userJourney.html
    /// </summary>
    internal static readonly string[] UserJourneyKeywords =
    [
        "journey",
        "section",
        "title",
        // Journey Theming: https://github.com/mermaid-js/mermaid/blob/develop/docs/config/theming.md#user-journey-colors
        "fillType0:",
        "fillType1:",
        "fillType2:",
        "fillType3:",
        "fillType4:",
        "fillType5:",
        "fillType6:",
        "fillType7:"
    ];

    #endregion UserJourney

    #region XY Chart

    /// <summary>
    /// Contains keywords specific to XY Chart diagrams (xychart).
    /// See: https://mermaid.js.org/syntax/xyChart.html
    /// </summary>
    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Following Mermaid naming convention for clarity.")]
    internal static readonly string[] XYChartKeywords =
    [
        "xychart",
        "title",
        "x-axis",
        "y-axis",
        "series",
        "type",
        "line",
        "bar",
        "horizontal",
        "vertical",
        "max",
        "min",
        "-->",
        // XY Chart Config: https://mermaid.js.org/syntax/xyChart.html#chart-configurations
        "width:",
        "height:",
        "titlePadding:",
        "titleFontSize:",
        "showTitle:",
        "xAxis:",
        "yAxis:",
        "chartOrientation:",
        "plotReservedSpacePercent:",
        "showDataLabel:",
        // XY Chart AxisConfig: https://mermaid.js.org/syntax/xyChart.html#axisconfig
        "showLabel:",
        "labelFontSize:",
        "labelPadding:",
        "showTitle:",
        "titleFontSize:",
        "titlePadding:",
        "showTick:",
        "tickLength:",
        "tickWidth:",
        "showAxisLine:",
        "axisLineWidth:",
        // Chart Theme Variables: https://mermaid.js.org/syntax/xyChart.html#chart-theme-variables
        "backgroundColor:",
        "titleColor:",
        "xAxisLabelColor:",
        "xAxisTitleColor:",
        "xAxisTickColor:",
        "xAxisLineColor:",
        "yAxisLabelColor:",
        "yAxisTitleColor:",
        "yAxisTickColor:",
        "yAxisLineColor:",
        "plotColorPalette:",
    ];

    #endregion XY Chart

    /// <summary>
    /// Contains all keyword source arrays used for chart and diagram parsing.
    /// </summary>
    /// <remarks>Each inner array represents the set of keywords associated with a specific chart or diagram
    /// type. This field is intended for internal use to facilitate keyword lookups and categorization.</remarks>
    private static readonly string[][] _allSourceArrays =
    [
        FrontmatterKeywords,
        ThemingGeneralKeywords,
        CssStylesKeywords,
        ArchitectureDiagramKeywords,
        BlockDiagramKeywords,
        C4DiagramKeywords,
        ClassDiagramKeywords,
        ERDiagramKeywords,
        FlowchartKeywords,
        GanttChartKeywords,
        GitGraphKeywords,
        KanbanKeywords,
        MindmapKeywords,
        PacketKeywords,
        PieChartKeywords,
        QuadrantChartKeywords,
        RadarChartKeywords,
        RequirementDiagramKeywords,
        SankeyKeywords,
        SequenceDiagramKeywords,
        StateDiagramKeywords,
        TimelineKeywords,
        TreemapKeywords,
        UserJourneyKeywords,
        XYChartKeywords
    ];

    /// <summary>
    /// Provides a collection of all distinct keywords aggregated from the source arrays.
    /// </summary>
    /// <remarks>
    /// <para>This array contains only unique keyword values, preserving no particular order. The
    /// aggregation avoids repeated resizing and copying by flattening and deduplicating the source arrays
    /// efficiently.</para>
    /// <para>This SelectMany approach avoids the costly <see cref="List{T}"/> resizing and copying
    /// associated with calling <see cref="List{T}.AddRange"/> multiple times.</para>
    /// </remarks>
    internal static readonly string[] AggregatedDistinctKeywords = _allSourceArrays
        // No need to sort here; we sort later in PopulateCompletionData
        .SelectMany(static arr => arr)
        .Distinct()     // Avoid duplicates like "database", "section"
        .ToArray();     // Using array for better memory locality and keeping this data immutable

    #endregion Keyword String Arrays

    /// <summary>
    /// Creates an array of <see cref="IntellisenseCompletionData"/> objects from the specified keywords, assigning each a priority
    /// and optional icon.
    /// </summary>
    /// <param name="keywords">An array of keyword strings to be used for creating completion data. Cannot be null or empty.</param>
    /// <param name="priority">The priority value to assign to each completion data item. Defaults to 0.</param>
    /// <param name="icon">An optional icon to associate with each completion data item. If null, no icon is assigned.</param>
    /// <returns>An array of IntellisenseCompletionData objects corresponding to the provided keywords. Returns an empty array if
    /// no keywords are specified.</returns>
    internal static IntellisenseCompletionData[] CreateCompletionData(string[] keywords, int priority = 0, DrawingImage? icon = null)
    {
        if (keywords.Length == 0)
        {
            return Array.Empty<IntellisenseCompletionData>();
        }

        IntellisenseCompletionData[] result = new IntellisenseCompletionData[keywords.Length];
        for (int i = 0; i < keywords.Length; i++)
        {
            result[i] = new IntellisenseCompletionData(keywords[i], priority, icon);
        }
        return result;
    }

    /// <summary>
    /// Retrieves an array of completion data containing all aggregated distinct keywords available for IntelliSense
    /// suggestions.
    /// </summary>
    /// <returns>An array of <see cref="IntellisenseCompletionData"/> objects representing the distinct keywords. The array will
    /// be empty if no keywords are available.</returns>
    internal static IntellisenseCompletionData[] GetAggregatedDistinctKeywords() =>
        CreateCompletionData(AggregatedDistinctKeywords, priority: 0);
}
