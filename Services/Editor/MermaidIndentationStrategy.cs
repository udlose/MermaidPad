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

using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Indentation;
using MermaidPad.Models;
using MermaidPad.Models.Constants;
using MermaidPad.Models.Editor;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace MermaidPad.Services.Editor;

/// <summary>
/// Provides smart indentation for Mermaid diagrams and YAML frontmatter in the AvaloniaEdit text editor.
/// </summary>
/// <remarks>
/// <para>
/// This indentation strategy supports:
/// </para>
/// <list type="bullet">
///     <item><description>YAML frontmatter delimited by <c>---</c></description></item>
///     <item><description>Most Mermaid diagram families, including block-structured ones (e.g., flowchart subgraphs, sequence blocks)</description></item>
///     <item><description>Auto-dedent behavior for block-closing lines (e.g., <c>end</c> and <c>}</c>)</description></item>
///     <item><description>Sequence continuation keywords (<c>else</c>, <c>and</c>) aligning with parent blocks</description></item>
///     <item><description>Indentation-based diagrams (e.g., mindmap/treemap/kanban) where user-controlled indentation is preserved</description></item>
/// </list>
/// <para>
/// This strategy also performs a minimal, comment-preserving frontmatter formatting step to ensure YAML remains valid:
/// it can insert a single space after a mapping colon when missing (e.g., <c>key:value</c> -> <c>key: value</c>).
/// </para>
/// <para>
/// Supported diagram types:
/// <list type="bullet">
///     <item>flowchart, flowchart-elk, graph (with subgraph/end)</item>
///     <item>sequenceDiagram (with loop/alt/opt/par/critical/break/rect/end, and else/and continuations)</item>
///     <item>stateDiagram, stateDiagram-v2 (with state/end)</item>
///     <item>classDiagram, classDiagram-v2 (with class and namespace blocks)</item>
///     <item>erDiagram (with entity blocks)</item>
///     <item>gantt (with section)</item>
///     <item>pie, quadrantChart, xychart, radar-beta (flat structure)</item>
///     <item>mindmap, treemap, kanban (indentation-based hierarchy)</item>
///     <item>timeline, journey (with section)</item>
///     <item>gitGraph (flat structure)</item>
///     <item>C4Context, C4Container, C4Component, C4Dynamic, C4Deployment (with boundaries)</item>
///     <item>architecture-beta (with group)</item>
///     <item>block (with block:/end)</item>
///     <item>requirementDiagram (with requirement/element blocks)</item>
///     <item>sankey, packet (flat structure)</item>
/// </list>
/// </para>
/// </remarks>
#pragma warning disable IDE0078
[SuppressMessage("Style", "IDE0078:Use pattern matching", Justification = "Performance and code clarity")]
[SuppressMessage("ReSharper", "MergeIntoPattern", Justification = "Performance and code clarity")]
#pragma warning disable S3267 // Loops should be simplified with "LINQ" expressions. This code is performance-sensitive.
public sealed partial class MermaidIndentationStrategy : DefaultIndentationStrategy
{
    internal const int OneBasedFirstLineNumber = 1;

    private const string SingleSpaceString = " ";
    private const char SpaceChar = ' ';
    private const char TabChar = '\t';
    private const char LeftParen = '(';
    private const char LeftBrace = '{';
    private const char RightBrace = '}';
    private const char Colon = ':';
    private const char Hyphen = '-';
    private const char Underscore = '_';
    private const char Hash = '#';
    private const char Percent = '%';
    private const int MaxPreAllocatedIndentLevels = 20;
    private const int MaxFrontmatterScanLines = 100;
    private readonly string _indentationString;
    private readonly int _indentationSize;

    // Diagram declaration caching
    private int _cachedDeclarationLineNumber; // 0 = not cached
    private string? _cachedDeclarationContent; // null = not cached
    private DiagramType _cachedDiagramType = DiagramType.Unknown;

    // Frontmatter boundary caching
    private int _cachedFrontmatterStartLine = -1; // -1 means no frontmatter
    private int _cachedFrontmatterEndLine = -1; // -1 means frontmatter not closed
    private ITextSourceVersion? _cachedFrontmatterVersion;

    #region Pre-allocated indentation strings

    /// <summary>
    /// Pre-allocated indentation strings for pure 2-space indentation levels.
    /// </summary>
    private static readonly string[] _preAllocatedIndents2Spaces = CreateIndentationCache("  ", MaxPreAllocatedIndentLevels);

    /// <summary>
    /// Pre-allocated indentation strings for pure 4-space indentation levels.
    /// </summary>
    private static readonly string[] _preAllocatedIndents4Spaces = CreateIndentationCache("    ", MaxPreAllocatedIndentLevels);

    /// <summary>
    /// Pre-allocated indentation strings for pure tab indentation levels.
    /// </summary>
    private static readonly string[] _preAllocatedIndentsTab = CreateIndentationCache("\t", MaxPreAllocatedIndentLevels);

    #endregion Pre-allocated indentation strings

    #region Regex patterns

    /// <summary>
    /// Provides a compiled regular expression that matches one or more whitespace characters.
    /// </summary>
    /// <remarks>
    /// This is used for normalizing diagram declaration lines for stable caching (collapsing repeated whitespace).
    /// </remarks>
    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceNormalizationRegex();

    #endregion Regex patterns

    /// <summary>
    /// Initializes a new instance of the <see cref="MermaidIndentationStrategy"/> class using the specified editor options.
    /// </summary>
    /// <param name="options">The text editor options containing indentation settings. Cannot be <see langword="null"/>.</param>
    /// <remarks>
    /// This constructor respects the user's configured indentation preferences from the editor options,
    /// including whether to use tabs or spaces and the indentation size.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> is <see langword="null"/>.</exception>
    public MermaidIndentationStrategy(TextEditorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _indentationString = options.IndentationString;
        _indentationSize = options.IndentationSize;
    }

    /// <summary>
    /// Applies indentation to a single line.
    /// </summary>
    /// <remarks>
    /// Indents a single line based on:
    /// <list type="bullet">
    /// <item><description>Cached frontmatter boundaries and YAML rules</description></item>
    /// <item><description>Mermaid diagram type (cached) and block keyword heuristics</description></item>
    /// <item><description>Auto-dedent rules for closing keywords (current line)</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="document"/> or <paramref name="line"/> is null.</exception>
    public override void IndentLine(TextDocument document, DocumentLine line)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(line);

