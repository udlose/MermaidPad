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
using System.Diagnostics.CodeAnalysis;

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
/// <para>
/// Document structure analysis (frontmatter boundaries, diagram type detection) is delegated to
/// <see cref="DocumentAnalyzer"/> which maintains a shared cache across all editor features.
/// </para>
/// </remarks>
#pragma warning disable IDE0078
[SuppressMessage("Style", "IDE0078:Use pattern matching", Justification = "Performance and code clarity")]
[SuppressMessage("ReSharper", "MergeIntoPattern", Justification = "Performance and code clarity")]
#pragma warning disable S3267 // Loops should be simplified with "LINQ" expressions. This code is performance-sensitive.
internal sealed class MermaidIndentationStrategy : DefaultIndentationStrategy
{
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

    private readonly DocumentAnalyzer _documentAnalyzer;
    private readonly string _indentationString;
    private readonly int _indentationSize;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="MermaidIndentationStrategy"/> class using the specified editor options.
    /// </summary>
    /// <param name="documentAnalyzer">The document analyzer for determining line context (frontmatter vs. diagram). Cannot be null.</param>
    /// <param name="options">The text editor options containing indentation settings. Cannot be <see langword="null"/>.</param>
    /// <remarks>
    /// This constructor respects the user's configured indentation preferences from the editor options,
    /// including whether to use tabs or spaces and the indentation size.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="documentAnalyzer"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> is <see langword="null"/>.</exception>
    internal MermaidIndentationStrategy(DocumentAnalyzer documentAnalyzer, TextEditorOptions options)
    {
        ArgumentNullException.ThrowIfNull(documentAnalyzer);
        ArgumentNullException.ThrowIfNull(options);

        _documentAnalyzer = documentAnalyzer;
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

        // Get frontmatter boundaries once (warms the cache in DocumentAnalyzer)
        int frontmatterStartLine = _documentAnalyzer.GetFrontmatterStartLine(document);
        int frontmatterEndLine = _documentAnalyzer.GetFrontmatterEndLine(document);

        // Get diagram type from analyzer (uses shared cache)
        DiagramType diagramType = _documentAnalyzer.GetDiagramType(document);

        IndentLineInternal(document, line, diagramType, frontmatterStartLine, frontmatterEndLine);
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

        beginLine = Math.Max(DocumentAnalyzer.OneBasedFirstLineNumber, beginLine);
        endLine = Math.Min(document.LineCount, endLine);
        if (beginLine > endLine)
        {
            return;
        }

        // Get frontmatter boundaries ONCE for the batch (warms the cache in DocumentAnalyzer)
        int frontmatterStartLine = _documentAnalyzer.GetFrontmatterStartLine(document);
        int frontmatterEndLine = _documentAnalyzer.GetFrontmatterEndLine(document);

        // Keep YAML valid (low-risk formatting; does not parse YAML and does not remove comments).
        EnsureYamlSpacingInFrontmatter(document, beginLine, endLine, frontmatterStartLine, frontmatterEndLine);

        // Get diagram type from analyzer (uses shared cache)
        DiagramType diagramType = _documentAnalyzer.GetDiagramType(document);

        for (int lineNumber = beginLine; lineNumber <= endLine; lineNumber++)
        {
            DocumentLine line = document.GetLineByNumber(lineNumber);
            IndentLineInternal(document, line, diagramType, frontmatterStartLine, frontmatterEndLine);
        }
    }

    #region Indentation core

