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

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using MermaidPad.Exceptions.Assets;
using MermaidPad.Services;
using MermaidPad.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.Views;

public sealed partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly MermaidRenderer _renderer;
    private readonly MermaidUpdateService _updateService;
    private readonly IDebounceDispatcher _editorDebouncer;

    private bool _suppressEditorTextChanged;
    private bool _suppressEditorStateSync; // Prevent circular updates

    public MainWindow()
    {
        InitializeComponent();

        IServiceProvider sp = App.Services;
        _editorDebouncer = sp.GetRequiredService<IDebounceDispatcher>();
        _renderer = sp.GetRequiredService<MermaidRenderer>();
        _vm = sp.GetRequiredService<MainViewModel>();
        _updateService = sp.GetRequiredService<MermaidUpdateService>();
        DataContext = _vm;

        SimpleLogger.Log("=== MainWindow Initialization Started ===");
        SimpleLogger.Log("Window created, services resolved from DI container");

        Opened += OnOpened;
        Closing += OnClosing;

        // Focus the editor when the window is activated
        Activated += (_, _) =>
        {
            SimpleLogger.Log("Window activated, bringing focus to editor");
            BringFocusToEditor();
            //Dispatcher.UIThread.Post(() =>
            //{
            //    IInputElement? focused = FocusManager?.GetFocusedElement();
            //    Debug.WriteLine($"[Activated] Focused control: {focused?.GetType().Name ?? "null"}");

            //    // Make sure caret is visible:
            //    Editor.TextArea.Caret.CaretBrush = new SolidColorBrush(Colors.Red);

            //    // Ensure selection is visible
            //    Editor.TextArea.SelectionBrush = new SolidColorBrush(Colors.SteelBlue);
            //    if (!Editor.IsFocused)
            //    {
            //        Editor.Focus();
            //        Editor.TextArea.Caret.BringCaretToView();
            //    }
            //    IInputElement? afterFocus = FocusManager?.GetFocusedElement();
            //    Debug.WriteLine($"[Activated] After Editor.Focus(), focused control: {afterFocus?.GetType().Name ?? "null"}");
            //}, DispatcherPriority.Background);
        };

        // Initialize editor with ViewModel data using validation
        SetEditorStateWithValidation(
            _vm.DiagramText,
            _vm.EditorSelectionStart,
            _vm.EditorSelectionLength,
            _vm.EditorCaretOffset
        );

        SimpleLogger.Log($"Editor initialized with {_vm.DiagramText.Length} characters");

        // Set up two-way synchronization between Editor and ViewModel
        SetupEditorViewModelSync();

        SimpleLogger.Log("=== MainWindow Initialization Completed ===");
    }

    private void SetEditorStateWithValidation(string text, int selectionStart, int selectionLength, int caretOffset)
    {
        _suppressEditorStateSync = true; // Prevent circular updates during initialization
        try
        {
            Editor.Text = text;

            // Ensure selection bounds are valid
            int textLength = text.Length;
            int validSelectionStart = Math.Max(0, Math.Min(selectionStart, textLength));
            int validSelectionLength = Math.Max(0, Math.Min(selectionLength, textLength - validSelectionStart));
            int validCaretOffset = Math.Max(0, Math.Min(caretOffset, textLength));
            Editor.SelectionStart = validSelectionStart;
            Editor.SelectionLength = validSelectionLength;
            Editor.CaretOffset = validCaretOffset;

            SimpleLogger.Log($"Editor state set: Start={validSelectionStart}, Length={validSelectionLength}, Caret={validCaretOffset} (text length: {textLength})");
        }
        finally
        {
            _suppressEditorStateSync = false;
        }
    }

    private void SetupEditorViewModelSync()
    {
        // Editor -> ViewModel synchronization (text)
        Editor.TextChanged += (_, _) =>
        {
            if (_suppressEditorTextChanged)
            {
                return;
            }

            // Debounce to avoid excessive updates
            _editorDebouncer.DebounceOnUI("editor-text", TimeSpan.FromMilliseconds(DebounceDispatcher.DefaultTextDebounceMilliseconds), () =>
            {
                if (_vm.DiagramText != Editor.Text)
                {
                    _suppressEditorStateSync = true;
                    try
                    {
                        _vm.DiagramText = Editor.Text;
                    }
                    finally
                    {
                        _suppressEditorStateSync = false;
                    }
                }
            },
            DispatcherPriority.Background);
        };

        // Editor selection/caret -> ViewModel: subscribe to both, coalesce into one update
        Editor.TextArea.SelectionChanged += (_, _) =>
        {
            if (_suppressEditorStateSync)
            {
                return;
            }

            ScheduleEditorStateSyncIfNeeded();
        };

        Editor.TextArea.Caret.PositionChanged += (_, _) =>
        {
            if (_suppressEditorStateSync)
            {
                return;
            }

            ScheduleEditorStateSyncIfNeeded();
        };

        // ViewModel -> Editor synchronization
        _vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    // Coalesce caret + selection updates, and skip no-ops
    private void ScheduleEditorStateSyncIfNeeded()
    {
        int selectionStart = Editor.SelectionStart;
        int selectionLength = Editor.SelectionLength;
        int caretOffset = Editor.CaretOffset;

        if (selectionStart == _vm.EditorSelectionStart &&
            selectionLength == _vm.EditorSelectionLength &&
            caretOffset == _vm.EditorCaretOffset)
        {
            return; // nothing changed
        }

        _editorDebouncer.DebounceOnUI("editor-state", TimeSpan.FromMilliseconds(DebounceDispatcher.DefaultCaretDebounceMilliseconds), () =>
        {
            _suppressEditorStateSync = true;
            try
            {
                // Take the latest values at execution time to coalesce multiple events
                _vm.EditorSelectionStart = Editor.SelectionStart;
                _vm.EditorSelectionLength = Editor.SelectionLength;
                _vm.EditorCaretOffset = Editor.CaretOffset;
            }
            finally
            {
                _suppressEditorStateSync = false;
            }
        },
        DispatcherPriority.Background);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressEditorStateSync)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(_vm.DiagramText):
                if (Editor.Text != _vm.DiagramText)
                {
                    _editorDebouncer.DebounceOnUI("vm-text", TimeSpan.FromMilliseconds(DebounceDispatcher.DefaultTextDebounceMilliseconds), () =>
                    {
                        _suppressEditorTextChanged = true;
                        _suppressEditorStateSync = true;
                        try
                        {
                            Editor.Text = _vm.DiagramText;
                        }
                        finally
                        {
                            _suppressEditorTextChanged = false;
                            _suppressEditorStateSync = false;
                        }
                    },
                    DispatcherPriority.Background);
                }
                break;

            case nameof(_vm.EditorSelectionStart):
            case nameof(_vm.EditorSelectionLength):
            case nameof(_vm.EditorCaretOffset):
                _editorDebouncer.DebounceOnUI("vm-selection", TimeSpan.FromMilliseconds(DebounceDispatcher.DefaultCaretDebounceMilliseconds), () =>
                {
                    _suppressEditorStateSync = true;
                    try
                    {
                        // Validate bounds before setting
                        int textLength = Editor.Text.Length;
                        int validSelectionStart = Math.Max(0, Math.Min(_vm.EditorSelectionStart, textLength));
                        int validSelectionLength = Math.Max(0, Math.Min(_vm.EditorSelectionLength, textLength - validSelectionStart));
                        int validCaretOffset = Math.Max(0, Math.Min(_vm.EditorCaretOffset, textLength));

                        if (Editor.SelectionStart != validSelectionStart ||
                            Editor.SelectionLength != validSelectionLength ||
                            Editor.CaretOffset != validCaretOffset)
                        {
                            Editor.SelectionStart = validSelectionStart;
                            Editor.SelectionLength = validSelectionLength;
                            Editor.CaretOffset = validCaretOffset;
                        }
                    }
                    finally
                    {
                        _suppressEditorStateSync = false;
                    }
                },
                DispatcherPriority.Background);
                break;
        }
    }

    private void BringFocusToEditor()
    {
        Dispatcher.UIThread.Post(() =>
        {
            // Suppress event reactions during programmatic focus/caret adjustments
            _suppressEditorStateSync = true;
            try
            {
                // Make sure caret is visible:
                Editor.TextArea.Caret.CaretBrush = new SolidColorBrush(Colors.Red);

                // Ensure selection is visible
                Editor.TextArea.SelectionBrush = new SolidColorBrush(Colors.SteelBlue);
                if (!Editor.IsFocused)
                {
                    Editor.Focus();
                }
                Editor.TextArea.Caret.BringCaretToView();
            }
            finally
            {
                _suppressEditorStateSync = false;
            }
        }, DispatcherPriority.Background);
    }

    [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "Event handler")]
    private async void OnOpened(object? sender, EventArgs e)
    {
        try
        {
            SimpleLogger.Log("Window opened event triggered");
            await OnOpenedAsync();
            BringFocusToEditor();
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError("Unhandled exception in OnOpened", ex);

            //TODO - show a message to the user
            //await Dispatcher.UIThread.InvokeAsync(async () =>
            //{
            //    await MessageBox.ShowAsync(this, "An error occurred while opening the window. Please try again.", "Error", MessageBox.MessageBoxButtons.Ok, MessageBox.MessageBoxIcon.Error);
            //});
        }
    }

    private async Task OnOpenedAsync()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        SimpleLogger.Log("=== Window Opened Sequence Started ===");

        try
        {
            // Step 1: Check for Mermaid updates
            SimpleLogger.Log("Step 1: Checking for Mermaid updates...");
            await _vm.CheckForMermaidUpdatesAsync();
            SimpleLogger.Log("Mermaid update check completed");

            // Step 2: Initialize WebView (editor state is already synchronized via constructor)
            SimpleLogger.Log("Step 2: Initializing WebView...");
            string? assetsPath = Path.GetDirectoryName(_updateService.BundledMermaidPath);
            if (assetsPath is null)
            {
                const string error = "BundledMermaidPath does not contain a directory component";
                SimpleLogger.LogError(error);
                throw new InvalidOperationException(error);
            }

            await InitializeWebViewAsync();

            // Step 3: Update command states
            SimpleLogger.Log("Step 3: Updating command states...");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _vm.RenderCommand.NotifyCanExecuteChanged();
                _vm.ClearCommand.NotifyCanExecuteChanged();
            });

            stopwatch.Stop();
            SimpleLogger.LogTiming("Window opened sequence", stopwatch.Elapsed, success: true);
            SimpleLogger.Log("=== Window Opened Sequence Completed Successfully ===");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            SimpleLogger.LogTiming("Window opened sequence", stopwatch.Elapsed, success: false);
            SimpleLogger.LogError("Window opened sequence failed", ex);
            throw;
        }
    }

    [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "Event handler")]
    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        try
        {
            SimpleLogger.Log("Window closing, saving current state...");

            // Since we now have real-time sync, the ViewModel is already up-to-date
            // Just persist the current state
            _vm.Persist();

            // Dispose the renderer (stops HTTP server if running)
            if (_renderer is IAsyncDisposable disposableRenderer)
            {
                await disposableRenderer.DisposeAsync();
                SimpleLogger.Log("MermaidRenderer disposed");
            }

            SimpleLogger.Log("Window state saved successfully");
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError("Failed to save window state during close", ex);
        }
    }

    private async Task InitializeWebViewAsync()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        SimpleLogger.Log("=== WebView Initialization Started ===");

        // Temporarily disable live preview during WebView initialization
        bool originalLivePreview = _vm.LivePreviewEnabled;
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _vm.LivePreviewEnabled = false;
            SimpleLogger.Log($"Temporarily disabled live preview (was: {originalLivePreview})");
        });

        try
        {
            // Step 1: Initialize the MermaidRenderer
            await _renderer.InitializeAsync(Preview);

            // Step 2: Wait for content to load
            SimpleLogger.Log("Waiting for WebView content loading...");
            await Task.Delay(1_000);

            // Step 3: Perform initial render
            SimpleLogger.Log("Performing initial Mermaid render...");
            await _renderer.RenderAsync(_vm.DiagramText);

            stopwatch.Stop();
            SimpleLogger.LogTiming("WebView initialization", stopwatch.Elapsed, success: true);
            SimpleLogger.Log("=== WebView Initialization Completed Successfully ===");
        }
        catch (AssetIntegrityException)
        {
            throw;
        }
        catch (MissingAssetException)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            SimpleLogger.LogTiming("WebView initialization", stopwatch.Elapsed, success: false);
            SimpleLogger.LogError("WebView initialization failed", ex);
        }
        finally
        {
            // Re-enable live preview after WebView is ready
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _vm.LivePreviewEnabled = originalLivePreview;
                SimpleLogger.Log($"Re-enabled live preview: {originalLivePreview}");
            });
        }
    }

    [SuppressMessage("ReSharper", "UnusedParameter.Local")]
    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        SimpleLogger.Log("Close button clicked");
        Close();
    }
}
