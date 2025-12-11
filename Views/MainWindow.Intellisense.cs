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

using Avalonia.Input;
using AvaloniaEdit.CodeCompletion;
using MermaidPad.Models;
using MermaidPad.Models.Editor;
using MermaidPad.ObjectPoolPolicies;
using MermaidPad.Services.Editor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.Views;

/// <summary>
/// Represents the main application window that provides the editor interface and manages IntelliSense features for
/// authoring Mermaid diagrams.
/// </summary>
/// <remarks>The MainWindow class is responsible for initializing and coordinating code completion (IntelliSense)
/// within the editor, including handling user input events, displaying completion suggestions, and managing related
/// resources. It integrates with AvaloniaEdit to provide a responsive editing experience tailored for Mermaid diagram
/// syntax. This class is typically instantiated as the primary window of the application and should be used as the
/// entry point for editor-related functionality.</remarks>
#pragma warning disable IDE0078
[SuppressMessage("Style", "IDE0078:Use pattern matching", Justification = "Performance and code clarity")]
[SuppressMessage("ReSharper", "MergeIntoPattern", Justification = "Performance and code clarity")]
public partial class MainWindow
{
    private const int LookupTableSize = 128;
    private CompletionWindow? _completionWindow;
    private static readonly ObjectPool<HashSet<string>> _nodeBufferPool =
        new DefaultObjectPool<HashSet<string>>(new HashSetPooledObjectPolicy());

    // Persists known strings to avoid re-allocating "MyNode" repeatedly
    private readonly HashSet<string> _stringInternPool = new HashSet<string>(StringComparer.Ordinal);

    private readonly Dictionary<string, IntellisenseCompletionData> _wrapperCache =
        new Dictionary<string, IntellisenseCompletionData>(StringComparer.Ordinal);

    /// <summary>
    /// Indicates, for each character code, whether the character is considered a trigger character.
    /// </summary>
    /// <remarks>A trigger character is one that initiates a specific action or behavior in the parsing or
    /// processing logic. The array is indexed by character code, and a value of <see langword="true"/> at a given index
    /// means the corresponding character is a trigger character.</remarks>
    private static readonly bool[] _completionTriggerFlags = InitializeCompletionTriggerFlags();

    /// <summary>
    /// Initializes event handlers required to enable IntelliSense functionality in the editor.
    /// </summary>
    /// <remarks>Call this method during editor setup to ensure that IntelliSense features, such as code
    /// completion, are available to users. This method should be invoked before the user begins editing to guarantee
    /// correct behavior.</remarks>
    private void InitializeIntellisense()
    {
        Editor.TextArea.TextEntered += TextArea_TextEntered;
        Editor.TextArea.TextEntering += TextArea_TextEntering;
    }

    /// <summary>
    /// Initializes and returns a Boolean array indicating which ASCII characters are considered valid
    /// trigger characters to invoke Intellisense completion.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The returned array can be used to efficiently check whether a given ASCII character is a
    /// valid trigger character by using its code as an index.
    /// </para>
    /// <para>
    ///     NOTE: This method looks identical to <see cref="IntellisenseScanner.InitializeValidIdentifierFlags"/> but it serves a different purpose.
    ///     This method identifies: What keystroke should start/trigger the autocomplete popup?
    /// </para></remarks>
    /// <returns>A Boolean array of length <see cref="LookupTableSize"/> where each element is <see langword="true"/> if the corresponding ASCII character
    /// is a letter, digit, underscore ('_'), greater-than sign ('>'), or space character; otherwise, <see langword="false"/>.
    /// </returns>
    private static bool[] InitializeCompletionTriggerFlags()
    {
        bool[] flags = new bool[LookupTableSize];
        for (int i = 0; i < LookupTableSize; i++)
        {
            char c = (char)i;
            if ((c >= 'a' && c <= 'z') ||
                (c >= 'A' && c <= 'Z') ||
                (c >= '0' && c <= '9') ||
                c == '_' ||
                c == '>' ||
                c == ' ')
            {
                flags[i] = true;
            }
        }
        return flags;
    }

