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

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.ViewModels.Panels;

/// <summary>
/// ViewModel for the Editor panel, handling diagram text editing and editor state.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeMadeStatic.Global", Justification = "ViewModel properties are instance-based for binding.")]
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global", Justification = "ViewModel properties are set during initialization by the MVVM framework.")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global", Justification = "ViewModel properties are accessed by the view for data binding.")]
[SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "ViewModel members are accessed by the view for data binding.")]
public sealed partial class EditorViewModel : ViewModelBase
{
    private readonly ILogger<EditorViewModel> _logger;

    /// <summary>
    /// A value tracking if there is currently a file being loaded to suppress change events.
    /// </summary>
    private bool _suppressChangeEvents;

    /// <summary>
    /// Event raised when the diagram text changes. Used for communication with PreviewViewModel.
    /// </summary>
    public event EventHandler<string>? DiagramTextChanged;

    /// <summary>
    /// Gets or sets the current diagram text.
    /// </summary>
    [ObservableProperty]
    public partial string DiagramText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the selection start index in the editor.
    /// </summary>
    [ObservableProperty]
    public partial int EditorSelectionStart { get; set; }

    /// <summary>
    /// Gets or sets the selection length in the editor.
    /// </summary>
    [ObservableProperty]
    public partial int EditorSelectionLength { get; set; }

    /// <summary>
    /// Gets or sets the caret offset in the editor.
    /// </summary>
    [ObservableProperty]
    public partial int EditorCaretOffset { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether there is available content to copy to the clipboard.
    /// </summary>
    [ObservableProperty]
    public partial bool CanCopyClipboard { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether there is available content in the clipboard to paste.
    /// </summary>
    [ObservableProperty]
    public partial bool CanPasteClipboard { get; set; }

    /// <summary>
    /// Gets a value indicating whether there is text in the editor.
    /// </summary>
    public bool HasText => !string.IsNullOrWhiteSpace(DiagramText);

    /// <summary>
    /// Initializes a new instance of the MainWindow class using application-level services.
    /// </summary>
    /// <remarks>
    /// <para>This constructor retrieves required services from the application's dependency injection
    /// container to configure the main window. It is typically used when creating the main window at application
    /// startup.</para>
    /// <para>
    /// This constructor lives specifically for the purpose of avoiding this warning:
    ///     AVLN3001: XAML resource "avares://MermaidPad/Views/Panels/EditorPanel.axaml" won't be reachable via runtime loader, as no public constructor was found
    /// </para>
    /// </remarks>
    public EditorViewModel()
        : this(App.Services.GetRequiredService<ILogger<EditorViewModel>>())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EditorViewModel"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for this view model.</param>
    public EditorViewModel(ILogger<EditorViewModel> logger)
    {
        _logger = logger;
        _logger.LogInformation("EditorViewModel initialized");
    }

    /// <summary>
    /// Sets the editor state with validation, optionally suppressing change events.
    /// </summary>
    /// <param name="text">The diagram text to set.</param>
    /// <param name="selectionStart">The selection start position.</param>
    /// <param name="selectionLength">The selection length.</param>
    /// <param name="caretOffset">The caret offset position.</param>
    /// <param name="suppressEvents">Whether to suppress change events during this update.</param>
    /// <exception cref="ArgumentNullException">Thrown if the provided text is null.</exception>
    public void SetEditorState(string text, int selectionStart, int selectionLength, int caretOffset, bool suppressEvents = false)
    {
        ArgumentNullException.ThrowIfNull(text);        // allowed to be empty or whitespace, but not null
        if (suppressEvents)
        {
            _suppressChangeEvents = true;
        }

        try
        {
            DiagramText = text;
            EditorSelectionStart = selectionStart;
            EditorSelectionLength = selectionLength;
            EditorCaretOffset = caretOffset;

            _logger.LogDebug(
                "Editor state set: Text length={TextLength}, Selection={Start},{Length}, Caret={Caret}",
                text.Length,
                selectionStart,
                selectionLength,
                caretOffset);
        }
        finally
        {
            if (suppressEvents)
            {
                _suppressChangeEvents = false;
            }
        }
    }

    /// <summary>
    /// Clears the diagram text and resets the editor selection and caret position.
    /// </summary>
    public void Clear()
    {
        DiagramText = string.Empty;
        EditorSelectionStart = 0;
        EditorSelectionLength = 0;
        EditorCaretOffset = 0;
        _logger.LogInformation("Editor cleared");
    }

    /// <summary>
    /// Handles changes to the diagram text and raises the DiagramTextChanged event.
    /// </summary>
    /// <param name="value">The new value of the diagram text.</param>
    partial void OnDiagramTextChanged(string value)
    {
        if (_suppressChangeEvents)
        {
            return;
        }

        OnPropertyChanged(nameof(HasText));

        // Raise event for communication with PreviewViewModel
        DiagramTextChanged?.Invoke(this, value);
    }
}