        UpdateFrontmatterCache(document);
        DiagramType diagramType = GetCachedDiagramType(document);
        IndentLineInternal(document, line, diagramType);
    }

    /// <summary>
    /// Applies indentation to a range of lines. 
    /// </summary>
    /// <remarks>
    /// Applies indentation to each line in the specified range sequentially.
    /// This method also performs a safe frontmatter formatting pass (space insertion after YAML colons) within
    /// the same range to keep frontmatter valid without stripping comments.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="document"/> is null.</exception>
    public override void IndentLines(TextDocument document, int beginLine, int endLine)
    {
        ArgumentNullException.ThrowIfNull(document);

        beginLine = Math.Max(OneBasedFirstLineNumber, beginLine);
        endLine = Math.Min(document.LineCount, endLine);
        if (beginLine > endLine)
        {
            return;
        }

        // Update caches once (hot path).
        UpdateFrontmatterCache(document);

        // Keep YAML valid (low-risk formatting; does not parse YAML and does not remove comments).
        EnsureYamlSpacingInFrontmatter(document, beginLine, endLine);

        // Cache diagram type once for the batch.
        DiagramType diagramType = GetCachedDiagramType(document);

        for (int lineNumber = beginLine; lineNumber <= endLine; lineNumber++)
        {
            DocumentLine line = document.GetLineByNumber(lineNumber);
            IndentLineInternal(document, line, diagramType);
        }
    }

    #region Indentation core

    /// <summary>
    /// Performs indentation for a single line using the already-cached frontmatter boundaries and diagram type.
    /// </summary>
    /// <param name="document">The text document being edited.</param>
    /// <param name="line">The current line to indent.</param>
    /// <param name="diagramType">The cached diagram type for the document.</param>
    private void IndentLineInternal(TextDocument document, DocumentLine line, DiagramType diagramType)
    {
        DocumentContext context = DetermineContextFromCache(line.LineNumber);

        // Delimiters must be at column 0 (production rule).
        if (context == DocumentContext.FrontmatterStart || context == DocumentContext.FrontmatterEnd)
        {
            ApplyIndentation(document, line, string.Empty);
            return;
        }

        DocumentLine? previousLine = line.PreviousLine;
        if (previousLine is null)
        {
            return;
        }

        string desiredIndentation;
        if (context == DocumentContext.Frontmatter)
        {
            desiredIndentation = CalculateFrontmatterIndentation(document, line);
            ApplyIndentation(document, line, desiredIndentation);
            return;
        }

        desiredIndentation = CalculateDiagramIndentation(document, previousLine, diagramType);
        desiredIndentation = AdjustForCurrentLineKeywords(document, line, desiredIndentation, diagramType);

        ApplyIndentation(document, line, desiredIndentation);
    }

    /// <summary>
    /// Adjusts indentation based on the current line content for diagram context.
    /// </summary>
    /// <remarks>
    /// This implements dedent-on-the-closing-line behavior (e.g., <c>end</c> and <c>}</c> lines),
    /// and handles sequence continuation keywords (<c>else</c>, <c>and</c>) only for sequence diagrams.
    /// </remarks>
    /// <param name="document">The document containing the line.</param>
    /// <param name="line">The line being indented.</param>
    /// <param name="desiredIndentation">The computed indentation from previous-line rules.</param>
    /// <param name="diagramType">The detected diagram type.</param>
    /// <returns>The possibly adjusted indentation string.</returns>
    private string AdjustForCurrentLineKeywords(TextDocument document, DocumentLine line, string desiredIndentation, DiagramType diagramType)
    {
        // Avoid allocating the current-line string; parse directly from the document.
        if (IsClosingKeywordOnLine(document, line) ||
            (diagramType == DiagramType.Sequence && IsSequenceContinuationKeywordOnLine(document, line)))
        {
            return DedentOnce(desiredIndentation);
        }

        return desiredIndentation;
    }

    #endregion Indentation core

    #region YAML frontmatter indentation + formatting

    /// <summary>
    /// Calculates indentation for YAML frontmatter content.
    /// </summary>
    /// <remarks>
    /// This method implements the previously discussed YAML edge case:
    /// if there are blank lines between siblings (e.g., <c>logLevel: 'debug'</c> then blank line then <c>gitGraph:</c>),
    /// indentation is determined from the nearest previous non-blank line inside the frontmatter region.
    /// </remarks>
    /// <param name="document">The text document.</param>
    /// <param name="currentLine">The current line whose indentation is being computed.</param>
    /// <returns>The indentation string for the current frontmatter line.</returns>
    private string CalculateFrontmatterIndentation(TextDocument document, DocumentLine currentLine)
    {
        DocumentLine? effectivePrev = FindEffectivePreviousNonBlankLineInFrontmatter(document, currentLine);
        if (effectivePrev is null)
        {
            return string.Empty;
        }

        if (effectivePrev.LineNumber == _cachedFrontmatterStartLine)
        {
            // After opening delimiter, indent one level.
            return _indentationString;
        }

        ReadOnlySpan<char> prevSpan = document.GetText(effectivePrev.Offset, effectivePrev.Length).AsSpan();
        ReadOnlySpan<char> prevTrimmed = prevSpan.TrimStart();
        string prevIndentation = GetIndentationString(prevSpan);

        if (prevTrimmed.IsEmpty || IsYamlCommentLine(prevTrimmed))
        {
            return prevIndentation;
        }

        if (IsYamlBlockKeyLine(prevTrimmed))
        {
            return prevIndentation + _indentationString;
        }

        return prevIndentation;
    }

    /// <summary>
    /// Finds the nearest previous non-blank line within the frontmatter region.
    /// </summary>
    /// <param name="document">The document containing the frontmatter.</param>
    /// <param name="currentLine">The current line (within frontmatter).</param>
    /// <returns>The effective previous line, or null if none exists.</returns>
    private DocumentLine? FindEffectivePreviousNonBlankLineInFrontmatter(TextDocument document, DocumentLine currentLine)
    {
        DocumentLine? scan = currentLine.PreviousLine;
        while (scan is not null)
        {
            int scanLineNumber = scan.LineNumber;
            if (_cachedFrontmatterStartLine > 0 && scanLineNumber <= _cachedFrontmatterStartLine)
            {
                return scan;
            }

            if (!IsLineWhitespaceOnly(document, scan))
            {
                return scan;
            }

            scan = scan.PreviousLine;
        }

        return null;
    }

    /// <summary>
    /// Determines whether a trimmed YAML line is a comment line.
    /// </summary>
    /// <param name="trimmedStart">A span with leading whitespace already removed.</param>
    /// <returns>True if the line is a comment; otherwise false.</returns>
    private static bool IsYamlCommentLine(ReadOnlySpan<char> trimmedStart)
    {
        if (trimmedStart.IsEmpty)
        {
            return false;
        }

        // Mermaid-style comments sometimes appear in mixed content.
        return trimmedStart[0] == Hash || StartsWithChars(trimmedStart, Percent, Percent);
    }

    /// <summary>
    /// Determines whether a trimmed YAML line represents a "block key" that should indent the next line.
    /// </summary>
    /// <remarks>
    /// Treats these as block keys:
    /// <list type="bullet">
    /// <item><description><c>key:</c></description></item>
    /// <item><description><c>key: # comment</c></description></item>
    /// <item><description><c>- key:</c> (list-of-maps style)</description></item>
    /// <item><description><c>- key: # comment</c></description></item>
    /// </list>
    /// Treats <c>key: value</c> as an inline value (not a block key).
    /// </remarks>
    /// <param name="lineTrimmed">Line text with leading whitespace removed.</param>
    /// <returns>True if the line is a block key; otherwise false.</returns>
    private static bool IsYamlBlockKeyLine(ReadOnlySpan<char> lineTrimmed)
    {
        if (lineTrimmed.IsEmpty || IsYamlCommentLine(lineTrimmed))
        {
            return false;
        }

        ReadOnlySpan<char> span = lineTrimmed;

        // Allow list item prefix "- ..."
        if (span[0] == Hyphen)
        {
            span = span[1..].TrimStart();
        }

        if (span.IsEmpty)
        {
            return false;
        }

        int colonIndex = span.IndexOf(Colon);
        if (colonIndex < 0)
        {
            return false;
        }

        ReadOnlySpan<char> afterColon = span[(colonIndex + 1)..].TrimStart();
        if (afterColon.IsEmpty || afterColon[0] == Hash)
        {
            return true;
        }

        return StartsWithChars(afterColon, Percent, Percent);
    }

    /// <summary>
    /// Ensures YAML mapping colons have a space after them within the frontmatter region.
    /// </summary>
    /// <remarks>
    /// This is a deliberately minimal formatter:
    /// it only inserts a space after the first mapping colon on a line when missing (<c>key:value</c> -> <c>key: value</c>).
    /// It does not strip comments and does not parse YAML.
    /// </remarks>
    /// <param name="document">The document containing the frontmatter.</param>
    /// <param name="beginLine">The inclusive start line to format.</param>
    /// <param name="endLine">The inclusive end line to format.</param>
    private void EnsureYamlSpacingInFrontmatter(TextDocument document, int beginLine, int endLine)
    {
        if (_cachedFrontmatterStartLine < 0)
        {
            return;
        }

        int frontmatterStart = _cachedFrontmatterStartLine;
        int frontmatterEnd = _cachedFrontmatterEndLine > 0
            ? _cachedFrontmatterEndLine
            : Math.Min(document.LineCount, MaxFrontmatterScanLines);

        int from = Math.Max(beginLine, frontmatterStart + 1);
        int to = Math.Min(endLine, frontmatterEnd - 1);
        if (from > to)
        {
            return;
        }

        // Walk backwards to keep insert offsets stable across the range.
        for (int lineNumber = to; lineNumber >= from; lineNumber--)
        {
            DocumentLine line = document.GetLineByNumber(lineNumber);
            EnsureYamlSpacingOnLine(document, line);
        }
    }

    /// <summary>
    /// Inserts a single space after a YAML mapping colon when missing for a single line.
    /// </summary>
    /// <param name="document">The document containing the line.</param>
    /// <param name="line">The line to inspect and possibly modify.</param>
    private static void EnsureYamlSpacingOnLine(TextDocument document, DocumentLine line)
    {
        ReadOnlySpan<char> span = document.GetText(line.Offset, line.Length).AsSpan();
        ReadOnlySpan<char> trimmedStart = span.TrimStart();
        if (trimmedStart.IsEmpty || IsYamlCommentLine(trimmedStart))
        {
            return;
        }

        // Track how far into trimmedStart we advanced (used for absolute offset).
        ReadOnlySpan<char> scan = trimmedStart;
        int prefixAdvance = 0;

        // Optional list-item "- " prefix.
        if (scan[0] == Hyphen)
        {
            ReadOnlySpan<char> afterDash = scan[1..];
            ReadOnlySpan<char> afterDashTrimmed = afterDash.TrimStart();

            prefixAdvance = 1 + (afterDash.Length - afterDashTrimmed.Length);
            scan = afterDashTrimmed;

            if (scan.IsEmpty)
            {
                return;
            }
        }

        int colonIndex = scan.IndexOf(Colon);
        if (colonIndex <= 0)
        {
            return;
        }

        if (colonIndex + 1 >= scan.Length)
        {
            return;
        }

        char next = scan[colonIndex + 1];
        if (char.IsWhiteSpace(next))
        {
            return;
        }

        // Absolute insert offset: line.Offset + start-of-trimmedStart + prefixAdvance + colonIndex + 1
        int trimmedStartIndexInSpan = span.Length - trimmedStart.Length;
        int insertOffset = line.Offset + trimmedStartIndexInSpan + prefixAdvance + colonIndex + 1;
        document.Insert(insertOffset, SingleSpaceString);
    }

    #endregion YAML frontmatter

    #region Mermaid diagram indentation

    /// <summary>
    /// Calculates the appropriate indentation string for a Mermaid diagram line based on the preceding line and the
    /// diagram type.
    /// </summary>
    /// <remarks>This method considers the content and structure of the previous line, as well as the diagram
    /// type, to ensure correct indentation for Mermaid diagrams. Indentation is increased after diagram declarations or
    /// block openers, and preserved for indentation-based diagrams.</remarks>
    /// <param name="document">The text document containing the diagram.</param>
    /// <param name="previousLine">The line preceding the current diagram line. Used to determine the base indentation.</param>
    /// <param name="diagramType">The type of diagram being processed. Determines whether indentation
    /// is required or how it is applied.</param>
    /// <returns>A string representing the indentation to apply to the current diagram line. Returns the previous line's
    /// indentation, possibly with an additional indentation level depending on the diagram context.</returns>
    private string CalculateDiagramIndentation(TextDocument document, DocumentLine previousLine, DiagramType diagramType)
    {
        ReadOnlySpan<char> previousSpan = document.GetText(previousLine.Offset, previousLine.Length).AsSpan();
        ReadOnlySpan<char> previousTrimmed = previousSpan.TrimStart();

        string previousIndentation = GetIndentationString(previousSpan);
        if (previousTrimmed.IsEmpty || StartsWithChars(previousTrimmed, Percent, Percent))
        {
            return previousIndentation;
        }

        if (IsDiagramDeclaration(previousTrimmed))
        {
            return previousIndentation + _indentationString;
        }

        if (IsIndentationBasedDiagram(diagramType))
        {
            return previousIndentation;
        }

        if (IsBlockOpener(diagramType, previousTrimmed))
        {
            return previousIndentation + _indentationString;
        }

        return previousIndentation;
    }

    /// <summary>
    /// Determines whether the specified diagram type is indentation-based (user-controlled hierarchy).
    /// </summary>
    /// <param name="diagramType">The diagram type to test.</param>
    /// <returns>True if indentation-based; otherwise false.</returns>
    private static bool IsIndentationBasedDiagram(DiagramType diagramType)
    {
        return diagramType == DiagramType.Mindmap ||
               diagramType == DiagramType.Treemap ||
               diagramType == DiagramType.Kanban;
    }

    /// <summary>
    /// Determines if the line contains a Mermaid diagram declaration (e.g., <c>flowchart TD</c>).
    /// </summary>
    /// <param name="lineTrimmed">Line text with leading whitespace removed.</param>
    /// <returns>True if this line is a declaration; otherwise false.</returns>
    private static bool IsDiagramDeclaration(ReadOnlySpan<char> lineTrimmed)
    {
        int spaceIndex = lineTrimmed.IndexOf(SpaceChar);
        ReadOnlySpan<char> firstWord = spaceIndex > 0 ? lineTrimmed[..spaceIndex] : lineTrimmed;
        return DiagramTypeLookups.DiagramDeclarationsLookup.Contains(firstWord);
    }

    /// <summary>
    /// Determines whether a trimmed line begins a new Mermaid block that should increase indentation.
    /// </summary>
    /// <remarks>
    /// This method is a heuristic and is intentionally conservative: it only increases indentation for patterns
    /// known to represent a nested region in the supported Mermaid families.
    /// </remarks>
    /// <param name="diagramType">The detected diagram type.</param>
    /// <param name="lineTrimmed">Line text with leading whitespace removed.</param>
    /// <returns>True if the line opens a block; otherwise false.</returns>
    private static bool IsBlockOpener(DiagramType diagramType, ReadOnlySpan<char> lineTrimmed)
    {
        if (lineTrimmed.IsEmpty)
        {
            return false;
        }

        ReadOnlySpan<char> firstWord = GetFirstWord(lineTrimmed);
        return IsFlowchartSubgraphOpener(diagramType, firstWord) ||
               IsSequenceBlockOpener(diagramType, firstWord) ||
               IsStateBlockOpener(diagramType, lineTrimmed, firstWord) ||
               IsClassBlockOpener(diagramType, lineTrimmed, firstWord) ||
               IsBlockDiagramOpener(diagramType, firstWord) ||
               IsSectionOpener(diagramType, firstWord) ||
               IsArchitectureGroupOpener(diagramType, firstWord) ||
               IsC4BoundaryOpener(diagramType, firstWord) ||
               IsRequirementBlockOpener(diagramType, firstWord) ||
               IsErEntityOpener(diagramType, lineTrimmed);
    }

    /// <summary>
    /// Returns the first word from the specified span of characters, delimited by the first space character.
    /// </summary>
    /// <param name="lineTrimmed">A span of characters to search for the first word.
    /// Leading and trailing whitespace should be removed before calling this method.</param>
    /// <returns>A read-only span containing the first word in the input. If no space is found,
    /// returns the entire input span.</returns>
    private static ReadOnlySpan<char> GetFirstWord(ReadOnlySpan<char> lineTrimmed)
    {
        int spaceIndex = lineTrimmed.IndexOf(SpaceChar);
        return spaceIndex > 0 ? lineTrimmed[..spaceIndex] : lineTrimmed;
    }

    /// <summary>
    /// Determines whether the specified word represents a subgraph opener in a flowchart or graph diagram type.
    /// </summary>
    /// <param name="diagramType">The type of diagram to evaluate. Only flowchart and graph diagram
    /// types support subgraph openers.</param>
    /// <param name="firstWord">A read-only span containing the first word to check for a subgraph opener keyword.</param>
    /// <returns>true if the diagram type supports subgraph openers and the first word matches the subgraph opener keyword;
    /// otherwise, false.</returns>
    private static bool IsFlowchartSubgraphOpener(DiagramType diagramType, ReadOnlySpan<char> firstWord)
    {
        // "subgraph" only applies to flowchart/graph families.
        if (diagramType != DiagramType.Flowchart &&
            diagramType != DiagramType.FlowchartElk &&
            diagramType != DiagramType.Graph)
        {
            return false;
        }

        return firstWord.Equals(FlowchartDiagram.BlockOpenerNames.Subgraph, StringComparison.Ordinal);
    }

    /// <summary>
    /// Determines whether the specified word represents the start of a sequence block in a sequence diagram.
    /// </summary>
    /// <param name="diagramType">The type of diagram being processed. Must be <see cref="DiagramType.Sequence"/>
    /// to evaluate sequence block openers.</param>
    /// <param name="firstWord">A read-only span containing the first word to check for a sequence block opener.</param>
    /// <returns>true if <paramref name="diagramType"/> is <see cref="DiagramType.Sequence"/> and <paramref name="firstWord"/>
    /// matches a recognized sequence block opener keyword; otherwise, false.</returns>
    private static bool IsSequenceBlockOpener(DiagramType diagramType, ReadOnlySpan<char> firstWord)
    {
        if (diagramType != DiagramType.Sequence)
        {
            return false;
        }

        // Sequence blocks: loop/alt/opt/par/critical/break/rect
        return DiagramTypeLookups.SequenceBlockOpenersLookup.Contains(firstWord);
    }

    /// <summary>
    /// Determines whether the specified line represents the opening of a state block in a state diagram.
    /// </summary>
    /// <param name="diagramType">The type of diagram being parsed. Must be either
    /// <see cref="DiagramType.State"/> or <see cref="DiagramType.StateV2"/> to allow state block openers.</param>
    /// <param name="lineTrimmed">A span containing the trimmed line of text to evaluate.</param>
    /// <param name="firstWord">A span containing the first word of the trimmed line, used to identify state block openers.</param>
    /// <returns>true if the line is recognized as a state block opener in a state diagram; otherwise, false.</returns>
    private static bool IsStateBlockOpener(DiagramType diagramType, ReadOnlySpan<char> lineTrimmed, ReadOnlySpan<char> firstWord)
    {
        if (diagramType != DiagramType.State && diagramType != DiagramType.StateV2)
        {
            return false;
        }

        // state X { ... }
        if (!EndsWithChar(lineTrimmed, LeftBrace))
        {
            return false;
        }

        return firstWord.Equals(StateDiagram.BlockOpenerNames.State, StringComparison.Ordinal);
    }

    /// <summary>
    /// Determines whether the specified line represents the opening of a class or namespace block in a class diagram.
    /// </summary>
    /// <param name="diagramType">The type of diagram being parsed. Must be either
    /// <see cref="DiagramType.Class"/> or <see cref="DiagramType.ClassV2"/> for this method to return true.</param>
    /// <param name="lineTrimmed">A span containing the trimmed line of text to evaluate.</param>
    /// <param name="firstWord">A span containing the first word of the trimmed line, used to identify block type.</param>
    /// <returns>true if the line opens a class or namespace block in a class diagram; otherwise, false.</returns>
    private static bool IsClassBlockOpener(DiagramType diagramType, ReadOnlySpan<char> lineTrimmed, ReadOnlySpan<char> firstWord)
    {
        if (diagramType != DiagramType.Class && diagramType != DiagramType.ClassV2)
        {
            return false;
        }

        // namespace/class blocks always have "{"
        if (!EndsWithChar(lineTrimmed, LeftBrace))
        {
            return false;
        }

        return firstWord.Equals(ClassDiagram.Namespace, StringComparison.Ordinal) ||
               firstWord.Equals(ClassDiagram.Class, StringComparison.Ordinal);
    }

    /// <summary>
    /// Determines whether the specified word represents a valid block diagram opener for the given diagram type.
    /// </summary>
    /// <remarks>This method checks for valid block diagram opener keywords such as "block:" or "blockID:" and
    /// excludes reserved keywords like "block-beta". The check is case-sensitive and only applies to block
    /// diagrams.</remarks>
    /// <param name="diagramType">The type of diagram being parsed. Must be
    /// <see cref="DiagramType.Block"/> to allow block diagram openers.</param>
    /// <param name="firstWord">The first word or token from the diagram header to
    /// evaluate as a potential block diagram opener.</param>
    /// <returns>true if the specified word is a valid block diagram opener for a block diagram; otherwise, false.</returns>
    private static bool IsBlockDiagramOpener(DiagramType diagramType, ReadOnlySpan<char> firstWord)
    {
        if (diagramType != DiagramType.Block)
        {
            return false;
        }

        if (!firstWord.StartsWith(BlockDiagram.BlockOpenerNames.Block, StringComparison.Ordinal))
        {
            return false;
        }

        // Exclude the separate "block-beta" diagram header keyword.
        if (firstWord.Equals("block-beta", StringComparison.Ordinal))
        {
            return false;
        }

        // Allow "block:" or "blockID:" but not "blocks:" / "blockx:".
        int colonIndex = firstWord.IndexOf(Colon);
        return colonIndex == BlockDiagram.BlockOpenerNames.Block.Length;
    }

    /// <summary>
    /// Determines whether the specified word represents a section opener for the given diagram type.
    /// </summary>
    /// <remarks>Section openers are only meaningful for Gantt, User Journey, and Timeline diagram types. For
    /// other diagram types, this method always returns false.</remarks>
    /// <param name="diagramType">The type of diagram to evaluate. Only certain diagram types support section openers.</param>
    /// <param name="firstWord">The first word of the line to check, as a read-only span of characters.</param>
    /// <returns>true if the word is recognized as a section opener for the specified diagram type; otherwise, false.</returns>
    private static bool IsSectionOpener(DiagramType diagramType, ReadOnlySpan<char> firstWord)
    {
        // "section" is only meaningful for these families (avoid false positives in others).
        if (diagramType != DiagramType.Gantt &&
            diagramType != DiagramType.UserJourney &&
            diagramType != DiagramType.Timeline)
        {
            return false;
        }

        return firstWord.Equals(GeneralElementNames.Section, StringComparison.Ordinal);
    }

    /// <summary>
    /// Determines whether the specified word represents a group opener in an architecture diagram.
    /// </summary>
    /// <param name="diagramType">The type of diagram being analyzed. Must be set to
    /// <see cref="DiagramType.ArchitectureBeta"/> to perform the check.</param>
    /// <param name="firstWord">A read-only span containing the first word to evaluate as a potential group opener.</param>
    /// <returns>true if the diagram type is <see cref="DiagramType.ArchitectureBeta"/>
    /// and the first word matches the group opener element name;
    /// otherwise, false.</returns>
    private static bool IsArchitectureGroupOpener(DiagramType diagramType, ReadOnlySpan<char> firstWord)
    {
        if (diagramType != DiagramType.ArchitectureBeta)
        {
            return false;
        }

        return firstWord.Equals(ArchitectureDiagram.ElementNames.Group, StringComparison.Ordinal);
    }

    /// <summary>
    /// Determines whether the specified word represents a C4 boundary opener for the given diagram type.
    /// </summary>
    /// <remarks>This method only performs the boundary opener check for C4 diagram types.
    /// For other diagram types, it always returns false.</remarks>
    /// <param name="diagramType">The type of diagram to evaluate. Must be one of the
    /// C4 diagram types to perform the boundary check.</param>
    /// <param name="firstWord">A read-only span containing the first word to check
    /// for a C4 boundary opener. May include a suffix in
    /// parentheses, which will be ignored during evaluation.</param>
    /// <returns>true if the specified word is recognized as a C4 boundary opener
    /// for the given diagram type; otherwise, false.</returns>
    private static bool IsC4BoundaryOpener(DiagramType diagramType, ReadOnlySpan<char> firstWord)
    {
        if (diagramType != DiagramType.C4Context &&
            diagramType != DiagramType.C4Container &&
            diagramType != DiagramType.C4Component &&
            diagramType != DiagramType.C4Dynamic &&
            diagramType != DiagramType.C4Deployment)
        {
            return false;
        }

        // C4 boundaries: strip "(...)" suffix before lookup.
        int parenIndex = firstWord.IndexOf(LeftParen);
        ReadOnlySpan<char> boundaryKeyword = parenIndex > 0 ? firstWord[..parenIndex] : firstWord;
        return DiagramTypeLookups.C4BoundaryTypesLookup.Contains(boundaryKeyword);
    }

    /// <summary>
    /// Determines whether the specified word is recognized as a requirement block opener for the given diagram type.
    /// </summary>
    /// <param name="diagramType">The type of diagram to evaluate. Only diagrams of type
    /// <see cref="DiagramType.Requirement"/> are considered for block openers.</param>
    /// <param name="firstWord">The first word of the line to check, provided as a <see cref="ReadOnlySpan{char}"/>.</param>
    /// <returns>true if the specified word is a recognized requirement block opener for the given diagram type; otherwise,
    /// false.</returns>
    private static bool IsRequirementBlockOpener(DiagramType diagramType, ReadOnlySpan<char> firstWord)
    {
        if (diagramType != DiagramType.Requirement)
        {
            return false;
        }

        return DiagramTypeLookups.RequirementBlockTypesLookup.Contains(firstWord);
    }

    /// <summary>
    /// Determines whether the specified line represents the opening of an entity block
    /// in an entity-relationship (ER) diagram.
    /// </summary>
    /// <param name="diagramType">The type of diagram being parsed. Must be
    /// <see cref="DiagramType.ERDiagram"/> to match ER entity openers.</param>
    /// <param name="lineTrimmed">A <see cref="ReadOnlySpan{char}"/> containing the trimmed line of
    /// text to evaluate for an ER entity opener.</param>
    /// <returns><see langword="true"/> if the line represents the start of an ER entity block;
    /// otherwise, <see langword="false"/>.</returns>
    private static bool IsErEntityOpener(DiagramType diagramType, ReadOnlySpan<char> lineTrimmed)
    {
        if (diagramType != DiagramType.ERDiagram)
        {
            return false;
        }

        // ER entity blocks: "ENTITY {"
        if (!EndsWithChar(lineTrimmed, LeftBrace))
        {
            return false;
        }

        ReadOnlySpan<char> withoutBrace = lineTrimmed[..^1].TrimEnd();

        // The entity identifier is expected to be a single token.
        if (withoutBrace.IndexOf(SpaceChar) >= 0)
        {
            return false;
        }

        return IsValidEntityIdentifier(withoutBrace);
    }

    /// <summary>
    /// Determines if the specified span represents a valid ER diagram entity identifier (without the trailing brace).
    /// </summary>
    /// <remarks>
    /// A valid identifier must be non-empty, start with a letter or underscore, and contain only
    /// letters, digits, underscores, or hyphens.
    /// </remarks>
    /// <param name="identifier">Identifier span to validate.</param>
    /// <returns>True if valid; otherwise false.</returns>
    private static bool IsValidEntityIdentifier(ReadOnlySpan<char> identifier)
    {
        if (identifier.IsEmpty)
        {
            return false;
        }

        char firstChar = identifier[0];
        if (!char.IsLetter(firstChar) && firstChar != Underscore)
        {
            return false;
        }

        for (int i = 1; i < identifier.Length; i++)
        {
            char c = identifier[i];
            bool ok = char.IsLetterOrDigit(c) || c == Underscore || c == Hyphen;
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    #endregion Mermaid diagram indentation

    #region Diagram type caching

    /// <summary>
    /// Gets the cached diagram type for the document, detecting it if necessary.
    /// </summary>
    /// <remarks>
    /// This implements a two-tier caching strategy:
    /// <list type="number">
    /// <item><description>Fast path: re-check the previously cached declaration line content.</description></item>
    /// <item><description>Slow path: scan for the declaration line and reparse diagram type if it changed.</description></item>
    /// </list>
    /// </remarks>
    /// <param name="document">The text document.</param>
    /// <returns>The detected diagram type (or Unknown if not found).</returns>
    internal DiagramType GetCachedDiagramType(TextDocument document)
    {
        if (_cachedDeclarationLineNumber > 0 && _cachedDeclarationLineNumber <= document.LineCount)
        {
            DocumentLine cachedLine = document.GetLineByNumber(_cachedDeclarationLineNumber);
            string currentContent = GetNormalizedDeclarationText(document, cachedLine);

            if (IsValidDeclaration(currentContent) && currentContent == _cachedDeclarationContent)
            {
                return _cachedDiagramType;
            }
        }

        (int lineNumber, string content) = FindDeclarationLine(document);
        if (lineNumber == 0)
        {
            _cachedDeclarationLineNumber = 0;
            _cachedDeclarationContent = null;
            _cachedDiagramType = DiagramType.Unknown;
            return DiagramType.Unknown;
        }

        bool contentChanged = content != _cachedDeclarationContent;
        if (contentChanged)
        {
            _cachedDiagramType = ParseDiagramTypeFromDeclaration(content);
        }

        _cachedDeclarationLineNumber = lineNumber;
        _cachedDeclarationContent = content;
        return _cachedDiagramType;
    }

    /// <summary>
    /// Finds the diagram declaration line in the document.
    /// </summary>
    /// <remarks>
    /// This method scans for the first non-blank, non-comment line after any frontmatter region.
    /// </remarks>
    /// <param name="document">The document to scan.</param>
    /// <returns>Tuple of (lineNumber, normalizedText), or (0, empty) if not found.</returns>
    private (int LineNumber, string Content) FindDeclarationLine(TextDocument document)
    {
        int startLine = OneBasedFirstLineNumber;

        // Use cached boundaries (updated by UpdateFrontmatterCache).
        if (_cachedFrontmatterEndLine > 0)
        {
            startLine = _cachedFrontmatterEndLine + 1;
        }

        for (int lineNumber = startLine; lineNumber <= document.LineCount; lineNumber++)
        {
            DocumentLine line = document.GetLineByNumber(lineNumber);
            string content = GetNormalizedDeclarationText(document, line);
            if (IsValidDeclaration(content))
            {
                return (lineNumber, content);
            }
        }

        return (0, string.Empty);
    }

    /// <summary>
    /// Gets normalized text for a potential declaration line (trim + collapse whitespace).
    /// </summary>
    /// <param name="document">The document containing the line.</param>
    /// <param name="line">The line to read.</param>
    /// <returns>Normalized line text.</returns>
    private static string GetNormalizedDeclarationText(TextDocument document, DocumentLine line)
    {
        //TODO - this method allocates at least 3 different times:
        // 1. call to GetText
        // 2. Trim() creates a new string if there is leading/trailing whitespace
        // 3. Regex.Replace creates a new string if there is internal whitespace to collapse
        string text = document.GetText(line.Offset, line.Length).Trim();
        if (text.Length > 0)
        {
            text = WhitespaceNormalizationRegex().Replace(text, SingleSpaceString);
        }

        return text;
    }

    /// <summary>
    /// Determines whether the specified text represents a valid declaration line, excluding lines that are empty, null,
    /// or Mermaid comment lines.
    /// </summary>
    /// <remarks>A valid declaration line is any non-empty, non-null string that does not begin with the
    /// Mermaid comment prefix ("%%").</remarks>
    /// <param name="text">The text to evaluate as a potential declaration line. Cannot be null or empty.</param>
    /// <returns>true if the text is a valid declaration line; otherwise, false.</returns>
    private static bool IsValidDeclaration(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        // Mermaid comment lines start with %%
        return !StartsWithChars(text, Percent, Percent);
    }

    /// <summary>
    /// Parses the diagram type from the specified declaration string.
    /// </summary>
    /// <param name="declaration">The declaration string from which to determine the diagram type. Cannot be null.</param>
    /// <returns>A value of the <see cref="DiagramType"/> enumeration that represents the diagram type specified in the
    /// declaration. Returns <see cref="DiagramType.Unknown"/> if the declaration is empty.</returns>
    private static DiagramType ParseDiagramTypeFromDeclaration(string declaration)
    {
        ReadOnlySpan<char> declarationSpan = declaration.AsSpan();
        if (declarationSpan.IsEmpty)
        {
            return DiagramType.Unknown;
        }

        return ParseDiagramType(declarationSpan);
    }

    /// <summary>
    /// Parses the diagram type keyword from the specified line of text and returns the
    /// corresponding <see cref="DiagramType"/> value.
    /// </summary>
    /// <param name="lineText">A read-only span of characters containing the line of text to parse.
    /// The diagram type keyword is expected at the start of the line.</param>
    /// <returns>A <see cref="DiagramType"/> value corresponding to the recognized diagram type keyword.
    /// Returns <see cref="DiagramType.Unknown"/> if the keyword is not recognized or is empty.</returns>
    private static DiagramType ParseDiagramType(ReadOnlySpan<char> lineText)
    {
        int spaceIndex = lineText.IndexOf(SpaceChar);
        ReadOnlySpan<char> keyword = spaceIndex > 0 ? lineText[..spaceIndex] : lineText;
        if (keyword.IsEmpty)
        {
            return DiagramType.Unknown;
        }

        if (DiagramTypeLookups.DiagramNameToTypeMappingLookup.TryGetValue(keyword, out DiagramType diagramType))
        {
            return diagramType;
        }

        return keyword switch
        {
            // StartsWith gives us a little performance boost by narrowing down the checks
            _ when keyword.StartsWith(DiagramTypeNames.Flowchart, StringComparison.Ordinal) ||
                     keyword.StartsWith(DiagramTypeNames.Graph, StringComparison.Ordinal) => GetFlowchartDiagramType(keyword),

            _ when keyword.StartsWith("C4", StringComparison.Ordinal) => GetC4DiagramType(keyword),

            _ when keyword.Equals(DiagramTypeNames.ArchitectureBeta, StringComparison.Ordinal) => DiagramType.ArchitectureBeta,

            _ => DiagramType.Unknown
        };
    }

    /// <summary>
    /// Determines the <see cref="DiagramType"/> corresponding to the specified flowchart-related keyword.
    /// </summary>
    /// <param name="keyword">A read-only span of characters representing the diagram type keyword to evaluate.</param>
    /// <returns>A <see cref="DiagramType"/> value that matches the specified keyword.
    /// Returns <see cref="DiagramType.Unknown"/> if the keyword does not correspond to a known <see cref="DiagramType"/>.</returns>
    private static DiagramType GetFlowchartDiagramType(ReadOnlySpan<char> keyword)
    {
        return keyword switch
        {
            _ when keyword.Equals(DiagramTypeNames.Flowchart, StringComparison.Ordinal) => DiagramType.Flowchart,
            _ when keyword.Equals(DiagramTypeNames.FlowchartElk, StringComparison.Ordinal) => DiagramType.FlowchartElk,
            _ when keyword.Equals(DiagramTypeNames.Graph, StringComparison.Ordinal) => DiagramType.Graph,
            _ => DiagramType.Unknown,
        };
    }

    /// <summary>
    /// Determines the C4 diagram type that corresponds to the specified keyword.
    /// </summary>
    /// <param name="keyword">A read-only span of characters representing the diagram type keyword to evaluate. The comparison is
    /// case-sensitive and must match one of the predefined C4 diagram type names.</param>
    /// <returns>A value of the <see cref="DiagramType"/> enumeration that matches the specified keyword.
    /// Returns <see cref="DiagramType.Unknown"/> if the keyword does not correspond to a known C4 diagram type.</returns>
    private static DiagramType GetC4DiagramType(ReadOnlySpan<char> keyword)
    {
        return keyword switch
        {
            _ when keyword.Equals(DiagramTypeNames.C4Context, StringComparison.Ordinal) => DiagramType.C4Context,
            _ when keyword.Equals(DiagramTypeNames.C4Container, StringComparison.Ordinal) => DiagramType.C4Container,
            _ when keyword.Equals(DiagramTypeNames.C4Component, StringComparison.Ordinal) => DiagramType.C4Component,
            _ when keyword.Equals(DiagramTypeNames.C4Deployment, StringComparison.Ordinal) => DiagramType.C4Deployment,
            _ when keyword.Equals(DiagramTypeNames.C4Dynamic, StringComparison.Ordinal) => DiagramType.C4Dynamic,
            _ => DiagramType.Unknown,
        };
    }

    #endregion Diagram type caching

    #region Frontmatter caching / context

    /// <summary>
    /// Determines the document context (frontmatter or diagram) for the specified line.
    /// </summary>
    /// <remarks>
    /// This method uses cached frontmatter boundaries for O(1) lookup and updates the cache when needed.
    /// </remarks>
    /// <param name="document">The document to evaluate.</param>
    /// <param name="lineNumber">One-based line number.</param>
    /// <returns>The <see cref="DocumentContext"/> of the specified line.</returns>
    internal DocumentContext DetermineContext(TextDocument document, int lineNumber)
    {
        UpdateFrontmatterCache(document);
        return DetermineContextFromCache(lineNumber);
    }

    /// <summary>
    /// Determines the <see cref="DocumentContext"/> for a specified line number based on cached frontmatter boundaries.
    /// </summary>
    /// <remarks>This method uses cached values for the frontmatter start and end lines to efficiently
    /// determine the context of a line. If the frontmatter boundaries are not set, the method treats all lines as part
    /// of the diagram context.</remarks>
    /// <param name="lineNumber">The one-based line number for which to determine the document context.</param>
    /// <returns>A value from the <see cref="DocumentContext"/> enumeration indicating the context of the specified line.
    /// Returns <see cref="DocumentContext.Diagram"/>,  <see cref="DocumentContext.FrontmatterStart"/>,
    /// <see cref="DocumentContext.FrontmatterEnd"/>, or <see cref="DocumentContext.Frontmatter"/>
    /// depending on the line's position relative to the cached
    /// frontmatter start and end lines.</returns>
    private DocumentContext DetermineContextFromCache(int lineNumber)
    {
        if (_cachedFrontmatterStartLine < 0)
        {
            return DocumentContext.Diagram;
        }

        if (lineNumber == _cachedFrontmatterStartLine)
        {
            return DocumentContext.FrontmatterStart;
        }

        if (_cachedFrontmatterEndLine > 0 && lineNumber == _cachedFrontmatterEndLine)
        {
            return DocumentContext.FrontmatterEnd;
        }

        if (lineNumber > _cachedFrontmatterStartLine &&
            (_cachedFrontmatterEndLine < 0 || lineNumber < _cachedFrontmatterEndLine))
        {
            return DocumentContext.Frontmatter;
        }

        return DocumentContext.Diagram;
    }

    /// <summary>
    /// Updates the cached frontmatter boundaries for the specified text document if the cache is out of date.
    /// </summary>
    /// <remarks>This method checks whether the frontmatter cache for the given document can be reused based
    /// on the current document version and scan limits. If the cache is invalid or outdated, it rescans the document to
    /// update the cached frontmatter boundaries.</remarks>
    /// <param name="document">The text document for which to update the frontmatter cache. Cannot be null.</param>
    private void UpdateFrontmatterCache(TextDocument document)
    {
        ITextSourceVersion? currentVersion = document.Version;
        if (CanReuseFrontmatterCache(currentVersion, MaxFrontmatterScanLines, document))
        {
            return;
        }

        RescanFrontmatterBoundaries(document, MaxFrontmatterScanLines);
        _cachedFrontmatterVersion = currentVersion;
    }

    /// <summary>
    /// Determines whether the cached frontmatter can be reused for the specified document version and scan range.
    /// </summary>
    /// <remarks>The cache is considered reusable only if the document version matches or if there are no
    /// changes detected in the frontmatter region within the specified scan range. If the cache is reused after
    /// confirming no changes, the cached version is updated to the current version.</remarks>
    /// <param name="currentVersion">The current version of the text source to compare against the cached
    /// frontmatter version. Can be null.</param>
    /// <param name="maxFrontmatterScanLines">The maximum number of lines to scan for frontmatter changes. Must be non-negative.</param>
    /// <param name="document">The text document to check for frontmatter changes.</param>
    /// <returns>true if the cached frontmatter is valid for the specified document version and scan range; otherwise, false.</returns>
    private bool CanReuseFrontmatterCache(ITextSourceVersion? currentVersion, int maxFrontmatterScanLines, TextDocument document)
    {
        if (_cachedFrontmatterVersion is null || currentVersion is null)
        {
            return false;
        }

        if (!_cachedFrontmatterVersion.BelongsToSameDocumentAs(currentVersion))
        {
            return false;
        }

        if (_cachedFrontmatterVersion.CompareAge(currentVersion) == 0)
        {
            return true;
        }

        bool frontmatterChanged = HasChangesInFrontmatterRegion(currentVersion, maxFrontmatterScanLines, document);
        if (!frontmatterChanged)
        {
            _cachedFrontmatterVersion = currentVersion;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Determines whether there are any changes in the frontmatter region between the cached version and the specified
    /// text source version.
    /// </summary>
    /// <remarks>The frontmatter region is determined based on a cached end line or the specified maximum scan
    /// lines. If an error occurs while checking for changes, the method conservatively returns true to indicate that
    /// changes may be present.</remarks>
    /// <param name="currentVersion">The current version of the text source to compare against the cached frontmatter version.</param>
    /// <param name="maxFrontmatterScanLines">The maximum number of lines to scan when determining the extent of the frontmatter region. Must be greater than
    /// zero.</param>
    /// <param name="document">The text document containing the content to be analyzed for frontmatter changes.</param>
    /// <returns>true if any changes are detected within the frontmatter region; otherwise, false. Returns true if an error
    /// occurs during change detection.</returns>
    private bool HasChangesInFrontmatterRegion(ITextSourceVersion currentVersion, int maxFrontmatterScanLines, TextDocument document)
    {
        bool hasCachedEndLine = _cachedFrontmatterEndLine > 0;
        int regionEndLine = hasCachedEndLine
            ? _cachedFrontmatterEndLine
            : Math.Min(maxFrontmatterScanLines, document.LineCount);

        try
        {
            // Avoid LINQ allocations in a hot-ish path by using a foreach loop instead of .Any()
            foreach (TextChangeEventArgs change in _cachedFrontmatterVersion!.GetChangesTo(currentVersion))
            {
                if (IsChangeInFrontmatterRegion(change, regionEndLine, document))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            // Keep this single trace for diagnostics; treat as changed to be safe.
            Debug.WriteLine($"[MermaidIndentationStrategy]:HasChangesInFrontmatterRegion: Error checking frontmatter changes: {ex}");
            return true;
        }
    }

    /// <summary>
    /// Determines whether the specified text change affects the frontmatter region of the document.
    /// </summary>
    /// <param name="change">The text change event data containing the offset and length of the change.</param>
    /// <param name="frontmatterRegionEndLine">The one-based line number indicating the end of the frontmatter region.</param>
    /// <param name="document">The text document in which the change occurred.</param>
    /// <returns>true if the change overlaps with the frontmatter region; otherwise, false.</returns>
    private static bool IsChangeInFrontmatterRegion(TextChangeEventArgs change, int frontmatterRegionEndLine, TextDocument document)
    {
        int changeStartLine = document.GetLineByOffset(change.Offset).LineNumber;
        int changeEndOffset = change.Offset + change.RemovalLength;

        int changeEndLine = changeEndOffset < document.TextLength
            ? document.GetLineByOffset(changeEndOffset).LineNumber
            : document.LineCount;

        return changeStartLine <= frontmatterRegionEndLine && changeEndLine >= OneBasedFirstLineNumber;
    }

    /// <summary>
    /// Scans the specified document to identify the start and end line numbers of the frontmatter section, up to a
    /// maximum number of lines.
    /// </summary>
    /// <remarks>This method updates cached values for the frontmatter start and end line numbers based on the
    /// presence of delimiter lines. Only the first two delimiter lines found within the specified scan range are
    /// considered as the frontmatter boundaries.</remarks>
    /// <param name="document">The text document to scan for frontmatter boundaries.</param>
    /// <param name="maxFrontmatterScanLines">The maximum number of lines to scan from the beginning
    /// of the document when searching for frontmatter delimiters. Must be greater than zero.</param>
    private void RescanFrontmatterBoundaries(TextDocument document, int maxFrontmatterScanLines)
    {
        _cachedFrontmatterStartLine = -1;
        _cachedFrontmatterEndLine = -1;

        int maxLinesToScan = Math.Min(document.LineCount, maxFrontmatterScanLines);
        for (int i = OneBasedFirstLineNumber; i <= maxLinesToScan; i++)
        {
            DocumentLine scanLine = document.GetLineByNumber(i);
            if (!IsFrontmatterDelimiterLine(document, scanLine))
            {
                continue;
            }

            if (_cachedFrontmatterStartLine < 0)
            {
                _cachedFrontmatterStartLine = i;
            }
            else
            {
                _cachedFrontmatterEndLine = i;
                break;
            }
        }
    }

    #endregion Frontmatter caching / context

    #region Whitespace and indentation helpers

    /// <summary>
    /// Replaces the leading whitespace on the specified line with the given indentation string.
    /// </summary>
    /// <remarks>
    /// If the current indentation already matches the desired indentation, the document is not modified.
    /// </remarks>
    /// <param name="document">The document containing the line to modify.</param>
    /// <param name="line">The line whose indentation will be updated.</param>
    /// <param name="desiredIndentation">The indentation string to apply.</param>
    private static void ApplyIndentation(TextDocument document, DocumentLine line, string desiredIndentation)
    {
        ISegment indentationSegment = TextUtilities.GetWhitespaceAfter(document, line.Offset);

        // Fast reject: different length => must replace.
        if (indentationSegment.Length != desiredIndentation.Length)
        {
            document.Replace(indentationSegment.Offset,
                indentationSegment.Length,
                desiredIndentation,
                OffsetChangeMappingType.RemoveAndInsert);
            return;
        }

        // Same length: compare content without allocating.
        int offset = indentationSegment.Offset;
        for (int i = 0; i < indentationSegment.Length; i++)
        {
            if (document.GetCharAt(offset + i) != desiredIndentation[i])
            {
                document.Replace(indentationSegment.Offset,
                    indentationSegment.Length,
                    desiredIndentation,
                    OffsetChangeMappingType.RemoveAndInsert);
                return;
            }
        }

        // Identical => do nothing.
    }

    /// <summary>
    /// Reduces the indentation by one level (based on the editor indentation settings).
    /// </summary>
    /// <param name="currentIndentation">The current indentation string.</param>
    /// <returns>The indentation reduced by one unit, or empty string if it cannot be reduced.</returns>
    private string DedentOnce(string currentIndentation)
    {
        if (string.IsNullOrEmpty(currentIndentation))
        {
            return string.Empty;
        }

        // If a tab exists at the end of the indentation, treat it as one unit.
        if (currentIndentation[^1] == TabChar)
        {
            return currentIndentation[..^1];
        }

        // Otherwise remove up to _indentationSize trailing spaces.
        int remove = 0;
        for (int i = currentIndentation.Length - 1; i >= 0 && remove < _indentationSize; i--)
        {
            if (currentIndentation[i] != SpaceChar)
            {
                break;
            }

            remove++;
        }

        return remove > 0 ? currentIndentation[..^remove] : string.Empty;
    }

    /// <summary>
    /// Returns the leading indentation characters from the specified line as a string.
    /// </summary>
    /// <remarks>
    /// For common indentation patterns (tabs, pure 2-space indents, pure 4-space indents),
    /// a pre-allocated cached string is returned to avoid allocations.
    /// </remarks>
    /// <param name="lineSpan">The full line span (including indentation and content).</param>
    /// <returns>A string of leading indentation characters.</returns>
    private static string GetIndentationString(ReadOnlySpan<char> lineSpan)
    {
        int indentLength = GetIndentationLength(lineSpan);
        if (indentLength == 0)
        {
            return string.Empty;
        }

        ReadOnlySpan<char> indentSpan = lineSpan[..indentLength];

        // Pure tabs
        if (indentSpan[0] == TabChar && IsAllSameChar(indentSpan, TabChar))
        {
            int tabCount = indentLength;
            if (tabCount < _preAllocatedIndentsTab.Length)
            {
                return _preAllocatedIndentsTab[tabCount];
            }
        }
        // Pure spaces
        else if (indentSpan[0] == SpaceChar && IsAllSameChar(indentSpan, SpaceChar))
        {
            if (indentLength % 2 == 0)
            {
                int level2 = indentLength / 2;
                if (level2 < _preAllocatedIndents2Spaces.Length)
                {
                    return _preAllocatedIndents2Spaces[level2];
                }
            }

            if (indentLength % 4 == 0)
            {
                int level4 = indentLength / 4;
                if (level4 < _preAllocatedIndents4Spaces.Length)
                {
                    return _preAllocatedIndents4Spaces[level4];
                }
            }
        }

        // Fallback allocation (non-standard indentation patterns).
        return indentSpan.ToString();
    }

    /// <summary>
    /// Calculates the number of leading whitespace characters at the start of the specified character span.
    /// </summary>
    /// <param name="lineSpan">A read-only span of characters representing the line to analyze for leading whitespace.</param>
    /// <returns>The number of consecutive whitespace characters at the beginning of the span. Returns 0 if the span starts with
    /// a non-whitespace character or is empty.</returns>
    private static int GetIndentationLength(ReadOnlySpan<char> lineSpan)
    {
        int count = 0;
        for (int i = 0; i < lineSpan.Length; i++)
        {
            if (char.IsWhiteSpace(lineSpan[i]))
            {
                count++;
            }
            else
            {
                break;
            }
        }

        return count;
    }

    /// <summary>
    /// Determines whether all characters in the specified span are equal to the given character.
    /// </summary>
    /// <param name="span">The span of characters to examine.</param>
    /// <param name="c">The character to compare each element of the span against.</param>
    /// <returns>true if every character in the span is equal to the specified character; otherwise, false.</returns>
    private static bool IsAllSameChar(ReadOnlySpan<char> span, char c)
    {
        for (int i = 0; i < span.Length; i++)
        {
            if (span[i] != c)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Determines whether the specified span begins with the given two characters in order.
    /// </summary>
    /// <param name="span">The span of characters to examine.</param>
    /// <param name="first">The character to compare to the first character of the span.</param>
    /// <param name="second">The character to compare to the second character of the span.</param>
    /// <returns><see langword="true"/> if the span is at least two characters long and its first two characters match <paramref
    /// name="first"/> and <paramref name="second"/> respectively; otherwise, <see langword="false"/>.</returns>
    private static bool StartsWithChars(ReadOnlySpan<char> span, char first, char second)
    {
        return span.Length >= 2 && span[0] == first && span[1] == second;
    }

    /// <summary>
    /// Determines whether the specified read-only character span ends with the specified character.
    /// </summary>
    /// <param name="span">The read-only span of characters to examine.</param>
    /// <param name="c">The character to compare to the last character of the span.</param>
    /// <returns>true if the last character of the span equals the specified character; otherwise, false. Returns false if the
    /// span is empty.</returns>
    private static bool EndsWithChar(ReadOnlySpan<char> span, char c)
    {
        if (span.IsEmpty)
        {
            return false;
        }

        return span[^1] == c;
    }

    /// <summary>
    /// Creates a cache of indentation strings for each indentation level up to the specified maximum.
    /// </summary>
    /// <param name="indent">A single indentation unit (e.g., 2 spaces or a tab).</param>
    /// <param name="maxLevels">Maximum number of levels to cache.</param>
    /// <returns>An array where index N is N copies of <paramref name="indent"/>.</returns>
    private static string[] CreateIndentationCache(string indent, int maxLevels)
    {
        string[] cache = new string[maxLevels + 1];
        cache[0] = string.Empty;

        for (int i = 1; i <= maxLevels; i++)
        {
            cache[i] = cache[i - 1] + indent;
        }

        return cache;
    }

    /// <summary>
    /// Determines whether a document line contains only whitespace characters (excluding line delimiters).
    /// </summary>
    /// <remarks>
    /// Uses <see cref="TextUtilities.GetLeadingWhitespace(TextDocument, DocumentLine)"/> to avoid allocating strings
    /// for "blank line" checks.
    /// </remarks>
    private static bool IsLineWhitespaceOnly(TextDocument document, DocumentLine line)
    {
        // DocumentLine.Length excludes the line delimiter, so this correctly detects lines that are only spaces/tabs.
        return TextUtilities.GetLeadingWhitespace(document, line).Length == line.Length;
    }

    /// <summary>
    /// Determines whether the specified line in the document consists solely of the frontmatter delimiter, ignoring
    /// leading and trailing whitespace.
    /// </summary>
    /// <remarks>This method is typically used to identify lines that mark the start or end of a frontmatter
    /// section, such as lines containing only '---' in Markdown documents. Whitespace before or after the delimiter is
    /// ignored.</remarks>
    /// <param name="document">The text document containing the line to evaluate.</param>
    /// <param name="line">The line within the document to check for the frontmatter delimiter.</param>
    /// <returns>true if the trimmed content of the line matches the frontmatter delimiter; otherwise, false.</returns>
    private static bool IsFrontmatterDelimiterLine(TextDocument document, DocumentLine line)
    {
        // Match document.GetText(...).AsSpan().Trim().SequenceEqual(Frontmatter.Delimiter) without allocations.
        int offset = line.Offset;
        int length = line.Length;

        int start = 0;
        while (start < length && char.IsWhiteSpace(document.GetCharAt(offset + start)))
        {
            start++;
        }

        int end = length - 1;
        while (end >= start && char.IsWhiteSpace(document.GetCharAt(offset + end)))
        {
            end--;
        }

        int trimmedLen = end - start + 1;
        if (trimmedLen != Frontmatter.Delimiter.Length)
        {
            return false;
        }

        // Frontmatter.Delimiter is expected to be "---"
        for (int i = 0; i < trimmedLen; i++)
        {
            if (document.GetCharAt(offset + start + i) != Frontmatter.Delimiter[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Determines whether the specified line consists solely of a closing keyword, such as a right brace ('}') or the
    /// word 'end', optionally followed by whitespace.
    /// </summary>
    /// <remarks>This method ignores leading and trailing whitespace when evaluating the line. It is typically
    /// used to identify lines that mark the end of a code block in languages that use '}' or 'end' as closing
    /// constructs.</remarks>
    /// <param name="document">The text document containing the line to examine.</param>
    /// <param name="line">The line within the document to check for a closing keyword.</param>
    /// <returns>true if the line contains only a closing keyword (either '}' or 'end', possibly followed by whitespace);
    /// otherwise, false.</returns>
    private static bool IsClosingKeywordOnLine(TextDocument document, DocumentLine line)
    {
        int offset = line.Offset;
        int length = line.Length;

        // Trim start
        int start = 0;
        while (start < length && char.IsWhiteSpace(document.GetCharAt(offset + start)))
        {
            start++;
        }

        // Trim end
        int end = length - 1;
        while (end >= start && char.IsWhiteSpace(document.GetCharAt(offset + end)))
        {
            end--;
        }

        int trimmedLen = end - start + 1;
        if (trimmedLen <= 0)
        {
            return false;
        }

        // "}"
        if (trimmedLen == 1 && document.GetCharAt(offset + start) == RightBrace)
        {
            return true;
        }

        // "end" or "end <whitespace>..."
        if (trimmedLen >= 3)
        {
            char c0 = document.GetCharAt(offset + start + 0);
            char c1 = document.GetCharAt(offset + start + 1);
            char c2 = document.GetCharAt(offset + start + 2);

            if (c0 == 'e' && c1 == 'n' && c2 == 'd')
            {
                if (trimmedLen == 3)
                {
                    return true;
                }

                char next = document.GetCharAt(offset + start + 3);
                return char.IsWhiteSpace(next);
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether the specified line in the document begins with a sequence continuation keyword.
    /// </summary>
    /// <remarks>A sequence continuation keyword is identified if the first non-whitespace word on the line
    /// matches a known keyword such as 'else' or 'and'. This method does not allocate substrings when performing the
    /// comparison.</remarks>
    /// <param name="document">The text document to examine for sequence continuation keywords.</param>
    /// <param name="line">The line within the document to check for a sequence continuation keyword at the start.</param>
    /// <returns>true if the line starts with a recognized sequence continuation keyword; otherwise, false.</returns>
    private static bool IsSequenceContinuationKeywordOnLine(TextDocument document, DocumentLine line)
    {
        int offset = line.Offset;
        int length = line.Length;

        // Find first non-whitespace
        int start = 0;
        while (start < length && char.IsWhiteSpace(document.GetCharAt(offset + start)))
        {
            start++;
        }

        if (start >= length)
        {
            return false;
        }

        // Find first word end
        int wordEnd = start;
        while (wordEnd < length && !char.IsWhiteSpace(document.GetCharAt(offset + wordEnd)))
        {
            wordEnd++;
        }

        int wordLen = wordEnd - start;
        if (wordLen <= 0)
        {
            return false;
        }

        // Compare with known keywords without allocating a substring.
        ReadOnlySpan<char> elseKw = SequenceDiagram.BlockOpenerNames.Else.AsSpan();
        ReadOnlySpan<char> andKw = SequenceDiagram.BlockOpenerNames.And.AsSpan();

        if (wordLen == elseKw.Length && MatchWord(document, offset + start, elseKw))
        {
            return true;
        }

        if (wordLen == andKw.Length && MatchWord(document, offset + start, andKw))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Determines whether the specified span of characters in the document matches the given word exactly.
    /// </summary>
    /// <remarks>The comparison is case-sensitive and requires an exact match of all characters in the
    /// specified span. No normalization or culture-specific comparison is performed.</remarks>
    /// <param name="document">The text document to search within.</param>
    /// <param name="startOffset">The zero-based character offset in the document at which to begin matching.</param>
    /// <param name="word">The word to compare against the document content, represented as a read-only span of characters.</param>
    /// <returns>true if the sequence of characters in the document at the specified offset matches the given word; otherwise,
    /// false.</returns>
    private static bool MatchWord(TextDocument document, int startOffset, ReadOnlySpan<char> word)
    {
        for (int i = 0; i < word.Length; i++)
        {
            if (document.GetCharAt(startOffset + i) != word[i])
            {
                return false;
            }
        }

        return true;
    }

    #endregion Whitespace and indentation helpers
}
#pragma warning restore S3267 // Loops should be simplified with "LINQ" expressions.