    /// <summary>
    /// Handles the TextEntering event for the text area, processing user input and managing completion window behavior
    /// during text entry.
    /// </summary>
    /// <remarks>This method is typically used to coordinate code completion or intellisense features in
    /// response to user typing. If a completion window is open and the entered text is not an identifier character, the
    /// method may trigger insertion requests to the completion list. This helps ensure that completion suggestions are
    /// managed appropriately as the user types.</remarks>
    /// <param name="sender">The source of the event, typically the text area control where text is being entered.</param>
    /// <param name="e">An object containing information about the text input event, including the entered text.</param>
    private void TextArea_TextEntering(object? sender, TextInputEventArgs e)
    {
        if (e.Text?.Length > 0 && _completionWindow is not null)
        {
            // If the user types a non-identifier character while the window is open,
            // we might want to manually close it, though AvaloniaEdit handles much of this.
            char c = e.Text[0];

            // Check if char is NOT a valid identifier part (Letter, Digit, Dash, Underscore)
            if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
            {
                _completionWindow.CompletionList.RequestInsertion(e);
            }
        }
    }

    /// <summary>
    /// Handles the event when text is entered into the text area and triggers the completion window if appropriate.
    /// </summary>
    /// <remarks>The completion window is only shown if a single character is entered and no completion window
    /// is currently open. This method does not perform any action if the entered text is empty or if a completion
    /// window is already displayed.</remarks>
    /// <param name="sender">The source of the event, typically the text area control where text input occurred.</param>
    /// <param name="e">An object containing information about the text input event, including the entered text.</param>
    private void TextArea_TextEntered(object? sender, TextInputEventArgs e)
    {
        // If no text entered or completion window already open, do nothing
        if (string.IsNullOrEmpty(e.Text) || _completionWindow is not null)
        {
            return;
        }

        char typedChar = e.Text[0];

#if DEBUG
        Stopwatch sw = Stopwatch.StartNew();
#endif
        // Check if we should open the window
        if (!ShouldTriggerCompletion(typedChar))
        {
            return;
        }

        ShowCompletionWindow();
#if DEBUG
        sw.Stop();
        _logger.LogDebug("Intellisense completion triggered in {ElapsedMilliseconds} ms", sw.ElapsedMilliseconds);
#endif
    }

    /// <summary>
    /// Determines whether typing the specified character should trigger code completion in the editor.
    /// </summary>
    /// <remarks>Completion is triggered for certain immediate characters, such as '>' and space, or when a
    /// word threshold is met (e.g., after typing at least one valid identifier character). The method does not trigger
    /// completion for non-trigger characters or when insufficient context is present.</remarks>
    /// <param name="typedChar">The character that was typed by the user must be a valid
    /// trigger character for completion to be considered.</param>
    /// <returns>true if code completion should be triggered for the specified character; otherwise, false.</returns>
    private static bool ShouldTriggerCompletion(char typedChar)
    {
        // NOTE: Threshold of "characters typed before triggering completion" is 1 for this editor.
        // If this is changed to use more than 1 character, we can use the logic below.
        return IsTriggerChar(typedChar);

        //// Immediate Triggers: arrows, spaces
        //if (typedChar == '>' || typedChar == ' ')
        //{
        //    return true;
        //}

        //// Word Triggers (Threshold Logic) - require 2-char threshold to avoid noise
        //// We know typedChar is Letter/Digit/_ because IsTriggerChar passed
        //int offset = Editor.CaretOffset;
        //if (offset < 2)
        //{
        //    return false;
        //}

        //// Look at the char BEFORE the one we just typed (offset - 2)
        //// Example: typed "b", text is "ab|". offset-1='b', offset-2='a'.
        //// If previous char is also a valid identifier part, we have a "word" started.
        //char prevChar = Editor.Document.GetCharAt(offset - 2);
        //return char.IsLetterOrDigit(prevChar) || prevChar == '_';
    }

