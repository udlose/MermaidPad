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
    public const string Flowchart = "flowchart";
    public const string FlowchartElk = "flowchart-elk";
    public const string Graph = "graph";
    public const string SequenceDiagram = "sequenceDiagram";
    public const string StateDiagram = "stateDiagram";
    public const string StateDiagramV2 = "stateDiagram-v2";
    public const string ClassDiagram = "classDiagram";
    public const string ClassDiagramV2 = "classDiagram-v2";

    public const string ERDiagram = "erDiagram";
    public const string UserJourney = "journey";
    public const string Gantt = "gantt";
    public const string Pie = "pie";
    public const string QuadrantChart = "quadrantChart";
    public const string RequirementDiagram = "requirementDiagram";
    public const string GitGraph = "gitGraph";

    public const string C4Context = "C4Context";
    public const string C4Container = "C4Container";
    public const string C4Component = "C4Component";
    public const string C4Deployment = "C4Deployment";
    public const string C4Dynamic = "C4Dynamic";

    public const string Mindmap = "mindmap";
    public const string Timeline = "timeline";
    public const string Sankey = "sankey";
    public const string XYChart = "xychart";
    public const string Block = "block";

    public const string Packet = "packet";
    public const string Kanban = "kanban";
    public const string ArchitectureBeta = "architecture-beta";
    public const string RadarBeta = "radar-beta";
    public const string Treemap = "treemap";
}
