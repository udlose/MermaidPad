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
/// Represents the type of Mermaid diagram detected in the document.
/// </summary>
[SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Enum names match Mermaid diagram types")]
public enum DiagramType
{
    /// <summary>
    /// An unknown or unrecognized diagram type.
    /// </summary>
    Unknown,

    /// <summary>
    /// A standard flowchart diagram showing process flows and decision points.
    /// See https://mermaid.js.org/syntax/flowchart.html
    /// </summary>
    Flowchart,

    /// <summary>
    /// A flowchart diagram using the ELK (Eclipse Layout Kernel) layout engine for improved automatic layout.
    /// See https://mermaid.js.org/syntax/flowchart.html#renderer
    /// </summary>
    FlowchartElk,

    /// <summary>
    /// A graph diagram representing nodes and their connections.
    /// See https://mermaid.js.org/syntax/flowchart.html
    /// </summary>
    Graph,

    /// <summary>
    /// A sequence diagram depicting interactions between actors and objects over time.
    /// See https://mermaid.js.org/syntax/sequenceDiagram.html
    /// </summary>
    Sequence,

    /// <summary>
    /// A state diagram (version 1) showing states and transitions in a system.
    /// See https://mermaid.js.org/syntax/stateDiagram.html
    /// </summary>
    State,

    /// <summary>
    /// A state diagram (version 2) with enhanced features for modeling system states and transitions.
    /// See https://mermaid.js.org/syntax/stateDiagram.html
    /// </summary>
    StateV2,

    /// <summary>
    /// A class diagram (version 1) showing object-oriented class structures and relationships.
    /// See https://mermaid.js.org/syntax/classDiagram.html
    /// </summary>
    Class,

    /// <summary>
    /// A class diagram (version 2) with improved syntax for modeling class hierarchies and associations.
    /// See https://mermaid.js.org/syntax/classDiagram.html
    /// </summary>
    ClassV2,

    /// <summary>
    /// An Entity Relationship diagram modeling database entities and their relationships.
    /// See https://mermaid.js.org/syntax/entityRelationshipDiagram.html
    /// </summary>
    ERDiagram,

    /// <summary>
    /// A user journey diagram mapping user experiences and touchpoints through a process.
    /// See https://mermaid.js.org/syntax/userJourney.html
    /// </summary>
    UserJourney,

    /// <summary>
    /// A Gantt chart for visualizing project schedules, tasks, and timelines.
    /// See https://mermaid.js.org/syntax/gantt.html
    /// </summary>
    Gantt,

    /// <summary>
    /// A pie chart displaying proportional data as slices of a circle.
    /// See https://mermaid.js.org/syntax/pie.html
    /// </summary>
    Pie,

    /// <summary>
    /// A quadrant chart plotting items across two dimensions in four quadrants.
    /// See https://mermaid.js.org/syntax/quadrantChart.html
    /// </summary>
    QuadrantChart,

    /// <summary>
    /// A requirement diagram modeling system requirements and their relationships.
    /// See https://mermaid.js.org/syntax/requirementDiagram.html
    /// </summary>
    Requirement,

    /// <summary>
    /// A Git graph diagram visualizing Git branching, commits, and merge history.
    /// See https://mermaid.js.org/syntax/gitgraph.html
    /// </summary>
    GitGraph,

    /// <summary>
    /// A C4 context diagram showing the system context and external actors (highest level C4 model).
    /// See https://mermaid.js.org/syntax/c4.html#c4-system-context-diagram-c4context
    /// </summary>
    C4Context,

    /// <summary>
    /// A C4 container diagram showing the high-level technology choices and containers within the system.
    /// See https://mermaid.js.org/syntax/c4.html#c4-container-diagram-c4container
    /// </summary>
    C4Container,

    /// <summary>
    /// A C4 component diagram showing the internal components and their interactions within a container.
    /// See https://mermaid.js.org/syntax/c4.html#c4-component-diagram-c4component
    /// </summary>
    C4Component,

    /// <summary>
    /// A C4 deployment diagram showing the physical deployment of containers to infrastructure.
    /// See https://mermaid.js.org/syntax/c4.html#c4-deployment-diagram-c4deployment
    /// </summary>
    C4Deployment,

    /// <summary>
    /// A C4 dynamic diagram illustrating runtime behavior and collaboration between elements.
    /// See https://mermaid.js.org/syntax/c4.html#c4-dynamic-diagram-c4dynamic
    /// </summary>
    C4Dynamic,

    /// <summary>
    /// A mindmap diagram for visualizing hierarchical information and brainstorming.
    /// See https://mermaid.js.org/syntax/mindmap.html
    /// </summary>
    Mindmap,

    /// <summary>
    /// A timeline diagram showing chronological events along a time axis.
    /// See https://mermaid.js.org/syntax/timeline.html
    /// </summary>
    Timeline,

    /// <summary>
    /// A Sankey diagram visualizing flow quantities between nodes with proportional link widths.
    /// See https://mermaid.js.org/syntax/sankey.html
    /// </summary>
    Sankey,

    /// <summary>
    /// An XY chart for plotting data points on a coordinate system.
    /// See https://mermaid.js.org/syntax/xyChart.html
    /// </summary>
    XYChart,

    /// <summary>
    /// A block diagram showing system components and their connections.
    /// See https://mermaid.js.org/syntax/block.html
    /// </summary>
    Block,

    /// <summary>
    /// A packet diagram visualizing network packet structures and protocol layers.
    /// See https://mermaid.js.org/syntax/packet.html
    /// </summary>
    Packet,

    /// <summary>
    /// A Kanban board diagram for visualizing workflow and work-in-progress.
    /// See https://mermaid.js.org/syntax/kanban.html
    /// </summary>
    Kanban,

    /// <summary>
    /// An architecture diagram (beta) for modeling software or system architecture.
    /// See https://mermaid.js.org/syntax/architecture.html
    /// </summary>
    ArchitectureBeta,

    /// <summary>
    /// A radar chart (beta) displaying multivariate data on axes radiating from a center point.
    /// See https://mermaid.js.org/syntax/radar.html
    /// </summary>
    RadarBeta,

    /// <summary>
    /// A treemap diagram displaying hierarchical data as nested rectangles.
    /// See https://mermaid.js.org/syntax/treemap.html
    /// </summary>
    Treemap
}