    /// <summary>
    /// Performs indentation for a single line using the already-cached frontmatter boundaries and diagram type.
    /// </summary>
    /// <param name="document">The text document being edited.</param>
    /// <param name="line">The current line to indent.</param>
    /// <param name="diagramType">The cached diagram type for the document.</param>
    /// <param name="frontmatterStartLine">The cached frontmatter start line (-1 if no frontmatter).</param>
    /// <param name="frontmatterEndLine">The cached frontmatter end line (-1 if unclosed).</param>
    private void IndentLineInternal(TextDocument document, DocumentLine line, DiagramType diagramType,
        int frontmatterStartLine, int frontmatterEndLine)
    {
        // Use DocumentAnalyzer for context determination (cache is already warm)
        DocumentContext context = _documentAnalyzer.DetermineLineContext(document, line.LineNumber);

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
            desiredIndentation = CalculateFrontmatterIndentation(document, line, frontmatterStartLine);
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
    /// <param name="frontmatterStartLine">The cached frontmatter start line.</param>
    /// <returns>The indentation string for the current frontmatter line.</returns>
    private string CalculateFrontmatterIndentation(TextDocument document, DocumentLine currentLine, int frontmatterStartLine)
    {
        DocumentLine? effectivePrev = FindEffectivePreviousNonBlankLineInFrontmatter(document, currentLine, frontmatterStartLine);
        if (effectivePrev is null)
        {
            return string.Empty;
        }

        if (effectivePrev.LineNumber == frontmatterStartLine)
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
    /// <param name="frontmatterStartLine">The cached frontmatter start line.</param>
    /// <returns>The effective previous line, or null if none exists.</returns>
    private static DocumentLine? FindEffectivePreviousNonBlankLineInFrontmatter(TextDocument document, DocumentLine currentLine,
        int frontmatterStartLine)
    {
        DocumentLine? scan = currentLine.PreviousLine;
        while (scan is not null)
        {
            int scanLineNumber = scan.LineNumber;
            if (frontmatterStartLine > 0 && scanLineNumber <= frontmatterStartLine)
            {
                return scan;
            }

            if (!DocumentAnalyzer.IsLineBlank(document, scan))
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
        return trimmedStart[0] == Hash || DocumentAnalyzer.StartsWithChars(trimmedStart, Percent, Percent);
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

        return DocumentAnalyzer.StartsWithChars(afterColon, Percent, Percent);
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
    /// <param name="frontmatterStartLine">The cached frontmatter start line (-1 if no frontmatter).</param>
    /// <param name="frontmatterEndLine">The cached frontmatter end line (-1 if unclosed).</param>
    private static void EnsureYamlSpacingInFrontmatter(TextDocument document, int beginLine, int endLine,
        int frontmatterStartLine, int frontmatterEndLine)
    {
        if (frontmatterStartLine < 0)
        {
            return;
        }

        int frontmatterEnd = frontmatterEndLine > 0
            ? frontmatterEndLine
            : Math.Min(document.LineCount, MaxFrontmatterScanLines);

        int from = Math.Max(beginLine, frontmatterStartLine + 1);
        int to = Math.Min(endLine, frontmatterEnd - 1);
        if (from > to)
        {
            return;
        }

        // Walk backwards to keep insert offsets stable across the range.
        // When inserting or deleting text, changes at higher offsets (later lines)
        // do not affect the offsets of earlier lines. By iterating from the end
        // towards the start, we ensure that each modification does not invalidate
        // the offsets for lines that have not yet been processed.
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
        document.Insert(insertOffset, DocumentAnalyzer.SingleSpaceString);
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
        if (previousTrimmed.IsEmpty || DocumentAnalyzer.StartsWithChars(previousTrimmed, Percent, Percent))
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
        if (!DocumentAnalyzer.EndsWithChar(lineTrimmed, LeftBrace))
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
        if (!DocumentAnalyzer.EndsWithChar(lineTrimmed, LeftBrace))
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
        if (!DiagramTypeLookups.C4DiagramTypes.Contains(diagramType))
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
        if (!DocumentAnalyzer.EndsWithChar(lineTrimmed, LeftBrace))
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
        if (indentSpan[0] == TabChar && DocumentAnalyzer.IsAllSameChar(indentSpan, TabChar))
        {
            int tabCount = indentLength;
            if (tabCount < _preAllocatedIndentsTab.Length)
            {
                return _preAllocatedIndentsTab[tabCount];
            }
        }
        // Pure spaces
        else if (indentSpan[0] == SpaceChar && DocumentAnalyzer.IsAllSameChar(indentSpan, SpaceChar))
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

        if (wordLen == elseKw.Length && DocumentAnalyzer.MatchWord(document, offset + start, elseKw))
        {
            return true;
        }

        if (wordLen == andKw.Length && DocumentAnalyzer.MatchWord(document, offset + start, andKw))
        {
            return true;
        }

        return false;
    }

    #endregion Whitespace and indentation helpers
}
#pragma warning restore S3267 // Loops should be simplified with "LINQ" expressions.
