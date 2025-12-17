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

using AvaloniaEdit.Document;
using MermaidPad.Models;
using MermaidPad.Models.Constants;
using MermaidPad.Models.Editor;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MermaidPad.Services.Editor;

/// <summary>
/// Provides analysis of Mermaid document structure, including frontmatter boundary detection,
/// line context determination, and diagram type identification.
/// </summary>
/// <remarks>
/// <para>
/// This service is designed to be shared across multiple editor features (indentation, commenting, etc.)
/// that need to understand document structure. It uses intelligent caching to avoid redundant scans
/// of the document for frontmatter boundaries and diagram type detection.
/// </para>
/// <para>
/// The analyzer identifies YAML frontmatter delimited by <c>---</c> markers and distinguishes between:
/// <list type="bullet">
/// <item><description>Frontmatter delimiter lines (opening and closing <c>---</c>)</description></item>
/// <item><description>Frontmatter content lines (between delimiters)</description></item>
/// <item><description>Diagram content lines (after frontmatter or entire document if no frontmatter)</description></item>
/// </list>
/// </para>
/// <para>
/// Performance characteristics:
/// <list type="bullet">
/// <item><description>O(1) context lookups after initial cache</description></item>
/// <item><description>Zero allocations for line context queries</description></item>
/// <item><description>Incremental cache invalidation based on document version tracking</description></item>
/// </list>
/// </para>
/// </remarks>
#pragma warning disable S3267 // Loops should be simplified with "LINQ" expressions. This code is performance-sensitive.
internal sealed partial class DocumentAnalyzer
{
    internal const int OneBasedFirstLineNumber = 1;
    internal const string SingleSpaceString = " ";
    internal const char SpaceChar = ' ';

    private const int MaxFrontmatterScanLines = 100;
    private const char Percent = '%';

    // Frontmatter boundary caching
    private int _cachedFrontmatterStartLine = -1; // -1 means no frontmatter
    private int _cachedFrontmatterEndLine = -1; // -1 means frontmatter not closed
    private ITextSourceVersion? _cachedFrontmatterVersion;

    // Diagram declaration caching
    private int _cachedDeclarationLineNumber; // 0 = not cached
    private string? _cachedDeclarationContent; // null = not cached
    private DiagramType _cachedDiagramType = DiagramType.Unknown;

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

    #region Frontmatter internal API

    /// <summary>
    /// Determines the document context (frontmatter or diagram) for the specified line number.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method uses cached frontmatter boundaries for O(1) lookup performance.
    /// The cache is automatically updated when the document version changes.
    /// </para>
    /// <para>
    /// Possible return values:
    /// <list type="bullet">
    /// <item><description><see cref="DocumentContext.FrontmatterStart"/> - Line is the opening <c>---</c> delimiter</description></item>
    /// <item><description><see cref="DocumentContext.Frontmatter"/> - Line is YAML content between delimiters</description></item>
    /// <item><description><see cref="DocumentContext.FrontmatterEnd"/> - Line is the closing <c>---</c> delimiter</description></item>
    /// <item><description><see cref="DocumentContext.Diagram"/> - Line is Mermaid diagram content</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="document">The text document to analyze. Cannot be null.</param>
    /// <param name="lineNumber">The one-based line number to evaluate.</param>
    /// <returns>A <see cref="DocumentContext"/> value indicating the structural role of the specified line.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="document"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="lineNumber"/> is negative.</exception>
    internal DocumentContext DetermineLineContext(TextDocument document, int lineNumber)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentOutOfRangeException.ThrowIfNegative(lineNumber);

