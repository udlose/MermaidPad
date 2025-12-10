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

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.Services.Editor;

/// <summary>
/// Provides functionality for scanning text to identify and process valid identifiers for intellisense suggestions,
/// efficiently skipping insignificant characters and comments.
/// </summary>
/// <remarks>The IntellisenseScanner is designed for high-performance parsing scenarios where only relevant
/// identifiers need to be extracted from a text buffer, such as in code editors or diagram tools. It uses lookup tables
/// and intern pools to optimize memory usage and avoid duplicate processing. The type is a readonly ref struct,
/// ensuring stack-only allocation and preventing heap usage, which is beneficial for large or frequent scans. Thread
/// safety is not guaranteed; each instance should be used on a single thread.</remarks>
#pragma warning disable IDE0078
[SuppressMessage("Style", "IDE0078:Use pattern matching", Justification = "Performance and code clarity")]
[SuppressMessage("ReSharper", "MergeIntoPattern", Justification = "Performance and code clarity")]
public readonly ref struct IntellisenseScanner
{
    private readonly ReadOnlySpan<char> _text;
    private readonly HashSet<string> _results;
    private readonly HashSet<string> _internPool;

    private const int LookupTableSize = 128;
    private const char SingleSpaceChar = ' ';

    /// <summary>
    /// Contains a set of keywords that are ignored during diagram parsing or processing.
    /// </summary>
    /// <remarks>The set includes reserved words and directives from various diagram types, such as
    /// flowcharts, sequence diagrams, class diagrams, state diagrams, ER diagrams, Gantt charts, pie charts, Git
    /// graphs, journey diagrams, mindmaps, requirement diagrams, and C4 diagrams. These keywords are excluded to
    /// prevent them from being treated as user-defined identifiers or content.</remarks>
    private static readonly FrozenSet<string> _ignoredKeywords = FrozenSet.ToFrozenSet(
        [
            // Graph / Flowchart
            "graph", "flowchart", "subgraph", "end", "style", "classDef", "click", "linkStyle",
            "TD", "TB", "BT", "RL", "LR",

            // Sequence Diagram
            "sequenceDiagram", "participant", "actor", "boundary", "control", "entity", "database", "box", "loop", "alt", "else", "opt", "par", "rect", "autonumber", "activate", "deactivate",

            // Class Diagram
            "classDiagram", "class", "interface", "namespace", "cssClass", "callback", "link",

            // State Diagram
            "stateDiagram", "stateDiagram-v2", "state", "note",

            // ER Diagram
            "erDiagram",

            // Gantt
            "gantt", "dateFormat", "axisFormat", "title", "section", "excludes", "todayMarker",

            // Pie
            "pie", "showData",

            // GitGraph
            "gitGraph", "commit", "branch", "merge", "checkout", "cherry-pick", "reset",

            // Journey
            "journey", "section",

            // Mindmap
            "mindmap", "root",

            // Requirement
            "requirementDiagram", "requirement", "functionalRequirement", "interfaceRequirement", "performanceRequirement", "physicalRequirement", "designConstraint",

            // C4
            "C4Context", "C4Container", "C4Component", "C4Dynamic", "C4Deployment",

            // Common
            "%%"
        ],
        StringComparer.Ordinal
    );

    /// <summary>
    /// Provides an alternate lookup for keywords using a read-only character span as the key.
    /// </summary>
    /// <remarks>This lookup enables efficient keyword matching against read-only spans of characters, which
    /// can improve performance when parsing or analyzing text without allocating new strings. The lookup is
    /// case-sensitive unless otherwise specified by the underlying set configuration.</remarks>
    private static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> _keywordLookup =
        _ignoredKeywords.GetAlternateLookup<ReadOnlySpan<char>>();

    private static readonly bool[] _validIdentifierFlags = InitializeValidIdentifierFlags();

    /// <summary>
    /// Initializes and returns a lookup table indicating which ASCII characters are valid for use in identifiers.
    /// </summary>
    /// <remarks>
    /// <para>The returned array can be used to efficiently check whether a character is valid in an
    /// identifier according to standard ASCII rules. The lookup table covers all characters in the range defined by
    /// <c>LookupTableSize</c>.
    /// </para>
    /// <para>
    ///     NOTE: This method looks identical to <c>MainWindow.InitializeTriggerFlags</c> but it serves a different purpose.
    ///     This method identifies: What is a valid part of a Node Name?
    /// </para>
    /// </remarks>
    /// <returns>A Boolean array where each element corresponds to an ASCII character; the value is <see langword="true"/> if the
    /// character is a letter, digit, or underscore, and <see langword="false"/> otherwise.</returns>
    private static bool[] InitializeValidIdentifierFlags()
    {
        bool[] flags = new bool[LookupTableSize];
        for (int i = 0; i < LookupTableSize; i++)
        {
            char c = (char)i;

            // Allow standard identifiers
            // NOTE: Some mermaid types allow '.' or '-' in names, but strictly
            // speaking those are usually separators. We stick to word chars for safety.
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_')
            {
                flags[i] = true;
            }
        }
        return flags;
    }

    /// <summary>
    /// Initializes a new instance of the IntellisenseScanner class with the specified text to scan, result set, and
    /// intern pool.
    /// </summary>
    /// <param name="text">The read-only span of characters representing the text to be scanned for intellisense suggestions.</param>
    /// <param name="results">The set that will store the results of the scan. Must not be null.</param>
    /// <param name="internPool">The set used to intern strings during scanning to optimize memory usage.
    /// Must not be null.</param>
    public IntellisenseScanner(ReadOnlySpan<char> text, HashSet<string> results, HashSet<string> internPool)
    {
        _text = text;
        _results = results;
        _internPool = internPool;
    }

    /// <summary>
    /// Scans the input text and processes identifiers, skipping insignificant characters such as whitespace and
    /// comments.
    /// </summary>
    /// <remarks>This method advances through the text, identifying and handling relevant tokens while
    /// ignoring non-essential content. It is typically used as part of a parsing or tokenization process. The method
    /// does not return a value; instead, it updates internal state or collections as necessary.</remarks>
    public void Scan()
    {
        int index = 0;
        int length = _text.Length;

        HashSet<string>.AlternateLookup<ReadOnlySpan<char>> resultLookup = _results.GetAlternateLookup<ReadOnlySpan<char>>();
        HashSet<string>.AlternateLookup<ReadOnlySpan<char>> internLookup = _internPool.GetAlternateLookup<ReadOnlySpan<char>>();

        while (index < length)
        {
            // Fast Skip (Whitespace / Comments)
            int newIndex = SkipInsignificant(index, length);
            if (newIndex != index)
            {
                index = newIndex;
                continue;
            }

            // Process Identifier
            if (TryProcessIdentifier(ref index, length, resultLookup, internLookup))
            {
                continue;
            }

            // Fallback: Skip character
            index++;
        }
    }

    /// <summary>
    /// Advances the specified index past insignificant characters, such as whitespace or comment lines, in the text
    /// buffer.
    /// </summary>
    /// <remarks>Insignificant characters include whitespace and lines beginning with a double percent sign
    /// (%%), which are treated as comments. This method does not modify the underlying text buffer.</remarks>
    /// <param name="i">The zero-based index in the text buffer from which to begin skipping insignificant characters.</param>
    /// <param name="length">The total length of the text buffer. Used to ensure bounds are not exceeded when scanning for insignificant
    /// characters.</param>
    /// <returns>The index of the next significant character in the text buffer, or the incremented index if only a single
    /// whitespace character was skipped.</returns>
    private int SkipInsignificant(int i, int length)
    {
        char c = _text[i];

        // Whitespace: Skip 1 char
        // Treat all control characters (ASCII 0-32) as whitespace for performance
        // This is a fast alternative to char.IsWhiteSpace(c), but less explicit
        if (c <= SingleSpaceChar)
        {
            return i + 1;
        }

        // Skip Comments (%%)
        if (c == '%' && (i + 1 < length) && _text[i + 1] == '%')
        {
            i += 2;
            while (i < length)
            {
                char check = _text[i];
                if (check == '\n' || check == '\r')
                {
                    break;
                }

                i++;
            }
            // Fall through to return i (pointing at newline)
        }

        // No change
        return i;
    }

    /// <summary>
    /// Attempts to process an identifier at the specified position in the text, updating the index and managing lookup
    /// pools as needed.
    /// </summary>
    /// <remarks>If the identifier is a keyword or has already been processed in the current scan, it will not
    /// be added to the results. Identifiers are interned and added to the results only if they are valid and not
    /// duplicates. The method does not throw exceptions for invalid input; it returns false if the position does not
    /// start a valid identifier.</remarks>
    /// <param name="index">The current position within the text to start processing. On return, updated to point just past the processed
    /// identifier if successful.</param>
    /// <param name="length">The total length of the text to be scanned. Must be greater than zero and not exceed the bounds of the text.</param>
    /// <param name="resultLookup">A lookup structure used to track identifiers already processed during the current scan. Prevents duplicate
    /// processing within the scan.</param>
    /// <param name="internLookup">A lookup structure used to intern identifier strings, ensuring that each unique identifier is stored only once.</param>
    /// <returns>true if a valid identifier was found and processed at the specified position; otherwise, false.</returns>
    private bool TryProcessIdentifier(
        ref int index,
        int length,
        HashSet<string>.AlternateLookup<ReadOnlySpan<char>> resultLookup,
        HashSet<string>.AlternateLookup<ReadOnlySpan<char>> internLookup)
    {
        char c = _text[index];

        // Check start of identifier
        if (c >= LookupTableSize || !_validIdentifierFlags[c])
        {
            return false;
        }

        int start = index;
        index++;

        // Consume identifier
        while (index < length)
        {
            char nextC = _text[index];
            if (nextC >= LookupTableSize || !_validIdentifierFlags[nextC])
            {
                break;
            }

            index++;
        }

        ReadOnlySpan<char> wordSpan = _text[start..index];

        // Filter keywords
        if (_keywordLookup.Contains(wordSpan))
        {
            return true;
        }

        // Filter duplicates already in this scan
        if (resultLookup.Contains(wordSpan))
        {
            return true;
        }

        // If it's a word and not a keyword, it's a candidate.
        // Check intern pool
        if (!internLookup.TryGetValue(wordSpan, out string? textValue))
        {
            // Allocate new string and add to intern pool
            textValue = wordSpan.ToString();
            _internPool.Add(textValue);
        }

        _results.Add(textValue);
        return true;
    }
}