    /// <summary>
    /// Displays the code completion window at the current caret position in the editor, providing relevant completion
    /// suggestions based on the document content.
    /// </summary>
    /// <remarks>If the document is empty, the completion window is not shown. Exceptions encountered during
    /// the completion process are logged and do not interrupt the user experience.</remarks>
    private void ShowCompletionWindow()
    {
        HashSet<string> reusableSet = _nodeBufferPool.Get();
        try
        {
            // NOTE: Accessing .Text allocates a string. This is unavoidable with standard AvaloniaEdit API.
            string docText = Editor.Text ?? string.Empty;
            if (string.IsNullOrEmpty(docText))
            {
                return;
            }

#if DEBUG
            Stopwatch sw = Stopwatch.StartNew();
#endif

            // Detect current diagram type using the indentation strategy cache
            MermaidIndentationStrategy? indentationStrategy = Editor.TextArea.IndentationStrategy as MermaidIndentationStrategy;
            DiagramType currentDiagramType = indentationStrategy?.GetCachedDiagramType(Editor.Document)
                ?? DiagramType.Unknown;

            // Get context-aware keywords
            IntellisenseCompletionData[] contextKeywords = IntellisenseKeywords.GetKeywordsForDiagramType(currentDiagramType);

            // Scan document for user-defined nodes/identifiers
            IntellisenseScanner scanner = new IntellisenseScanner(docText.AsSpan(), reusableSet, _stringInternPool);
            scanner.Scan();

            // Setup Window
            _completionWindow = new CompletionWindow(Editor.TextArea)
            {
                // Find the start of the word at the caret
                StartOffset = GetWordStartOffset(Editor.CaretOffset, docText)
            };

            IList<ICompletionData>? completionData = _completionWindow.CompletionList.CompletionData;
            PopulateCompletionData(completionData, reusableSet, contextKeywords);

            _completionWindow.Show();
            _completionWindow.Closed += CompletionWindow_Closed;

#if DEBUG
            sw.Stop();
            _logger.LogDebug("Intellisense completion triggered in {ElapsedMilliseconds} ms (Diagram: {DiagramType})",
                sw.ElapsedMilliseconds, currentDiagramType);
#endif
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error showing completion window");
            Debug.Fail("Error showing completion window", ex.ToString());
        }
        finally
        {
            // Return reusable set to pool
            _nodeBufferPool.Return(reusableSet);
        }
    }

    /// <summary>
    /// Populates the specified completion data list with context-aware keywords and scanned node entries, sorted
    /// alphabetically by <see cref="StringComparison.OrdinalIgnoreCase"/> for consistent ordering for use in code
    /// completion scenarios where "abc" and "ABC" should be grouped together.
    /// </summary>
    /// <remarks>This method combines "context-aware" keywords and scanned node names into a single,
    /// alphabetically sorted, by <see cref="StringComparison.OrdinalIgnoreCase"/>, list of completion data.
    /// The resulting items are added to the target list, which is displayed in an IntelliSense completion window in TextEditor.
    /// The method does not clear the target list before adding new items; callers should ensure the list is in the desired state prior to invocation.</remarks>
    /// <param name="targetList">The list to which the combined and sorted completion data will be added. Must not be null.</param>
    /// <param name="scannedNodes">A set of node names that have been scanned and should be included in the completion data. Cannot be null.</param>
    /// <param name="contextAwareKeywords">An array of context-specific completion keywords to be included in the completion data. Cannot be null.</param>
    [SuppressMessage("ReSharper", "SuggestBaseTypeForParameter", Justification = "AvaloniaEdit's CompletionList.CompletionData implements IList<ICompletionData>, not ICollection<ICompletionData>")]
    private void PopulateCompletionData(
        IList<ICompletionData> targetList,
        HashSet<string> scannedNodes,
        IntellisenseCompletionData[] contextAwareKeywords)
    {
        // Create a temporary buffer for sorting. It's pre-sized to Context Keywords + Scanned Nodes
        List<ICompletionData> tempList = new List<ICompletionData>(contextAwareKeywords.Length + scannedNodes.Count);

        // Make the CompletionWindow "context-aware" so that only relevant keywords are shown for the current diagram type.
        // So, only add context-aware keywords instead of all static keywords
        tempList.AddRange(contextAwareKeywords);

        // Add Scanned Nodes
        foreach (string nodeText in scannedNodes)
        {
            // Only allocate new wrapper if we've never seen this node before
            if (!_wrapperCache.TryGetValue(nodeText, out IntellisenseCompletionData? wrapper))
            {
                wrapper = new IntellisenseCompletionData(nodeText, 0, IntellisenseCompletionData.AbcIcon);
                _wrapperCache[nodeText] = wrapper;
            }
            tempList.Add(wrapper);
        }

        // Sort the combined list. Use ordinal ignore-case for consistent ordering to group similar items, regardless of casing.
        tempList.Sort(static (a, b) => string.Compare(a.Text, b.Text, StringComparison.OrdinalIgnoreCase));

        // Dump into the Window. Unfortunately CompletionData is not an implementation of List<T> that supports AddRange.
        // CompletionList.CompletionData only implements IList<ICompletionData>, so we have to iterate and add one-by-one.
        foreach (ICompletionData item in tempList)
        {
            targetList.Add(item);
        }
    }