        UpdateFrontmatterCache(document);
        return DetermineContextFromCache(lineNumber);
    }

    /// <summary>
    /// Determines whether the specified line contains only whitespace characters.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method uses <see cref="TextUtilities.GetLeadingWhitespace"/> to avoid allocating strings
    /// for blank line checks, making it suitable for performance-critical operations.
    /// </para>
    /// <para>
    /// A line is considered blank if it contains only spaces, tabs, or is completely empty.
    /// Line delimiters (CR, LF, CRLF) are not included in the check.
    /// </para>
    /// </remarks>
    /// <param name="document">The text document containing the line. Cannot be null.</param>
    /// <param name="line">The document line to evaluate. Cannot be null.</param>
    /// <returns><see langword="true"/> if the line contains only whitespace; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="document"/> or <paramref name="line"/> is null.</exception>
    internal static bool IsLineBlank(TextDocument document, DocumentLine line)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(line);

        // DocumentLine.Length excludes the line delimiter, so this correctly detects lines that are only spaces/tabs.
        return TextUtilities.GetLeadingWhitespace(document, line).Length == line.Length;
    }

    /// <summary>
    /// Determines whether the specified line consists solely of the frontmatter delimiter (<c>---</c>),
    /// ignoring leading and trailing whitespace.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method identifies lines that mark the boundaries of YAML frontmatter sections.
    /// The delimiter must be exactly three hyphens; other content on the line (besides whitespace)
    /// will cause this method to return <see langword="false"/>.
    /// </para>
    /// <para>
    /// Performance: Uses character-by-character comparison without string allocation.
    /// </para>
    /// </remarks>
    /// <param name="document">The text document containing the line. Cannot be null.</param>
    /// <param name="line">The document line to evaluate. Cannot be null.</param>
    /// <returns><see langword="true"/> if the trimmed line content is exactly <c>---</c>; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="document"/> or <paramref name="line"/> is null.</exception>
    internal static bool IsFrontmatterDelimiter(TextDocument document, DocumentLine line)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(line);

        int offset = line.Offset;
        int length = line.Length;

        // Find start of non-whitespace content
        int start = 0;
        while (start < length && char.IsWhiteSpace(document.GetCharAt(offset + start)))
        {
            start++;
        }

        // Find end of non-whitespace content
        int end = length - 1;
        while (end >= start && char.IsWhiteSpace(document.GetCharAt(offset + end)))
        {
            end--;
        }

        // Calculate trimmed length
        int trimmedLen = end - start + 1;
        if (trimmedLen != Frontmatter.Delimiter.Length)
        {
            return false;
        }

        // Compare character-by-character (Frontmatter.Delimiter is expected to be "---")
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
    /// Gets the one-based line number of the frontmatter start delimiter, or -1 if no frontmatter exists.
    /// </summary>
    /// <remarks>
    /// This method updates the cache if necessary before returning the result.
    /// A return value of -1 indicates the document has no frontmatter section.
    /// </remarks>
    /// <param name="document">The text document to analyze. Cannot be null.</param>
    /// <returns>The one-based line number of the opening <c>---</c> delimiter, or -1 if not present.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="document"/> is null.</exception>
    internal int GetFrontmatterStartLine(TextDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        UpdateFrontmatterCache(document);
        return _cachedFrontmatterStartLine;
    }

    /// <summary>
    /// Gets the one-based line number of the frontmatter end delimiter, or -1 if the frontmatter is unclosed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method updates the cache if necessary before returning the result.
    /// </para>
    /// <para>
    /// Possible return values:
    /// <list type="bullet">
    /// <item><description>Positive number: Line number of the closing <c>---</c> delimiter</description></item>
    /// <item><description>-1: Either no frontmatter exists, or frontmatter is unclosed (only opening delimiter found)</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="document">The text document to analyze. Cannot be null.</param>
    /// <returns>The one-based line number of the closing <c>---</c> delimiter, or -1 if not present/unclosed.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="document"/> is null.</exception>
    internal int GetFrontmatterEndLine(TextDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        UpdateFrontmatterCache(document);
        return _cachedFrontmatterEndLine;
    }















    ///// <summary>
    ///// Determines whether the document contains a valid frontmatter section with both opening and closing delimiters.
    ///// </summary>
    ///// <remarks>
    ///// A document has frontmatter only if both the opening <c>---</c> and closing <c>---</c> delimiters
    ///// are present. An unclosed frontmatter section (only opening delimiter) does not count as valid frontmatter.
    ///// </remarks>
    ///// <param name="document">The text document to analyze. Cannot be null.</param>
    ///// <returns><see langword="true"/> if the document has properly delimited frontmatter; otherwise, <see langword="false"/>.</returns>
    ///// <exception cref="ArgumentNullException">Thrown if <paramref name="document"/> is null.</exception>
    //internal bool HasFrontmatter(TextDocument document)
    //{
    //    ArgumentNullException.ThrowIfNull(document);

    //    UpdateFrontmatterCache(document);
    //    return _cachedFrontmatterStartLine > 0 && _cachedFrontmatterEndLine > 0;
    //}













    #endregion Frontmatter internal API

    #region Diagram type internal API

    /// <summary>
    /// Gets the diagram type for the document, using cached values when possible.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method implements a two-tier caching strategy:
    /// <list type="number">
    /// <item><description>Fast path: re-check the previously cached declaration line content.</description></item>
    /// <item><description>Slow path: scan for the declaration line and reparse diagram type if it changed.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The diagram type is detected by finding the first non-blank, non-comment line after any frontmatter,
    /// and parsing the diagram keyword from that line.
    /// </para>
    /// </remarks>
    /// <param name="document">The text document to analyze. Cannot be null.</param>
    /// <returns>The detected diagram type, or <see cref="DiagramType.Unknown"/> if not found.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="document"/> is null.</exception>
    internal DiagramType GetDiagramType(TextDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        // Ensure frontmatter cache is up to date first
        UpdateFrontmatterCache(document);

        return GetCachedDiagramType(document, _cachedFrontmatterEndLine);
    }

    #endregion Diagram type internal API

    #region Frontmatter caching implementation

    /// <summary>
    /// Determines the <see cref="DocumentContext"/> for a specified line number based on cached frontmatter boundaries.
    /// </summary>
    /// <remarks>
    /// This is an O(1) operation that uses previously cached frontmatter start and end line numbers.
    /// If no frontmatter boundaries are cached, all lines are treated as diagram content.
    /// </remarks>
    /// <param name="lineNumber">The one-based line number for which to determine the document context.</param>
    /// <returns>
    /// A value from the <see cref="DocumentContext"/> enumeration indicating the context of the specified line.
    /// Returns <see cref="DocumentContext.Diagram"/>, <see cref="DocumentContext.FrontmatterStart"/>,
    /// <see cref="DocumentContext.FrontmatterEnd"/>, or <see cref="DocumentContext.Frontmatter"/>
    /// depending on the line's position relative to the cached frontmatter boundaries.
    /// </returns>
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
    /// <remarks>
    /// <para>
    /// This method implements an intelligent caching strategy:
    /// <list type="number">
    /// <item><description>Check if cache is valid for current document version</description></item>
    /// <item><description>If valid, reuse cached boundaries (fast path)</description></item>
    /// <item><description>If invalid, rescan document and update cache (slow path)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The cache is considered valid if:
    /// <list type="bullet">
    /// <item><description>Document version matches cached version, OR</description></item>
    /// <item><description>No changes occurred in the frontmatter region since last scan</description></item>
    /// </list>
    /// </para>
    /// </remarks>
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
    /// Determines whether the cached frontmatter boundaries can be reused for the specified document version.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The cache is reusable if:
    /// <list type="number">
    /// <item><description>Document version exactly matches cached version (no changes), OR</description></item>
    /// <item><description>Changes occurred outside the frontmatter region (cache still valid)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// If the cache is reused after confirming no frontmatter changes, the cached version
    /// is updated to the current version to avoid redundant change checking on future calls.
    /// </para>
    /// </remarks>
    /// <param name="currentVersion">The current version of the text source to compare against the cached version.</param>
    /// <param name="maxFrontmatterScanLines">The maximum number of lines to consider as the frontmatter region.</param>
    /// <param name="document">The text document to check for frontmatter changes.</param>
    /// <returns><see langword="true"/> if the cached frontmatter is valid for the current version; otherwise, <see langword="false"/>.</returns>
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
    /// Determines whether any changes occurred in the frontmatter region between the cached version
    /// and the current document version.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The frontmatter region is defined as the first N lines of the document, where N is either:
    /// <list type="bullet">
    /// <item><description>The cached frontmatter end line (if known), OR</description></item>
    /// <item><description>The maximum scan lines (if frontmatter end is unknown/unclosed)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// If an error occurs during change detection, this method conservatively returns <see langword="true"/>
    /// to indicate changes may be present, ensuring the cache is invalidated and rescanned.
    /// </para>
    /// </remarks>
    /// <param name="currentVersion">The current version of the text source to compare against the cached version.</param>
    /// <param name="maxFrontmatterScanLines">The maximum number of lines to scan when determining frontmatter extent.</param>
    /// <param name="document">The text document containing the content to analyze for frontmatter changes.</param>
    /// <returns>
    /// <see langword="true"/> if any changes are detected within the frontmatter region; otherwise, <see langword="false"/>.
    /// Returns <see langword="true"/> if an error occurs during change detection.
    /// </returns>
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
            Debug.WriteLine($"[DocumentAnalyzer] {nameof(HasChangesInFrontmatterRegion)}: Error checking frontmatter changes: {ex}");
            return true;
        }
    }

    /// <summary>
    /// Determines whether a specific text change affects the frontmatter region of the document.
    /// </summary>
    /// <remarks>
    /// A change affects the frontmatter region if any part of the changed text overlaps with
    /// the lines from 1 to <paramref name="frontmatterRegionEndLine"/>.
    /// </remarks>
    /// <param name="change">The text change event data containing the offset and length of the change.</param>
    /// <param name="frontmatterRegionEndLine">The one-based line number indicating the end of the frontmatter region.</param>
    /// <param name="document">The text document in which the change occurred.</param>
    /// <returns><see langword="true"/> if the change overlaps with the frontmatter region; otherwise, <see langword="false"/>.</returns>
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
    /// Scans the document to identify the start and end line numbers of the frontmatter section.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method searches for the frontmatter delimiters (<c>---</c>) within the first N lines
    /// of the document, where N is specified by <paramref name="maxFrontmatterScanLines"/>.
    /// </para>
    /// <para>
    /// Scanning rules:
    /// <list type="bullet">
    /// <item><description>First <c>---</c> found = frontmatter start line</description></item>
    /// <item><description>Second <c>---</c> found = frontmatter end line</description></item>
    /// <item><description>Only first two delimiters are considered; subsequent delimiters are ignored</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// After scanning, the cached boundary values are updated:
    /// <list type="bullet">
    /// <item><description>No delimiters found: Start = -1, End = -1 (no frontmatter)</description></item>
    /// <item><description>One delimiter found: Start = line number, End = -1 (unclosed frontmatter)</description></item>
    /// <item><description>Two delimiters found: Start = first line, End = second line (valid frontmatter)</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="document">The text document to scan for frontmatter boundaries.</param>
    /// <param name="maxFrontmatterScanLines">The maximum number of lines to scan from the beginning of the document.</param>
    private void RescanFrontmatterBoundaries(TextDocument document, int maxFrontmatterScanLines)
    {
        _cachedFrontmatterStartLine = -1;
        _cachedFrontmatterEndLine = -1;

        int maxLinesToScan = Math.Min(document.LineCount, maxFrontmatterScanLines);
        for (int i = OneBasedFirstLineNumber; i <= maxLinesToScan; i++)
        {
            DocumentLine scanLine = document.GetLineByNumber(i);
            if (!IsFrontmatterDelimiter(document, scanLine))
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

    #endregion Frontmatter caching implementation

    #region Diagram type caching implementation

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
    /// <param name="frontmatterEndLine">The cached frontmatter end line (-1 if no frontmatter or unclosed).</param>
    /// <returns>The detected diagram type (or Unknown if not found).</returns>
    private DiagramType GetCachedDiagramType(TextDocument document, int frontmatterEndLine)
    {
        if (_cachedDeclarationLineNumber > 0 && _cachedDeclarationLineNumber <= document.LineCount)
        {
            DocumentLine cachedLine = document.GetLineByNumber(_cachedDeclarationLineNumber);
            string currentContent = GetNormalizedDeclarationText(document, cachedLine);

            if (IsValidSyntaxDeclaration(currentContent) && currentContent == _cachedDeclarationContent)
            {
                return _cachedDiagramType;
            }
        }

        (int lineNumber, string content) = FindDeclarationLine(document, frontmatterEndLine);
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
    /// <param name="frontmatterEndLine">The frontmatter end line (-1 if no frontmatter or unclosed).</param>
    /// <returns>Tuple of (lineNumber, normalizedText), or (0, empty) if not found.</returns>
    private static (int LineNumber, string Content) FindDeclarationLine(TextDocument document, int frontmatterEndLine)
    {
        int startLine = OneBasedFirstLineNumber;

        // Skip past frontmatter if present
        if (frontmatterEndLine > 0)
        {
            startLine = frontmatterEndLine + 1;
        }

        for (int lineNumber = startLine; lineNumber <= document.LineCount; lineNumber++)
        {
            DocumentLine line = document.GetLineByNumber(lineNumber);
            string content = GetNormalizedDeclarationText(document, line);
            if (IsValidSyntaxDeclaration(content))
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
    private static bool IsValidSyntaxDeclaration(string text)
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
    /// <param name="declarationSpan">The declaration as a <see cref="ReadOnlySpan{char}"/>
    /// from which to determine the diagram type. Cannot be null.</param>
    /// <returns>A value of the <see cref="DiagramType"/> enumeration that represents the diagram type specified in the
    /// declaration. Returns <see cref="DiagramType.Unknown"/> if the declaration is empty.</returns>
    private static DiagramType ParseDiagramTypeFromDeclaration(ReadOnlySpan<char> declarationSpan)
    {
        if (declarationSpan.IsEmpty)
        {
            return DiagramType.Unknown;
        }

        int spaceIndex = declarationSpan.IndexOf(SpaceChar);
        ReadOnlySpan<char> keyword = spaceIndex > 0 ? declarationSpan[..spaceIndex] : declarationSpan;
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
        if (keyword.IsEmpty)
        {
            return DiagramType.Unknown;
        }

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
        if (keyword.IsEmpty)
        {
            return DiagramType.Unknown;
        }

        return DiagramTypeLookups.C4DiagramNameToTypeMappingLookup.TryGetValue(keyword, out DiagramType diagramType)
            ? diagramType
            : DiagramType.Unknown;
    }

    #endregion Diagram type caching implementation

    #region String/ReadOnlySpan<char> Helpers

    /// <summary>
    /// Determines whether the specified span begins with the given two characters in order.
    /// </summary>
    /// <param name="span">The span of characters to examine.</param>
    /// <param name="first">The character to compare to the first character of the span.</param>
    /// <param name="second">The character to compare to the second character of the span.</param>
    /// <returns><see langword="true"/> if the span is at least two characters long and its first two characters match <paramref
    /// name="first"/> and <paramref name="second"/> respectively; otherwise, <see langword="false"/>.</returns>
    internal static bool StartsWithChars(ReadOnlySpan<char> span, char first, char second)
    {
        // No need for a method like string.StartsWith for just two chars - too much overhead :P
        return span.Length >= 2 && span[0] == first && span[1] == second;
    }

    /// <summary>
    /// Determines whether all characters in the specified span are equal to the given character.
    /// </summary>
    /// <param name="span">The span of characters to examine.</param>
    /// <param name="c">The character to compare each element of the span against.</param>
    /// <returns>true if every character in the span is equal to the specified character; otherwise, false.</returns>
    internal static bool IsAllSameChar(ReadOnlySpan<char> span, char c)
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
    /// Determines whether the specified read-only character span ends with the specified character.
    /// </summary>
    /// <param name="span">The read-only span of characters to examine.</param>
    /// <param name="c">The character to compare to the last character of the span.</param>
    /// <returns>true if the last character of the span equals the specified character; otherwise, false. Returns false if the
    /// span is empty.</returns>
    internal static bool EndsWithChar(ReadOnlySpan<char> span, char c)
    {
        if (span.IsEmpty)
        {
            return false;
        }

        return span[^1] == c;
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
    internal static bool MatchWord(TextDocument document, int startOffset, ReadOnlySpan<char> word)
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

    #endregion String/ReadOnlySpan<char> Helpers
}
#pragma warning restore S3267 // Loops should be simplified with "LINQ" expressions
