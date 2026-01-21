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
/// Provides constant string values representing supported diagram type names for use with Mermaid and related
/// diagramming tools. See: https://mermaid.js.org/ for more information.
/// </summary>
/// <remarks>The constants in this class follow the naming conventions used by Mermaid to ensure compatibility
/// when generating or parsing diagrams. This class is intended for internal use to avoid hardcoding diagram type names
/// throughout the codebase.</remarks>
[SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Following Mermaid naming convention for clarity.")]
internal static class DiagramTypeNames
{
    internal const string Flowchart = "flowchart";
    internal const string FlowchartElk = "flowchart-elk";
    internal const string Graph = "graph";
    internal const string SequenceDiagram = "sequenceDiagram";
    internal const string StateDiagram = "stateDiagram";
    internal const string StateDiagramV2 = "stateDiagram-v2";
    internal const string ClassDiagram = "classDiagram";
    internal const string ClassDiagramV2 = "classDiagram-v2";

    internal const string ERDiagram = "erDiagram";
    internal const string UserJourney = "journey";
    internal const string Gantt = "gantt";
    internal const string Pie = "pie";
    internal const string QuadrantChart = "quadrantChart";
    internal const string RequirementDiagram = "requirementDiagram";
    internal const string GitGraph = "gitGraph";

    internal const string C4Context = "C4Context";
    internal const string C4Container = "C4Container";
    internal const string C4Component = "C4Component";
    internal const string C4Deployment = "C4Deployment";
    internal const string C4Dynamic = "C4Dynamic";

    internal const string Mindmap = "mindmap";
    internal const string Timeline = "timeline";
    internal const string Sankey = "sankey";
    internal const string XYChart = "xychart";
    internal const string Block = "block";

    internal const string Packet = "packet";
    internal const string Kanban = "kanban";
    internal const string ArchitectureBeta = "architecture-beta";
    internal const string RadarBeta = "radar-beta";
    internal const string Treemap = "treemap";
}