    /// <summary>
    /// Finds the zero-based index of the first character of the word that precedes or contains the specified caret
    /// position within the given text.
    /// </summary>
    /// <remarks>Word characters are considered to be letters, digits, or underscores. Separators such as
    /// spaces, brackets, and punctuation mark word boundaries.</remarks>
    /// <param name="caretOffset">The zero-based caret position in the text for which to locate
    /// the start of the word. Must be between 0 and the length of <paramref name="text"/>.</param>
    /// <param name="text">The text in which to search for the word start. Cannot be null.</param>
    /// <returns>The zero-based index of the first character of the word at or before the specified
    /// caret position. Returns 0 if the caret is at the start of the text or if no word characters precede the caret.
    /// </returns>
    private static int GetWordStartOffset(int caretOffset, string text)
    {
        int i = caretOffset - 1;
        while (i >= 0)
        {
            char c = text[i];

            // Stop if we hit a separator (space, symbols, etc.)
            if (!char.IsLetterOrDigit(c) && c != '_')
            {
                return i + 1; // The character AFTER the separator is the start
            }

            i--;
        }
        return 0; // Start of document
    }

    /// <summary>
    /// Handles the Closed event of the completion window, performing necessary cleanup when the window is closed.
    /// </summary>
    /// <param name="sender">The source of the event, typically the completion window that was closed.</param>
    /// <param name="e">An EventArgs instance containing event data.</param>
    private void CompletionWindow_Closed(object? sender, EventArgs e)
    {
        if (_completionWindow is not null)
        {
            _completionWindow.Closed -= CompletionWindow_Closed;
            _completionWindow = null;
        }
    }

    /// <summary>
    /// Determines whether the specified character is recognized as a trigger character for code completion.
    /// </summary>
    /// <param name="c">The character to evaluate as a potential trigger for completion.</param>
    /// <returns>true if the character is a completion trigger; otherwise, false.</returns>
    private static bool IsTriggerChar(char c) => c < LookupTableSize && _completionTriggerFlags[c];

    /// <summary>
    /// Unsubscribes event handlers related to Intellisense functionality from the editor and completion window.
    /// </summary>
    /// <remarks>Call this method to detach Intellisense-related event handlers when the editor or completion
    /// window is no longer needed, such as during cleanup or disposal. This helps prevent memory leaks and unintended
    /// behavior from lingering event subscriptions.</remarks>
    private void UnsubscribeIntellisenseEventHandlers()
    {
        if (Editor is not null)
        {
            Editor.TextArea.TextEntered -= TextArea_TextEntered;
            Editor.TextArea.TextEntering -= TextArea_TextEntering;
        }

        if (_completionWindow is not null)
        {
            _completionWindow.Closed -= CompletionWindow_Closed;
            _completionWindow.Close();
            _completionWindow = null;
        }
    }
}
