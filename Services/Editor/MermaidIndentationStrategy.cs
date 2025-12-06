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
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace MermaidPad.Services.Editor;

/// <summary>
/// Provides smart indentation for Mermaid diagrams and YAML frontmatter in the AvaloniaEdit text editor.
/// </summary>
/// <remarks>
/// <para>
/// This strategy handles indentation for all Mermaid diagram types including flowcharts, sequence diagrams,
/// state diagrams, class diagrams, ER diagrams, Gantt charts, and more. It also properly indents YAML
/// frontmatter sections delimited by <c>---</c>.
/// </para>
/// <para>
/// The strategy supports auto-dedent when typing block-closing keywords like <c>end</c> or <c>}</c>,
/// providing a more intuitive editing experience similar to IDE behavior for programming languages.
/// </para>
/// <para>
/// Supported diagram types:
/// <list type="bullet">
///     <item>flowchart, flowchart-elk, graph (with subgraph/end)</item>
///     <item>sequenceDiagram (with loop/alt/opt/par/critical/break/rect/end)</item>
///     <item>stateDiagram, stateDiagram-v2 (with state/end)</item>
///     <item>classDiagram (with class blocks)</item>
///     <item>erDiagram (with entity blocks)</item>
///     <item>gantt (with section)</item>
///     <item>pie, quadrantChart, xychart-beta, radar-beta (flat structure)</item>
///     <item>mindmap, treemap-beta (indentation-based hierarchy)</item>
///     <item>timeline, journey (with section)</item>
///     <item>gitGraph (with branch/checkout/merge)</item>
///     <item>C4Context, C4Container, C4Component, C4Dynamic (with boundaries)</item>
///     <item>architecture-beta (with group)</item>
///     <item>block-beta (with block:/end)</item>
///     <item>requirementDiagram (with requirement/element blocks)</item>
///     <item>sankey-beta, packet-beta (flat structure)</item>
///     <item>kanban (indentation-based columns)</item>
/// </list>
/// </para>
/// </remarks>
public sealed partial class MermaidIndentationStrategy : DefaultIndentationStrategy
{
    private const char TabChar = '\t';
    private const char SpaceChar = ' ';
    private const string DefaultIndentation = "  "; // 2 spaces - standard for YAML/Mermaid
    private const int DefaultIndentationSize = 2;
    private readonly string _indentationString;
    private readonly int _indentationSize;

    private int _cachedDeclarationLineNumber;  // 0 = not cached
    private string? _cachedDeclarationContent; // null = not cached
    private DiagramType _cachedDiagramType = DiagramType.Unknown;

    /// <summary>
    /// Gets the delimiter string used to identify the boundaries of frontmatter sections in a document.
    /// </summary>
    /// <remarks>The delimiter is typically used in formats such as Markdown to separate metadata from
    /// content. This property provides a read-only span containing the delimiter value.</remarks>
    private static ReadOnlySpan<char> FrontmatterDelimiter => "---";

    // Cached frontmatter boundaries to avoid O(n) scanning on every line
    private int _cachedFrontmatterStartLine = -1;  // -1 means no frontmatter
    private int _cachedFrontmatterEndLine = -1;    // -1 means frontmatter not closed
    private ITextSourceVersion? _cachedFrontmatterVersion;

    #region Pre-allocated whitespace to avoid ToString() allocations

    private const int MaxPreAllocatedIndentLevels = 20;
    private static readonly string[] _preAllocatedIndents2Spaces = CreateIndentationCache("  ", MaxPreAllocatedIndentLevels);
    private static readonly string[] _preAllocatedIndents4Spaces = CreateIndentationCache("    ", MaxPreAllocatedIndentLevels);
    private static readonly string[] _preAllocatedIndentsTab = CreateIndentationCache("\t", MaxPreAllocatedIndentLevels);

    #endregion Pre-allocated whitespace

    #region FrozenSet and FrozenDictionary Declarations

