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

using MermaidPad.Models.Constants;
using System.Collections.Frozen;

namespace MermaidPad.Models;

/// <summary>
/// Provides lookup sets and dictionaries for supported diagram types, block openers, and related keywords, enabling
/// efficient, case-sensitive recognition and mapping of diagram language elements.
/// </summary>
/// <remarks>This static class exposes pre-initialized, thread-safe collections for fast membership and mapping
/// queries involving diagram type names, sequence block openers, C4 boundary types, and requirement block types.
/// Alternate lookup variants are provided for allocation-free queries using read-only character spans, which are useful
/// in high-performance parsing scenarios. All lookups use ordinal, case-sensitive comparison to ensure consistent
/// recognition of keywords.</remarks>
internal static class DiagramTypeLookups
{
    #region FrozenSet and FrozenDictionary Declarations

    /// <summary>
    /// Provides a pre-initialized, case-sensitive set containing the names of all supported diagram declarations.
    /// </summary>
    /// <remarks>This set is used to efficiently determine whether a given string corresponds to a recognized
    /// diagram type. The comparison is performed using ordinal, case-sensitive matching. The set includes both stable
    /// and beta diagram types.</remarks>
    internal static readonly FrozenSet<string> DiagramDeclarations = FrozenSet.ToFrozenSet(
    [
        DiagramTypeNames.Flowchart,
        DiagramTypeNames.FlowchartElk,
        DiagramTypeNames.Graph,
        DiagramTypeNames.SequenceDiagram,
        DiagramTypeNames.StateDiagram,
        DiagramTypeNames.StateDiagramV2,
        DiagramTypeNames.ClassDiagram,
        DiagramTypeNames.ERDiagram,
        DiagramTypeNames.Gantt,
        DiagramTypeNames.Pie,
        DiagramTypeNames.Mindmap,
        DiagramTypeNames.Timeline,
        DiagramTypeNames.UserJourney,
        DiagramTypeNames.GitGraph,
        DiagramTypeNames.C4Context,
        DiagramTypeNames.C4Container,
        DiagramTypeNames.C4Component,
        DiagramTypeNames.C4Deployment,
        DiagramTypeNames.C4Dynamic,
        DiagramTypeNames.ArchitectureBeta,
        DiagramTypeNames.Block,
        DiagramTypeNames.RequirementDiagram,
        DiagramTypeNames.Sankey,
        DiagramTypeNames.XYChart,
        DiagramTypeNames.QuadrantChart,
        DiagramTypeNames.Packet,
        DiagramTypeNames.Kanban,
        DiagramTypeNames.RadarBeta,
        DiagramTypeNames.Treemap
    ], StringComparer.Ordinal);

    /// <summary>
    /// Represents a set of keywords that indicate the opening of a sequence block in the parsed language. The set is
    /// case-sensitive.
    /// </summary>
    /// <remarks>This set is used to efficiently determine whether a given string marks the start of a
    /// sequence block, such as 'loop', 'alt', or 'opt'. The use of a frozen set ensures fast lookups and thread
    /// safety.</remarks>
    internal static readonly FrozenSet<string> SequenceBlockOpeners = FrozenSet.ToFrozenSet(
    [
        SequenceDiagram.BlockOpenerNames.Loop,
        SequenceDiagram.BlockOpenerNames.Alt,
        SequenceDiagram.BlockOpenerNames.Else,
        SequenceDiagram.BlockOpenerNames.Opt,
        SequenceDiagram.BlockOpenerNames.Par,
        SequenceDiagram.BlockOpenerNames.And,
        SequenceDiagram.BlockOpenerNames.Critical,
        SequenceDiagram.BlockOpenerNames.Break,
        SequenceDiagram.BlockOpenerNames.Rect
    ], StringComparer.Ordinal);

