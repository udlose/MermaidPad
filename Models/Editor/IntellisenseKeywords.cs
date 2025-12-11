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

using Avalonia.Media.Imaging;
using MermaidPad.Models.Constants;
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
        Frontmatter.Comment,
        Frontmatter.Mermaid,
        Frontmatter.DefaultRenderer,
        Frontmatter.Title,
        Frontmatter.Config,
        Frontmatter.Layout,
        Frontmatter.Dagre,
        Frontmatter.Elk,
        Frontmatter.Theme,
        Frontmatter.ThemeVariables,
        Frontmatter.LogLevel,
        Frontmatter.SecurityLevel,
        Frontmatter.StartOnLoad,
        Frontmatter.Secure,
        Frontmatter.PrimaryColor,
        Frontmatter.SignalColor,
        Frontmatter.SignalTextColor,
        Frontmatter.True,
        Frontmatter.False,
        Frontmatter.Base,
        Frontmatter.Forest,
        GeneralElementNames.Default,
        Frontmatter.Dark,
        Frontmatter.Neutral,
        Frontmatter.UseWidth,
        Frontmatter.UseMaxWidth
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
        CssStyles.FontFamily,
        CssStyles.FontSize,
        Frontmatter.PrimaryColor,
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
        CssStyles.TextColor,
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
        CssStyles.CssStyle,
        CssStyles.BackgroundColor,
        CssStyles.BorderColor,
        CssStyles.BorderWidth,
        CssStyles.Color,
        CssStyles.Fill,
        CssStyles.FontFamily,
        CssStyles.FontSize,
        CssStyles.FontWeight,
        CssStyles.Stroke,
        CssStyles.StrokeDasharray,
        CssStyles.StrokeDashoffset,
        CssStyles.StrokeWidth,
        GeneralElementNames.Style,
        CssStyles.TextColor,
        GeneralElementNames.Default
    ];

    #endregion CSS Styles

    #region Architecture Diagram

    /// <summary>
    /// Contains keywords specific to Architecture diagrams (architecture-beta).
    /// See: https://mermaid.js.org/syntax/architecture.html
    /// </summary>
    internal static readonly string[] ArchitectureDiagramKeywords =
    [
        DiagramTypeNames.ArchitectureBeta,
        ArchitectureDiagram.ElementNames.Cloud,
        ArchitectureDiagram.ElementNames.Database,
        ArchitectureDiagram.ElementNames.Disk,
        ArchitectureDiagram.ElementNames.Edge,
        ArchitectureDiagram.ElementNames.Group,
        ArchitectureDiagram.ElementNames.Internet,
        ArchitectureDiagram.ElementNames.Junction,
        ArchitectureDiagram.ElementNames.Server,
        ArchitectureDiagram.ElementNames.Service,
        "in",
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
        DiagramTypeNames.Block,
        "columns",
        GeneralElementNames.End,
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
        DiagramTypeNames.C4Component,
        DiagramTypeNames.C4Container,
        DiagramTypeNames.C4Context,
        DiagramTypeNames.C4Deployment,
        DiagramTypeNames.C4Dynamic,
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
        GeneralElementNames.Link,
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
        C4Diagram.BoundaryTypes.Boundary,
        C4Diagram.BoundaryTypes.Enterprise,
        C4Diagram.BoundaryTypes.System,
        // C4 Container Diagram Elements
        "Container",
        "ContainerDb",
        "ContainerQueue",
        "Container_Ext",
        "ContainerDb_Ext",
        "ContainerQueue_Ext",
        C4Diagram.BoundaryTypes.Container,
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
        DiagramTypeNames.ClassDiagram,
        DiagramTypeNames.ClassDiagramV2,
        GeneralElementNames.Class,
        GeneralElementNames.ClassDef,
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
        GeneralElementNames.Link,
        "call",
        "callback",
        GeneralElementNames.Direction,
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
        DiagramTypeNames.ERDiagram,
        "allows",
        "has",
        DirectionNames.TopToBottom,
        DirectionNames.BottomToTop,
        DirectionNames.RightToLeft,
        DirectionNames.LeftToRight,
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
        DiagramTypeNames.Graph,
        DiagramTypeNames.Flowchart,
        DiagramTypeNames.FlowchartElk,
        FlowchartDiagram.BlockOpenerNames.Subgraph,
        GeneralElementNames.End,
        GeneralElementNames.Style,
        GeneralElementNames.Class,
        GeneralElementNames.ClassDef,
        "click",
        "linkStyle",
        GeneralElementNames.End,
        DirectionNames.TopToBottom,
        DirectionNames.TopToBottomTD,
        DirectionNames.BottomToTop,
        DirectionNames.RightToLeft,
        DirectionNames.LeftToRight,
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
        ShapeNames.Square,
        ShapeNames.Circle,
        ShapeNames.Rounded,
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
        ShapeNames.Curve,
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
        ShapeNames.Circ,
        ShapeNames.Circle,
        GeneralElementNames.Cloud,
        "collate",
        "com-link",
        "comment",
        "cross-circ",
        "crossed-circle",
        "curv-trap",
        "curved-trapezoid",
        ShapeNames.Cyl,
        ShapeNames.Cylinder,
        "das",
        GeneralElementNames.Database,
        "db",
        "dbl-circ",
        "decision",
        "delay",
        "diam",
        ShapeNames.Diamond,
        ShapeNames.Disk,
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
        ShapeNames.Hex,
        ShapeNames.Hexagon,
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
        ShapeNames.Rect,
        ShapeNames.Rectangle,
        ShapeNames.Rounded,
        "shaded-process",
        "sl-rect",
        "sloped-rectangle",
        "sm-circ",
        "small-circle",
        "st-doc",
        "st-rect",
        "stacked-document",
        "stacked-rectangle",
        ShapeNames.Stadium,
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
        CssStyles.TitleColor,
        "edgeLabelBackground:",
        "nodeTextColor:",
        // Flowchart Config: https://mermaid.js.org/config/schema-docs/config-defs-flowchart-diagram-config.html
        FlowchartDiagram.Config.TitleTopMargin,
        FlowchartDiagram.Config.SubGraphTitleMargin,
        FlowchartDiagram.Config.ArrowMarkerAbsolute,
        FlowchartDiagram.Config.DiagramPadding,
        FlowchartDiagram.Config.HtmlLabels,
        FlowchartDiagram.Config.NodeSpacing,
        FlowchartDiagram.Config.RankSpacing,
        FlowchartDiagram.Config.Curve,
        FlowchartDiagram.Config.Padding,
        FlowchartDiagram.Config.DefaultRenderer,
        FlowchartDiagram.Config.WrappingWidth,
        FlowchartDiagram.Config.InheritDir
    ];

    #endregion Flowchart

    #region Gantt

    /// <summary>
    /// Contains keywords specific to Gantt charts.
    /// See: https://mermaid.js.org/syntax/gantt.html
    /// </summary>
    internal static readonly string[] GanttChartKeywords =
    [
        DiagramTypeNames.Gantt,
        "dateFormat",
        "axisFormat",
        GeneralElementNames.Title,
        GeneralElementNames.Section,
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
        DiagramTypeNames.GitGraph,
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
        DiagramTypeNames.Kanban,
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
        DiagramTypeNames.Mindmap,
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
        DiagramTypeNames.Packet,
        GeneralElementNames.Title,
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
        DiagramTypeNames.Pie,
        "showData",
        GeneralElementNames.Title,
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
        DiagramTypeNames.QuadrantChart,
        GeneralElementNames.Title,
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
        CssStyles.Color,
        CssStyles.Radius,
        CssStyles.StrokeWidth,
        CssStyles.StrokeColor,
    ];

    #endregion Quadrant Diagrams

    #region Radar Charts

    /// <summary>
    /// Contains keywords specific to Radar charts (radar-beta).
    /// See: https://mermaid.js.org/syntax/radar.html
    /// </summary>
    internal static readonly string[] RadarChartKeywords =
    [
        DiagramTypeNames.RadarBeta,
        GeneralElementNames.Title,
        "axis",
        ShapeNames.Curve,
        "showLegend",
        "max",
        "min",
        "graticule",
        ShapeNames.Circle,
        ShapeNames.Polygon,
        "ticks",
        // Radar Chart Theming: https://mermaid.js.org/syntax/radar.html#configuration
        CssStyles.Width,
        CssStyles.Height,
        CssStyles.MarginTop,
        CssStyles.MarginBottom,
        CssStyles.MarginLeft,
        CssStyles.MarginRight,
        "axisScaleFactor:",
        "axisLabelFactor:",
        "curveTension:",
        // Radar Chart Global Theming: https://mermaid.js.org/syntax/radar.html#global-theme-variables
        CssStyles.FontSize,
        CssStyles.TitleColor,
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
        DiagramTypeNames.RequirementDiagram,
        RequirementDiagram.BlockTypes.Requirement,
        RequirementDiagram.BlockTypes.Element,
        RequirementDiagram.BlockTypes.FunctionalRequirement,
        RequirementDiagram.BlockTypes.InterfaceRequirement,
        RequirementDiagram.BlockTypes.PerformanceRequirement,
        RequirementDiagram.BlockTypes.PhysicalRequirement,
        RequirementDiagram.BlockTypes.DesignConstraint,
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
        GeneralElementNames.Direction,
        DirectionNames.TopToBottom,
        DirectionNames.BottomToTop,
        DirectionNames.LeftToRight,
        DirectionNames.RightToLeft,
        "simulation",
        GeneralElementNames.Style,
        GeneralElementNames.Class,
        GeneralElementNames.ClassDef,
    ];

    #endregion Requirement Diagram

    #region Sankey

    /// <summary>
    /// Contains keywords specific to Sankey diagrams (sankey).
    /// See: https://mermaid.js.org/syntax/sankey.html
    /// </summary>
    internal static readonly string[] SankeyKeywords =
    [
        DiagramTypeNames.Sankey,
        "source",
        "target",
        "value",
        CssStyles.Width,
        CssStyles.Height,
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
        DiagramTypeNames.SequenceDiagram,
        SequenceDiagram.ParticipantTypes.Participant,
        SequenceDiagram.ParticipantTypes.Actor,
        SequenceDiagram.ParticipantTypes.Boundary,
        SequenceDiagram.ParticipantTypes.Control,
        SequenceDiagram.ParticipantTypes.Entity,
        SequenceDiagram.ParticipantTypes.Database,
        "collections",
        "queue",
        "as",
        "create",
        "destroy",
        "activate",
        "deactivate",
        "note",
        CssStyles.HtmlBreak,
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
        SequenceDiagram.BlockOpenerNames.Loop,
        GeneralElementNames.End,
        SequenceDiagram.BlockOpenerNames.Alt,
        SequenceDiagram.BlockOpenerNames.Else,
        SequenceDiagram.BlockOpenerNames.Opt,
        SequenceDiagram.BlockOpenerNames.Par,
        SequenceDiagram.BlockOpenerNames.And,
        SequenceDiagram.BlockOpenerNames.Critical,
        SequenceDiagram.BlockOpenerNames.Break,
        ShapeNames.Rect,
        "rgb",
        "rgba",
        "autonumber",
        GeneralElementNames.Link,
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
        // Sequence Numbers: https://mermaid.js.org/syntax/sequenceDiagram.html#sequencenumbers
        "showSequenceNumbers:",
    ];

    #endregion Sequence Diagram

    #region State Diagrams

    /// <summary>
    /// Contains keywords specific to State diagrams (stateDiagram, stateDiagram-v2).
    /// See: https://mermaid.js.org/syntax/stateDiagram.html
    /// </summary>
    internal static readonly string[] StateDiagramKeywords =
    [
        DiagramTypeNames.StateDiagram,
        DiagramTypeNames.StateDiagramV2,
        StateDiagram.BlockOpenerNames.State,
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
        GeneralElementNames.Direction,
        GeneralElementNames.Class,
        GeneralElementNames.ClassDef,
        DirectionNames.TopToBottom,
        DirectionNames.BottomToTop,
        DirectionNames.RightToLeft,
        DirectionNames.LeftToRight,
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
        DiagramTypeNames.Timeline,
        GeneralElementNames.Title,
        GeneralElementNames.Section,
        CssStyles.HtmlBreak,
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
        DiagramTypeNames.Treemap,
        GeneralElementNames.Title,
        GeneralElementNames.ClassDef,
        // Treemap Theming: https://mermaid.js.org/syntax/treemap.html#diagram-padding
        "diagramPadding:",
        // https://mermaid.js.org/syntax/treemap.html#configuration-options
        Frontmatter.UseMaxWidth,
        CssStyles.Padding,
        "showValues:",
        "nodeWidth:",
        "nodeHeight:",
        CssStyles.BorderWidth,
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
        DiagramTypeNames.UserJourney,
        GeneralElementNames.Section,
        GeneralElementNames.Title,
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
        DiagramTypeNames.XYChart,
        GeneralElementNames.Title,
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
        CssStyles.Width,
        CssStyles.Height,
        "showTitle:",
        "chartOrientation:",
        "plotReservedSpacePercent:",
        "showDataLabel:",
        // XY Chart AxisConfig: https://mermaid.js.org/syntax/xyChart.html#axisconfig
        "showLabel:",
        "labelFontSize:",
        "labelPadding:",
        "titleFontSize:",
        "titlePadding:",
        "showTick:",
        "tickLength:",
        "tickWidth:",
        "showAxisLine:",
        "axisLineWidth:",
        // Chart Theme Variables: https://mermaid.js.org/syntax/xyChart.html#chart-theme-variables
        CssStyles.BackgroundColor,
        CssStyles.TitleColor,
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
    /// <returns>An array of <see cref="IntellisenseCompletionData"/> objects corresponding to the provided keywords. Returns an empty array if
    /// no keywords are specified.</returns>
    internal static IntellisenseCompletionData[] CreateCompletionData(string[] keywords, int priority = 0, RenderTargetBitmap? icon = null)
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
        CreateCompletionData(AggregatedDistinctKeywords, priority: 0, IntellisenseCompletionData.AbcIcon);
}