    /// <summary>
    /// Provides a pre-initialized, case-insensitive set containing the names of all supported diagram declarations.
    /// </summary>
    /// <remarks>This set is used to efficiently determine whether a given string corresponds to a recognized
    /// diagram type. The comparison is performed using ordinal, case-insensitive matching. The set includes both stable
    /// and beta diagram types.</remarks>
    private static readonly FrozenSet<string> _diagramDeclarations = FrozenSet.ToFrozenSet(
    [
        "flowchart", "flowchart-elk", "graph",
        "sequenceDiagram",
        "stateDiagram", "stateDiagram-v2",
        "classDiagram",
        "erDiagram",
        "gantt",
        "pie",
        "mindmap",
        "timeline",
        "journey",
        "gitGraph",
        "C4Context", "C4Container", "C4Component", "C4Dynamic",
        "architecture-beta",
        "block-beta",
        "requirementDiagram",
        "sankey-beta",
        "xychart-beta",
        "quadrantChart",
        "packet-beta",
        "kanban",
        "radar-beta",
        "treemap-beta"
    ], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Represents a set of keywords that indicate the opening of a sequence block in the parsed language. The set is
    /// case-insensitive.
    /// </summary>
    /// <remarks>This set is used to efficiently determine whether a given string marks the start of a
    /// sequence block, such as 'loop', 'alt', or 'opt'. The use of a frozen set ensures fast lookups and thread
    /// safety.</remarks>
    private static readonly FrozenSet<string> _sequenceBlockOpeners = FrozenSet.ToFrozenSet(
    [
        "loop", "alt", "else", "opt", "par", "and", "critical", "break", "rect"
    ], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Maps exact diagram type keywords to their corresponding <see cref="DiagramType"/> values.
    /// Uses FrozenDictionary for O(1) case-insensitive lookup without string allocation.
    /// </summary>
    private static readonly FrozenDictionary<string, DiagramType> _exactDiagramTypes = new Dictionary<string, DiagramType>
    {
        ["sequenceDiagram"] = DiagramType.Sequence,
        ["classDiagram"] = DiagramType.Class,
        ["erDiagram"] = DiagramType.EntityRelationship,
        ["gantt"] = DiagramType.Gantt,
        ["pie"] = DiagramType.Pie,
        ["mindmap"] = DiagramType.Mindmap,
        ["timeline"] = DiagramType.Timeline,
        ["journey"] = DiagramType.Journey,
        ["gitGraph"] = DiagramType.GitGraph,
        ["requirementDiagram"] = DiagramType.Requirement,
        ["quadrantChart"] = DiagramType.Quadrant,
        ["kanban"] = DiagramType.Kanban
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Represents a set of C4 model boundary type names used for case-insensitive comparisons.
    /// </summary>
    /// <remarks>This set includes common boundary types such as "Enterprise_Boundary", "System_Boundary",
    /// "Container_Boundary", and "Boundary". The set is frozen for efficient lookups and uses ordinal case-insensitive
    /// string comparison.</remarks>
    private static readonly FrozenSet<string> _c4BoundaryTypes = FrozenSet.ToFrozenSet(
    [
        "Enterprise_Boundary",
        "System_Boundary",
        "Container_Boundary",
        "Boundary"
    ], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Contains the set of block type names that are recognized as requirement-related elements.
    /// </summary>
    /// <remarks>The set includes common requirement block types such as 'requirement',
    /// 'functionalRequirement', and 'designConstraint'. Comparisons are performed using case-insensitive ordinal
    /// matching.</remarks>
    private static readonly FrozenSet<string> _requirementBlockTypes = FrozenSet.ToFrozenSet(
    [
        "requirement",
        "functionalRequirement",
        "performanceRequirement",
        "interfaceRequirement",
        "physicalRequirement",
        "designConstraint",
        "element"
    ], StringComparer.OrdinalIgnoreCase);

    #endregion FrozenSet and FrozenDictionary Declarations

    #region Allocation-free AlternateLookups

    /// <summary>
    /// Provides an alternate lookup for diagram declarations that enables allocation-free queries using read-only
    /// character spans.
    /// </summary>
    /// <remarks>This lookup allows efficient membership checks against the set of diagram declarations
    /// without allocating new strings. It is intended for scenarios where performance is critical and input data is
    /// available as a span of characters.</remarks>
    private static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> _diagramDeclarationsLookup =
        _diagramDeclarations.GetAlternateLookup<ReadOnlySpan<char>>();

    /// <summary>
    /// Provides an alternate lookup for sequence block opener strings using a read-only character span as the key.
    /// </summary>
    /// <remarks>This lookup enables efficient matching of sequence block opener strings against spans of
    /// characters, which can improve performance in scenarios where input is not already a string. The lookup is
    /// case-insensitive and relies on the contents of the underlying set of sequence block openers.</remarks>
    private static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> _sequenceBlockOpenersLookup =
        _sequenceBlockOpeners.GetAlternateLookup<ReadOnlySpan<char>>();

    /// <summary>
    /// Provides an alternate lookup for diagram types using exact string matching with a read-only character span as
    /// the key.
    /// </summary>
    /// <remarks>This lookup enables efficient retrieval of diagram types by matching the exact sequence of
    /// characters in the input span. It is intended for scenarios where precise, case-insensitive matching is required.
    /// The lookup is read-only and thread-safe.</remarks>
    private static readonly FrozenDictionary<string, DiagramType>.AlternateLookup<ReadOnlySpan<char>> _exactDiagramTypesLookup =
        _exactDiagramTypes.GetAlternateLookup<ReadOnlySpan<char>>();

    /// <summary>
    /// Provides an alternate lookup for C4 boundary types using a read-only character span as the key.
    /// </summary>
    /// <remarks>This lookup enables efficient, allocation-free searches for boundary types by allowing
    /// queries with a <see cref="ReadOnlySpan{char}"/> instead of a string. It is intended
    /// for internal use to optimize performance when working with substrings or slices of character data.</remarks>
    private static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> _c4BoundaryTypesLookup =
        _c4BoundaryTypes.GetAlternateLookup<ReadOnlySpan<char>>();

    /// <summary>
    /// Provides an alternate lookup for requirement block types using a read-only character span as the key.
    /// </summary>
    /// <remarks>This lookup enables efficient, case-insensitive searches for requirement block types without
    /// allocating new strings. It is intended for scenarios where input is available as a span, such as parsing or
    /// streaming operations.</remarks>
    private static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> _requirementBlockTypesLookup =
        _requirementBlockTypes.GetAlternateLookup<ReadOnlySpan<char>>();

    #endregion Allocation-free AlternateLookups

    #region Regex patterns for whitespace normalization

    /// <summary>
    /// Provides a compiled regular expression that matches one or more whitespace characters.
    /// </summary>
    /// <remarks>The returned regular expression uses the pattern "\s+" and is compiled for improved
    /// performance. It can be used to identify or replace runs of whitespace in text, such as for normalization or
    /// splitting operations.</remarks>
    /// <returns>A compiled <see cref="Regex"/> instance that matches sequences of whitespace characters in a string.</returns>
    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceNormalizationRegex();

    #endregion Regex patterns

    /// <summary>
    /// Initializes a new instance of the <see cref="MermaidIndentationStrategy"/> class with default settings.
    /// </summary>
    /// <remarks>
    /// Uses 2 spaces as the default indentation, which is the standard for YAML and Mermaid diagrams.
    /// </remarks>
    public MermaidIndentationStrategy()
    {
        _indentationString = DefaultIndentation;
        _indentationSize = DefaultIndentationSize;
    }

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

    /// <inheritdoc cref="IIndentationStrategy.IndentLine"/>
    /// <remarks>
    /// <para>
    /// This method analyzes the previous line's content to determine the appropriate indentation level
    /// for the current line. It handles:
    /// </para>
    /// <list type="bullet">
    ///     <item>YAML frontmatter sections (between <c>---</c> delimiters)</item>
    ///     <item>Block-opening keywords that increase indentation</item>
    ///     <item>Block-closing keywords that decrease indentation</item>
    ///     <item>Indentation-based diagrams (mindmap, treemap, kanban)</item>
    ///     <item>Auto-dedent when the current line contains only a closing keyword</item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="line"/> is <see langword="null"/>.</exception>
    public override void IndentLine(TextDocument document, DocumentLine line)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(line);

        DocumentLine? previousLine = line.PreviousLine;
        if (previousLine is null)
        {
            // First line - no indentation needed
            return;
        }

        string previousLineText = document.GetText(previousLine);
        string currentLineText = document.GetText(line);
        ReadOnlySpan<char> previousLineSpan = previousLineText.AsSpan();
        ReadOnlySpan<char> currentLineSpan = currentLineText.AsSpan();

        // Calculate the desired indentation based on context
        string desiredIndentation = CalculateIndentation(document, previousLine, previousLineSpan);

        // Check if we need to auto-dedent for closing keywords on the current line
        ReadOnlySpan<char> currentLineTrimmed = currentLineSpan.TrimStart();
        if (IsClosingKeyword(currentLineTrimmed))
        {
            // Dedent by one level from the calculated indentation
            desiredIndentation = DedentOnce(desiredIndentation);
        }

        // Get the current indentation segment
        ISegment indentationSegment = TextUtilities.GetWhitespaceAfter(document, line.Offset);

        // Only update if the indentation actually changed
        string currentIndentation = document.GetText(indentationSegment);
        if (currentIndentation != desiredIndentation)
        {
            // TextEditor does not allow for concurrent modifications, so this is safe
            document.Replace(indentationSegment.Offset, indentationSegment.Length, desiredIndentation,
                OffsetChangeMappingType.RemoveAndInsert);
        }
    }

    /// <inheritdoc cref="IIndentationStrategy.IndentLines"/>
    /// <remarks>
    /// Applies smart indentation to each line in the specified range sequentially.
    /// Each line's indentation is calculated based on the content of the previous line.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="document"/> is <see langword="null"/>.</exception>
    public override void IndentLines(TextDocument document, int beginLine, int endLine)
    {
        ArgumentNullException.ThrowIfNull(document);

        // Clamp to valid range
        beginLine = Math.Max(1, beginLine);
        endLine = Math.Min(document.LineCount, endLine);

        for (int lineNumber = beginLine; lineNumber <= endLine; lineNumber++)
        {
            DocumentLine line = document.GetLineByNumber(lineNumber);
            IndentLine(document, line);
        }
    }

    /// <summary>
    /// Calculates the appropriate indentation string for a new line based on the previous line's content and context.
    /// </summary>
    /// <param name="document">The text document being edited.</param>
    /// <param name="previousLine">The document line immediately before the line being indented.</param>
    /// <param name="previousLineSpan">A read-only span containing the text of the previous line.</param>
    /// <returns>The indentation string to apply to the new line.</returns>
    private string CalculateIndentation(TextDocument document, DocumentLine previousLine, ReadOnlySpan<char> previousLineSpan)
    {
        // Get the previous line's indentation as our baseline
        string previousIndentation = GetIndentationString(previousLineSpan);
        ReadOnlySpan<char> previousLineTrimmed = previousLineSpan.TrimStart();

        // Handle empty previous line - just copy indentation
        if (previousLineTrimmed.IsEmpty)
        {
            return previousIndentation;
        }

        // Check document context (frontmatter vs diagram content)
        DocumentContext context = DetermineContext(document, previousLine.LineNumber);

        return context switch
        {
            DocumentContext.FrontmatterStart => _indentationString, // After opening ---, indent
            DocumentContext.FrontmatterEnd => string.Empty, // After closing ---, no indent
            DocumentContext.Frontmatter => CalculateFrontmatterIndentation(previousLineSpan, previousLineTrimmed),
            DocumentContext.Diagram => CalculateDiagramIndentation(document, previousIndentation, previousLineTrimmed),
            _ => previousIndentation // Default: copy previous indentation
        };
    }

    /// <summary>
    /// Calculates indentation for YAML frontmatter content.
    /// </summary>
    /// <param name="previousLineSpan">The full previous line including whitespace.</param>
    /// <param name="previousLineTrimmed">The previous line with leading whitespace removed.</param>
    /// <returns>The indentation string for the new line.</returns>
    private string CalculateFrontmatterIndentation(ReadOnlySpan<char> previousLineSpan, ReadOnlySpan<char> previousLineTrimmed)
    {
        string previousIndentation = GetIndentationString(previousLineSpan);

        // After a YAML key with colon at end (e.g., "config:"), indent
        if (EndsWithColonNotInValue(previousLineTrimmed))
        {
            return previousIndentation + _indentationString;
        }

        // After a YAML list item "- ", maintain indentation
        if (!previousLineTrimmed.IsEmpty && previousLineTrimmed[0] == '-')
        {
            return previousIndentation;
        }

        // Default: maintain previous indentation
        return previousIndentation;
    }

    /// <summary>
    /// Calculates indentation for Mermaid diagram content.
    /// </summary>
    /// <param name="document">The text document.</param>
    /// <param name="previousIndentation">The previous line's indentation string.</param>
    /// <param name="previousLineTrimmed">The previous line with leading whitespace removed.</param>
    /// <returns>The indentation string for the new line.</returns>
    private string CalculateDiagramIndentation(TextDocument document, string previousIndentation, ReadOnlySpan<char> previousLineTrimmed)
    {
        // Check for comments - maintain indentation
        if (StartsWithChars(previousLineTrimmed, '%', '%'))
        {
            return previousIndentation;
        }

        // Check for diagram declaration - indent the content
        if (IsDiagramDeclaration(previousLineTrimmed))
        {
            return previousIndentation + _indentationString;
        }

        // Check for block-opening keywords
        if (IsBlockOpener(previousLineTrimmed))
        {
            return previousIndentation + _indentationString;
        }

        // Check for block-closing keywords - dedent handled elsewhere for current line
        // but if previous line was a closer, we maintain that reduced level
        if (IsClosingKeyword(previousLineTrimmed))
        {
            return previousIndentation;
        }

        // Check for lines ending with { (class definitions, entity blocks, etc.)
        if (EndsWithChar(previousLineTrimmed, '{'))
        {
            return previousIndentation + _indentationString;
        }

        // Check for C4 boundaries ending with '{'
        if (IsC4BoundaryOpener(previousLineTrimmed))
        {
            return previousIndentation + _indentationString;
        }

        // Special handling for indentation-based diagrams (use cached type to avoid repeated scans)
        DiagramType diagramType = GetCachedDiagramType(document);
        if (IsIndentationBasedDiagram(diagramType))
        {
            // For mindmap/treemap/kanban, maintain the previous line's indentation
            // The user controls hierarchy through manual indentation
            return previousIndentation;
        }

        // Default: maintain previous indentation
        return previousIndentation;
    }

    /// <summary>
    /// Gets the cached diagram type for the document, detecting it if necessary.
    /// </summary>
    /// <param name="document">The text document.</param>
    /// <returns>The detected diagram type.</returns>
    /// <remarks>
    /// <para>
    /// This method implements a two-tier caching strategy:
    /// <list type="number">
    ///     <item>Fast path: Check if the previously cached declaration line still contains the same content.
    ///     If so, return the cached diagram type without reparsing (O(1) line access + O(m) string comparison).</item>
    ///     <item>Slow path: If the cached line is invalid or content has changed, perform a full scan to locate
    ///     the declaration line and reparse if the content changed (O(k) scan where k = lines before declaration).</item>
    /// </list>
    /// </para>
    /// <para>
    /// The cache invalidates only when the diagram declaration line content actually changes, not on every
    /// document modification. This provides significant performance improvement for typical editing scenarios
    /// where the user is adding/modifying diagram nodes (not changing the diagram type).
    /// </para>
    /// <para>
    /// Performance characteristics:
    /// <list type="bullet">
    ///     <item>Fast path (95%+ of calls): O(1) - Direct line access and string comparison</item>
    ///     <item>Slow path (declaration moved): O(k) - Scan to find declaration, no reparse if content unchanged</item>
    ///     <item>Slowest path (declaration changed): O(k) + O(p) - Scan and parse diagram type</item>
    /// </list>
    /// </para>
    /// </remarks>
    private DiagramType GetCachedDiagramType(TextDocument document)
    {
        // Fast path: Check if cached line number is still valid and content matches
        if (_cachedDeclarationLineNumber > 0 && _cachedDeclarationLineNumber <= document.LineCount)
        {
            DocumentLine cachedLine = document.GetLineByNumber(_cachedDeclarationLineNumber);
            string currentContent = GetNormalizedDeclarationText(document, cachedLine);

            // If the line at the cached position is still a valid declaration and matches cached content
            if (IsValidDeclaration(currentContent) && currentContent == _cachedDeclarationContent)
            {
                // Fast path success - content unchanged, return cached type
                return _cachedDiagramType;
            }
        }

        // Slow path: Search for declaration line
        (int lineNumber, string content) = FindDeclarationLine(document);
        if (lineNumber == 0)
        {
            // No declaration found - clear cache and return Unknown
            _cachedDeclarationLineNumber = 0;
            _cachedDeclarationContent = null;
            _cachedDiagramType = DiagramType.Unknown;
            return DiagramType.Unknown;
        }

        // Check if content changed (requires reparsing)
        bool contentChanged = (content != _cachedDeclarationContent);
        if (contentChanged)
        {
            // Content changed - reparse diagram type
            _cachedDiagramType = ParseDiagramTypeFromDeclaration(content);
        }
        // else: Line number may have moved, but content is the same - reuse cached type

        // Update cache with latest position and content
        _cachedDeclarationLineNumber = lineNumber;
        _cachedDeclarationContent = content;

        return _cachedDiagramType;
    }

    /// <summary>
    /// Finds the diagram declaration line in the document.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Searches for the first non-blank, non-comment line after any frontmatter section.
    /// The diagram declaration line is always:
    /// <list type="bullet">
    ///     <item>The first non-blank line of the document, OR</item>
    ///     <item>The first non-blank line after the closing frontmatter delimiter (---)</item>
    /// </list>
    /// </para>
    /// <para>
    /// This method leverages the cached frontmatter boundaries to optimize the search,
    /// starting the scan after the frontmatter end line if frontmatter exists.
    /// </para>
    /// </remarks>
    /// <param name="document">The text document to search.</param>
    /// <returns>A tuple containing the line number (1-based) and normalized content of the declaration line.
    /// Returns (0, string.Empty) if no valid declaration is found.</returns>
    private (int LineNumber, string Content) FindDeclarationLine(TextDocument document)
    {
        // Determine starting line based on frontmatter boundaries
        int startLine = 1;

        // Update frontmatter cache if needed
        EnsureFrontmatterCacheValid(document);

        if (_cachedFrontmatterEndLine > 0)
        {
            // Frontmatter exists and is closed - start searching after it
            startLine = _cachedFrontmatterEndLine + 1;
        }

        // Search for first non-blank, non-comment line
        for (int lineNumber = startLine; lineNumber <= document.LineCount; lineNumber++)
        {
            DocumentLine line = document.GetLineByNumber(lineNumber);
            string content = GetNormalizedDeclarationText(document, line);

            if (IsValidDeclaration(content))
            {
                // Found the declaration line
                return (lineNumber, content);
            }
        }

        // No declaration found
        return (0, string.Empty);
    }

    /// <summary>
    /// Gets the normalized text content of a potential declaration line.
    /// </summary>
    /// <remarks>
    /// Normalization includes:
    /// <list type="bullet">
    ///     <item>Trimming leading and trailing whitespace</item>
    ///     <item>Collapsing consecutive whitespace characters to a single space</item>
    /// </list>
    /// This ensures consistent cache comparisons even if the user adds/removes spaces
    /// within the declaration line.
    /// </remarks>
    /// <param name="document">The text document containing the line.</param>
    /// <param name="line">The document line to extract text from.</param>
    /// <returns>The normalized text content of the line.</returns>
    private static string GetNormalizedDeclarationText(TextDocument document, DocumentLine line)
    {
        string text = document.GetText(line.Offset, line.Length).Trim();

        // Collapse consecutive whitespace to single space for consistent comparison
        // e.g., "flowchart  TD" -> "flowchart TD"
        if (text.Length > 0)
        {
            text = WhitespaceNormalizationRegex().Replace(text, " ");
        }

        return text;
    }

    /// <summary>
    /// Determines whether the specified text represents a valid diagram declaration.
    /// </summary>
    /// <remarks>
    /// A valid declaration must:
    /// <list type="bullet">
    ///     <item>Not be empty</item>
    ///     <item>Not be a comment line (starting with %%)</item>
    /// </list>
    /// </remarks>
    /// <param name="text">The text to validate.</param>
    /// <returns><see langword="true"/> if the text is a valid declaration; otherwise, <see langword="false"/>.</returns>
    private static bool IsValidDeclaration(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        // Check if it's a comment line
        return !StartsWithChars(text, '%', '%');
    }

    /// <summary>
    /// Parses the diagram type from a normalized declaration line.
    /// </summary>
    /// <remarks>
    /// This method converts the string to a ReadOnlySpan and delegates to the existing
    /// ParseDiagramType method for consistency.
    /// </remarks>
    /// <param name="declaration">The normalized declaration text (e.g., "flowchart TD").</param>
    /// <returns>The parsed <see cref="DiagramType"/>.</returns>
    private static DiagramType ParseDiagramTypeFromDeclaration(string declaration)
    {
        ReadOnlySpan<char> declarationSpan = declaration.AsSpan();
        if (declarationSpan.IsEmpty)
        {
            return DiagramType.Unknown;
        }

        // Use the existing ParseDiagramType method for consistency
        return ParseDiagramType(declarationSpan);
    }

    /// <summary>
    /// Determines the document context (frontmatter or diagram) for the specified line.
    /// </summary>
    /// <param name="document">The text document.</param>
    /// <param name="lineNumber">The line number to check context for.</param>
    /// <returns>The document context at the specified line.</returns>
    /// <remarks>
    /// Uses cached frontmatter boundaries for O(1) lookup instead of scanning from line 1.
    /// The cache is invalidated when the document version changes.
    /// </remarks>
    private DocumentContext DetermineContext(TextDocument document, int lineNumber)
    {
        // Ensure frontmatter boundaries are cached and up-to-date
        EnsureFrontmatterCacheValid(document);

        // No frontmatter in document
        if (_cachedFrontmatterStartLine < 0)
        {
            return DocumentContext.Diagram;
        }

        // Check if line is the frontmatter start delimiter
        if (lineNumber == _cachedFrontmatterStartLine)
        {
            return DocumentContext.FrontmatterStart;
        }

        // Check if line is the frontmatter end delimiter
        if (_cachedFrontmatterEndLine > 0 && lineNumber == _cachedFrontmatterEndLine)
        {
            return DocumentContext.FrontmatterEnd;
        }

        // Check if line is within frontmatter (between start and end, or after start if no end)
        if (lineNumber > _cachedFrontmatterStartLine && (_cachedFrontmatterEndLine < 0 || lineNumber < _cachedFrontmatterEndLine))
        {
            return DocumentContext.Frontmatter;
        }

        return DocumentContext.Diagram;
    }

    /// <summary>
    /// Ensures the frontmatter boundary cache is valid for the current document version.
    /// </summary>
    /// <param name="document">The text document.</param>
    private void EnsureFrontmatterCacheValid(TextDocument document)
    {
        ITextSourceVersion? currentVersion = document.Version;

        // Check if cache is still valid
        if (_cachedFrontmatterVersion is not null &&
            currentVersion is not null &&
            _cachedFrontmatterVersion.BelongsToSameDocumentAs(currentVersion) &&
            _cachedFrontmatterVersion.CompareAge(currentVersion) == 0)
        {
            return; // Cache is valid
        }

        // Scan document for frontmatter boundaries
        _cachedFrontmatterStartLine = -1;
        _cachedFrontmatterEndLine = -1;

        // Limit scan to first 100 lines, Frontmatter should be near the top
        const int maxFrontmatterScanLines = 100;
        int maxLinesToScan = Math.Min(document.LineCount, maxFrontmatterScanLines);
        for (int i = 1; i <= maxLinesToScan; i++)
        {
            DocumentLine scanLine = document.GetLineByNumber(i);
            ReadOnlySpan<char> lineText = document.GetText(scanLine).AsSpan().Trim();

            if (lineText.SequenceEqual(FrontmatterDelimiter))
            {
                if (_cachedFrontmatterStartLine < 0)
                {
                    // First --- found (opening)
                    _cachedFrontmatterStartLine = i;
                }
                else
                {
                    // Second --- found (closing)
                    _cachedFrontmatterEndLine = i;
                    break; // Found both delimiters
                }
            }
        }

        _cachedFrontmatterVersion = currentVersion;
    }

    /// <summary>
    /// Parses a line to determine the diagram type from its declaration.
    /// </summary>
    /// <param name="lineText">The trimmed line text to parse.</param>
    /// <returns>The detected diagram type.</returns>
    private static DiagramType ParseDiagramType(ReadOnlySpan<char> lineText)
    {
        // Extract first word (diagram type keyword)
        int spaceIndex = lineText.IndexOf(SpaceChar);
        ReadOnlySpan<char> keyword = spaceIndex > 0 ? lineText[..spaceIndex] : lineText;

        // Flowchart variants (check prefix)
        if (keyword.StartsWith("flowchart", StringComparison.OrdinalIgnoreCase) ||
            keyword.StartsWith("graph", StringComparison.OrdinalIgnoreCase))
        {
            return DiagramType.Flowchart;
        }

        // State diagram (check prefix for stateDiagram-v2)
        if (keyword.StartsWith("stateDiagram", StringComparison.OrdinalIgnoreCase))
        {
            return DiagramType.State;
        }

        // C4 diagrams (check prefix)
        if (keyword.StartsWith("C4", StringComparison.OrdinalIgnoreCase))
        {
            return DiagramType.C4;
        }

        // Architecture (check prefix for architecture-beta)
        if (keyword.StartsWith("architecture", StringComparison.OrdinalIgnoreCase))
        {
            return DiagramType.Architecture;
        }

        // Block diagram (check prefix for block-beta)
        if (keyword.StartsWith("block", StringComparison.OrdinalIgnoreCase))
        {
            return DiagramType.Block;
        }

        // Sankey (check prefix for sankey-beta)
        if (keyword.StartsWith("sankey", StringComparison.OrdinalIgnoreCase))
        {
            return DiagramType.Sankey;
        }

        // XY Chart (check prefix for xychart-beta)
        if (keyword.StartsWith("xychart", StringComparison.OrdinalIgnoreCase))
        {
            return DiagramType.XYChart;
        }

        // Packet (check prefix for packet-beta)
        if (keyword.StartsWith("packet", StringComparison.OrdinalIgnoreCase))
        {
            return DiagramType.Packet;
        }

        // Radar (check prefix for radar-beta)
        if (keyword.StartsWith("radar", StringComparison.OrdinalIgnoreCase))
        {
            return DiagramType.Radar;
        }

        // Treemap (check prefix for treemap-beta)
        if (keyword.StartsWith("treemap", StringComparison.OrdinalIgnoreCase))
        {
            return DiagramType.Treemap;
        }

        // Exact matches using O(1) FrozenDictionary alternate lookup (no allocation)
        return _exactDiagramTypesLookup.TryGetValue(keyword, out DiagramType diagramType)
            ? diagramType
            : DiagramType.Unknown;
    }

    /// <summary>
    /// Determines if the specified diagram type uses indentation-based hierarchy.
    /// </summary>
    /// <param name="diagramType">The diagram type to check.</param>
    /// <returns><see langword="true"/> if the diagram uses indentation for hierarchy; otherwise, <see langword="false"/>.</returns>
    private static bool IsIndentationBasedDiagram(DiagramType diagramType)
    {
        return diagramType is DiagramType.Mindmap or DiagramType.Treemap or DiagramType.Kanban;
    }

    /// <summary>
    /// Determines if the line contains a Mermaid diagram type declaration.
    /// </summary>
    /// <param name="lineTrimmed">The trimmed line text to check.</param>
    /// <returns><see langword="true"/> if the line is a diagram declaration; otherwise, <see langword="false"/>.</returns>
    private static bool IsDiagramDeclaration(ReadOnlySpan<char> lineTrimmed)
    {
        // Extract first word
        int spaceIndex = lineTrimmed.IndexOf(SpaceChar);
        ReadOnlySpan<char> firstWord = spaceIndex > 0 ? lineTrimmed[..spaceIndex] : lineTrimmed;

        // O(1) lookup using alternate lookup - no allocation
        return _diagramDeclarationsLookup.Contains(firstWord);
    }

    /// <summary>
    /// Determines if the line contains a block-opening keyword that should increase indentation.
    /// </summary>
    /// <param name="lineTrimmed">The trimmed line text to check.</param>
    /// <returns><see langword="true"/> if the line opens a new block; otherwise, <see langword="false"/>.</returns>
    private static bool IsBlockOpener(ReadOnlySpan<char> lineTrimmed)
    {
        // Extract first word for keyword matching
        int spaceIndex = lineTrimmed.IndexOf(SpaceChar);
        ReadOnlySpan<char> firstWord = spaceIndex > 0 ? lineTrimmed[..spaceIndex] : lineTrimmed;

        // Flowchart: subgraph
        if (firstWord.Equals("subgraph", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Sequence diagram blocks - O(1) lookup, no allocation
        if (_sequenceBlockOpenersLookup.Contains(firstWord))
        {
            return true;
        }

        // State diagram: state keyword followed by name and { (e.g., "state Active {")
        if (firstWord.Equals("state", StringComparison.OrdinalIgnoreCase) &&
            EndsWithChar(lineTrimmed, '{'))
        {
            return true;
        }

        // Block diagram: "block:" or "block:ID" (colon must immediately follow "block" prefix)
        // Valid: "block:", "block:ID", "block:myBlock"
        // Invalid: "block-beta", "block: text with: colons"
        if (firstWord.StartsWith("block", StringComparison.OrdinalIgnoreCase) &&
            !firstWord.Equals("block-beta", StringComparison.OrdinalIgnoreCase))
        {
            int colonIndex = firstWord.IndexOf(':');
            // Colon must be at position 5 (immediately after "block") or exist in firstWord
            if (colonIndex >= 5)
            {
                return true;
            }
        }

        // Gantt/Journey/Timeline: section
        if (firstWord.Equals("section", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Architecture: group
        if (firstWord.Equals("group", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // C4: exact boundary type matches (Enterprise_Boundary, System_Boundary, etc.)
        // Extract just the keyword before any parenthesis for matching
        int parenIndex = firstWord.IndexOf('(');
        ReadOnlySpan<char> boundaryKeyword = parenIndex > 0 ? firstWord[..parenIndex] : firstWord;
        if (_c4BoundaryTypesLookup.Contains(boundaryKeyword))
        {
            return true;
        }

        // Requirement diagram: exact matches for known requirement/element types
        if (_requirementBlockTypesLookup.Contains(firstWord))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Determines if the line contains a block-closing keyword that should decrease indentation.
    /// </summary>
    /// <param name="lineTrimmed">The trimmed line text to check.</param>
    /// <returns><see langword="true"/> if the line closes a block; otherwise, <see langword="false"/>.</returns>
    private static bool IsClosingKeyword(ReadOnlySpan<char> lineTrimmed)
    {
        if (lineTrimmed.IsEmpty)
        {
            return false;
        }

        // Single closing brace
        if (lineTrimmed.Length == 1 && lineTrimmed[0] == '}')
        {
            return true;
        }

        // "end" keyword - check length first for efficiency
        if (lineTrimmed.Length < 3)
        {
            return false;
        }

        // Check for "end" (exact match or followed by whitespace)
        if (lineTrimmed.Length == 3)
        {
            return lineTrimmed.Equals("end", StringComparison.OrdinalIgnoreCase);
        }

        // "end" followed by whitespace
        if (char.IsWhiteSpace(lineTrimmed[3]))
        {
            return lineTrimmed[..3].Equals("end", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    /// <summary>
    /// Determines if the line is a C4 boundary opener ending with '{'.
    /// </summary>
    /// <param name="lineTrimmed">The trimmed line text to check.</param>
    /// <returns><see langword="true"/> if the line is a C4 boundary opener; otherwise, <see langword="false"/>.</returns>
    private static bool IsC4BoundaryOpener(ReadOnlySpan<char> lineTrimmed)
    {
        if (!EndsWithChar(lineTrimmed, '{'))
        {
            return false;
        }

        // Extract the first word (boundary type keyword) before any parenthesis
        int spaceIndex = lineTrimmed.IndexOf(SpaceChar);

        ReadOnlySpan<char> firstWord = spaceIndex > 0 ? lineTrimmed[..spaceIndex] : lineTrimmed;

        int parenIndex = firstWord.IndexOf('(');
        ReadOnlySpan<char> boundaryKeyword = parenIndex > 0 ? firstWord[..parenIndex] : firstWord;

        // Use exact match against known C4 boundary types
        return _c4BoundaryTypesLookup.Contains(boundaryKeyword);
    }

    /// <summary>
    /// Determines if a YAML line ends with a colon that indicates a nested block (not an inline value).
    /// </summary>
    /// <param name="lineTrimmed">The trimmed line text to check.</param>
    /// <returns><see langword="true"/> if the line ends with a colon indicating a nested block; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// This method distinguishes between:
    /// <list type="bullet">
    ///     <item><c>config:</c> -> returns true (nested block follows)</item>
    ///     <item><c>name: John</c> -> returns false (inline value)</item>
    ///     <item><c>label: "test:"</c> -> returns false (inline value ending with colon)</item>
    /// </list>
    /// </remarks>
    private static bool EndsWithColonNotInValue(ReadOnlySpan<char> lineTrimmed)
    {
        // Must end with colon
        if (lineTrimmed.IsEmpty || lineTrimmed[^1] != ':')
        {
            return false;
        }

        // Find the first colon to check for inline values (e.g., "key: value" or "label: test:")
        // If the first colon has non-whitespace content after it, this is an inline value
        int firstColonIndex = lineTrimmed.IndexOf(':');
        if (firstColonIndex < lineTrimmed.Length - 1)
        {
            ReadOnlySpan<char> afterFirstColon = lineTrimmed[(firstColonIndex + 1)..].Trim();
            if (!afterFirstColon.IsEmpty)
            {
                return false; // Has inline value
            }
        }

        // Line ends with colon and has no inline value (e.g., "config:" or "key:   ")
        return true;
    }
    /// <summary>
    /// Checks if a span starts with two specific characters.
    /// </summary>
    /// <param name="span">The span to check.</param>
    /// <param name="first">The expected first character.</param>
    /// <param name="second">The expected second character.</param>
    /// <returns><see langword="true"/> if the span starts with the two characters; otherwise, <see langword="false"/>.</returns>
    private static bool StartsWithChars(ReadOnlySpan<char> span, char first, char second)
    {
        return span.Length >= 2 && span[0] == first && span[1] == second;
    }

    /// <summary>
    /// Checks if a span ends with a specific character.
    /// </summary>
    /// <param name="span">The span to check.</param>
    /// <param name="c">The expected last character.</param>
    /// <returns><see langword="true"/> if the span ends with the character; otherwise, <see langword="false"/>.</returns>
    private static bool EndsWithChar(ReadOnlySpan<char> span, char c)
    {
        return !span.IsEmpty && span[^1] == c;
    }

    /// <summary>
    /// Returns the leading indentation characters from the specified line as a string.
    /// </summary>
    /// <remarks>For common indentation patterns (such as tabs, 2-space, or 4-space indents), a pre-allocated
    /// string may be returned to improve performance. For other patterns, a new string is allocated. This method does
    /// not modify the input span.</remarks>
    /// <param name="lineSpan">A read-only span of characters representing the line from which
    /// to extract indentation. Only the leading whitespace characters are considered.</param>
    /// <returns>A string containing the leading indentation characters of the line.
    /// Returns an empty string if the line has no indentation.</returns>
    private static string GetIndentationString(ReadOnlySpan<char> lineSpan)
    {
        int indentLength = GetIndentationLength(lineSpan);
        if (indentLength == 0)
        {
            return string.Empty;
        }

        ReadOnlySpan<char> indentSpan = lineSpan[..indentLength];

        // Try to return a pre-allocated string for common patterns
        // Check for pure tabs
        if (indentSpan[0] == TabChar && IsAllSameChar(indentSpan, TabChar))
        {
            int tabCount = indentLength;
            if (tabCount < _preAllocatedIndentsTab.Length)
            {
                return _preAllocatedIndentsTab[tabCount];
            }
        }
        // Check for pure spaces
        else if (indentSpan[0] == SpaceChar && IsAllSameChar(indentSpan, SpaceChar))
        {
            // Check for 2-space indentation
            if (indentLength % 2 == 0)
            {
                int level = indentLength / 2;
                if (level < _preAllocatedIndents2Spaces.Length)
                {
                    return _preAllocatedIndents2Spaces[level];
                }
            }

            // Check for 4-space indentation
            if (indentLength % 4 == 0)
            {
                int level = indentLength / 4;
                if (level < _preAllocatedIndents4Spaces.Length)
                {
                    return _preAllocatedIndents4Spaces[level];
                }
            }
        }

        // Fallback: allocate new string for non-standard indentation
        return indentSpan.ToString();
    }

    /// <summary>
    /// Checks if all characters in a span are the same character.
    /// </summary>
    /// <param name="span">The span to check.</param>
    /// <param name="c">The character to compare against.</param>
    /// <returns><see langword="true"/> if all characters match; otherwise, <see langword="false"/>.</returns>
    private static bool IsAllSameChar(ReadOnlySpan<char> span, char c)
    {
        foreach (char ch in span)
        {
            if (ch != c)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets the length of leading whitespace in a line.
    /// </summary>
    /// <param name="lineSpan">The line text to measure.</param>
    /// <returns>The number of leading whitespace characters.</returns>
    private static int GetIndentationLength(ReadOnlySpan<char> lineSpan)
    {
        int count = 0;
        foreach (char c in lineSpan)
        {
            if (char.IsWhiteSpace(c))
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
    /// Reduces the indentation by one level.
    /// </summary>
    /// <param name="currentIndentation">The current indentation string.</param>
    /// <returns>The indentation reduced by one level, or empty string if at minimum.</returns>
    private string DedentOnce(string currentIndentation)
    {
        if (string.IsNullOrEmpty(currentIndentation))
        {
            return string.Empty;
        }

        // Remove one indentation unit
        if (currentIndentation.Length >= _indentationSize)
        {
            return currentIndentation[..^_indentationSize];
        }

        return string.Empty;
    }

    /// <summary>
    /// Creates a cache of indentation strings for each indentation level up to the specified maximum.
    /// </summary>
    /// <param name="indent">The string to use for a single indentation level. Cannot be null.</param>
    /// <param name="maxLevels">The maximum number of indentation levels to cache. Must be greater than or equal to 0.</param>
    /// <returns>An array of strings where each element represents the indentation for its corresponding level. The first element
    /// is an empty string for level 0.</returns>
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
    /// Represents the context within a Mermaid document.
    /// </summary>
    private enum DocumentContext
    {
        /// <summary>
        /// The line is the opening frontmatter delimiter (first ---).
        /// </summary>
        FrontmatterStart,

        /// <summary>
        /// The line is within the YAML frontmatter section.
        /// </summary>
        Frontmatter,

        /// <summary>
        /// The line is the closing frontmatter delimiter (second ---).
        /// </summary>
        FrontmatterEnd,

        /// <summary>
        /// The line is within the Mermaid diagram content.
        /// </summary>
        Diagram
    }

    /// <summary>
    /// Represents the type of Mermaid diagram detected in the document.
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedMember.Local", Justification = "All enum values may be used for diagram type detection")]
    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Enum names match Mermaid diagram types")]
    private enum DiagramType
    {
        Unknown,
        Flowchart,
        Sequence,
        State,
        Class,
        EntityRelationship,
        Gantt,
        Pie,
        Mindmap,
        Timeline,
        Journey,
        GitGraph,
        C4,
        Architecture,
        Block,
        Requirement,
        Sankey,
        XYChart,
        Quadrant,
        Packet,
        Kanban,
        Radar,
        Treemap
    }
}