    /// <summary>
    /// Maps exact diagram type keywords to their corresponding <see cref="DiagramType"/> values.
    /// Uses FrozenDictionary for O(1) case-sensitive lookup without string allocation.
    /// </summary>
    internal static readonly FrozenDictionary<string, DiagramType> DiagramNameToTypeMapping = new Dictionary<string, DiagramType>
    {
        [DiagramTypeNames.Flowchart] = DiagramType.Flowchart,
        [DiagramTypeNames.FlowchartElk] = DiagramType.FlowchartElk,
        [DiagramTypeNames.Graph] = DiagramType.Graph,
        [DiagramTypeNames.SequenceDiagram] = DiagramType.Sequence,
        [DiagramTypeNames.StateDiagram] = DiagramType.State,
        [DiagramTypeNames.StateDiagramV2] = DiagramType.StateV2,
        [DiagramTypeNames.ClassDiagram] = DiagramType.Class,
        [DiagramTypeNames.ClassDiagramV2] = DiagramType.ClassV2,

        [DiagramTypeNames.ERDiagram] = DiagramType.ERDiagram,
        [DiagramTypeNames.UserJourney] = DiagramType.UserJourney,
        [DiagramTypeNames.Gantt] = DiagramType.Gantt,
        [DiagramTypeNames.Pie] = DiagramType.Pie,
        [DiagramTypeNames.QuadrantChart] = DiagramType.QuadrantChart,
        [DiagramTypeNames.RequirementDiagram] = DiagramType.Requirement,
        [DiagramTypeNames.GitGraph] = DiagramType.GitGraph,

        [DiagramTypeNames.C4Context] = DiagramType.C4Context,
        [DiagramTypeNames.C4Container] = DiagramType.C4Container,
        [DiagramTypeNames.C4Component] = DiagramType.C4Component,
        [DiagramTypeNames.C4Deployment] = DiagramType.C4Deployment,
        [DiagramTypeNames.C4Dynamic] = DiagramType.C4Dynamic,

        [DiagramTypeNames.Mindmap] = DiagramType.Mindmap,
        [DiagramTypeNames.Timeline] = DiagramType.Timeline,
        [DiagramTypeNames.Sankey] = DiagramType.Sankey,
        [DiagramTypeNames.XYChart] = DiagramType.XYChart,
        [DiagramTypeNames.Block] = DiagramType.Block,

        [DiagramTypeNames.Packet] = DiagramType.Packet,
        [DiagramTypeNames.Kanban] = DiagramType.Kanban,
        [DiagramTypeNames.ArchitectureBeta] = DiagramType.ArchitectureBeta,
        [DiagramTypeNames.RadarBeta] = DiagramType.RadarBeta,
        [DiagramTypeNames.Treemap] = DiagramType.Treemap
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// Represents a set of C4 model boundary type names used for case-sensitive comparisons.
    /// </summary>
    /// <remarks>This set includes common boundary types such as "Enterprise_Boundary", "System_Boundary",
    /// "Container_Boundary", and "Boundary". The set is frozen for efficient lookups and uses ordinal case-sensitive
    /// string comparison.</remarks>
    internal static readonly FrozenSet<string> C4BoundaryTypes = FrozenSet.ToFrozenSet(
    [
        C4Diagram.BoundaryTypes.Enterprise,
        C4Diagram.BoundaryTypes.System,
        C4Diagram.BoundaryTypes.Container,
        C4Diagram.BoundaryTypes.Boundary
    ], StringComparer.Ordinal);

    /// <summary>
    /// Contains the set of block type names that are recognized as requirement-related elements.
    /// </summary>
    /// <remarks>The set includes common requirement block types such as 'requirement',
    /// 'functionalRequirement', and 'designConstraint'. Comparisons are performed using case-sensitive ordinal
    /// matching.</remarks>
    internal static readonly FrozenSet<string> RequirementBlockTypes = FrozenSet.ToFrozenSet(
    [
        RequirementDiagram.BlockTypes.Requirement,
        RequirementDiagram.BlockTypes.FunctionalRequirement,
        RequirementDiagram.BlockTypes.PerformanceRequirement,
        RequirementDiagram.BlockTypes.InterfaceRequirement,
        RequirementDiagram.BlockTypes.PhysicalRequirement,
        RequirementDiagram.BlockTypes.DesignConstraint,
        RequirementDiagram.BlockTypes.Element
    ], StringComparer.Ordinal);

    #endregion FrozenSet and FrozenDictionary Declarations

    #region Allocation-free AlternateLookups

    /// <summary>
    /// Provides an alternate lookup for diagram declarations that enables allocation-free queries using read-only
    /// character spans.
    /// </summary>
    /// <remarks>This lookup allows efficient membership checks against the set of diagram declarations
    /// without allocating new strings. It is intended for scenarios where performance is critical and input data is
    /// available as a span of characters.</remarks>
    internal static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> DiagramDeclarationsLookup =
        DiagramDeclarations.GetAlternateLookup<ReadOnlySpan<char>>();

    /// <summary>
    /// Provides an alternate lookup for sequence block opener strings using a read-only character span as the key.
    /// </summary>
    /// <remarks>This lookup enables efficient matching of sequence block opener strings against spans of
    /// characters, which can improve performance in scenarios where input is not already a string. The lookup is
    /// case-sensitive and relies on the contents of the underlying set of sequence block openers.</remarks>
    internal static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> SequenceBlockOpenersLookup =
        SequenceBlockOpeners.GetAlternateLookup<ReadOnlySpan<char>>();

    /// <summary>
    /// Provides an alternate lookup for diagram types using exact string matching with a read-only character span as
    /// the key.
    /// </summary>
    /// <remarks>This lookup enables efficient retrieval of diagram types by matching the exact sequence of
    /// characters in the input span. It is intended for scenarios where precise, case-sensitive matching is required.
    /// The lookup is read-only and thread-safe.</remarks>
    internal static readonly FrozenDictionary<string, DiagramType>.AlternateLookup<ReadOnlySpan<char>> DiagramNameToTypeMappingLookup =
        DiagramNameToTypeMapping.GetAlternateLookup<ReadOnlySpan<char>>();

    /// <summary>
    /// Provides an alternate lookup for C4 boundary types using a read-only character span as the key.
    /// </summary>
    /// <remarks>This lookup enables efficient, allocation-free searches for boundary types by allowing
    /// queries with a <see cref="ReadOnlySpan{char}"/> instead of a string. It is intended
    /// for internal use to optimize performance when working with substrings or slices of character data.</remarks>
    internal static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> C4BoundaryTypesLookup =
        C4BoundaryTypes.GetAlternateLookup<ReadOnlySpan<char>>();

    /// <summary>
    /// Provides an alternate lookup for requirement block types using a read-only character span as the key.
    /// </summary>
    /// <remarks>This lookup enables efficient, case-sensitive searches for requirement block types without
    /// allocating new strings. It is intended for scenarios where input is available as a span, such as parsing or
    /// streaming operations.</remarks>
    internal static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> RequirementBlockTypesLookup =
        RequirementBlockTypes.GetAlternateLookup<ReadOnlySpan<char>>();

    #endregion Allocation-free AlternateLookups
}
