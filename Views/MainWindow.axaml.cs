
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using MermaidPad.Services;
using MermaidPad.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly MermaidRenderer _renderer;
    private readonly MermaidUpdateService _updateService;
    private readonly IDebounceDispatcher _editorDebouncer;

    private bool _suppressEditorTextChanged = false;
    private bool _suppressEditorStateSync = false; // Prevent circular updates

    public MainWindow()
    {
        InitializeComponent();

        IServiceProvider sp = App.Services;
        _editorDebouncer = sp.GetRequiredService<IDebounceDispatcher>();
        _renderer = sp.GetRequiredService<MermaidRenderer>();
        _vm = sp.GetRequiredService<MainViewModel>();
        _updateService = sp.GetRequiredService<MermaidUpdateService>();
        DataContext = _vm;

        SimpleLogger.Log((_vm.LatestMermaidVersion == null).ToString());
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
        // Editor -> ViewModel synchronization
        Editor.TextChanged += (_, _) =>
        {
            if (_suppressEditorTextChanged)
            {
                return;
            }

            // Debounce to avoid excessive updates
            _editorDebouncer.Debounce("editor-text", TimeSpan.FromMilliseconds(DebounceDispatcher.DefaultTextDebounceMilliseconds), () =>
            {
                if (_vm.DiagramText != Editor.Text)
                {
                    SimpleLogger.Log($"Editor text changed, updating ViewModel ({_vm.DiagramText.Length} -> {Editor.Text.Length} chars)");
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
            });
        };

        // Editor selection/caret -> ViewModel synchronization
        // Hook into TextArea events since TextEditor doesn't expose SelectionChanged directly
        Editor.TextArea.SelectionChanged += (_, _) =>
        {
            if (_suppressEditorStateSync) return;

            _editorDebouncer.Debounce("editor-selection", TimeSpan.FromMilliseconds(DebounceDispatcher.DefaultCaretDebounceMilliseconds), () =>
            {
                _suppressEditorStateSync = true;
                try
                {
                    _vm.EditorSelectionStart = Editor.SelectionStart;
                    _vm.EditorSelectionLength = Editor.SelectionLength;
                    _vm.EditorCaretOffset = Editor.CaretOffset;

                    SimpleLogger.Log($"Editor selection synced to ViewModel: Start={Editor.SelectionStart}, Length={Editor.SelectionLength}, Caret={Editor.CaretOffset}");
                }
                finally
                {
                    _suppressEditorStateSync = false;
                }
            });
        };

        // Also hook into caret position changes for more comprehensive coverage
        Editor.TextArea.Caret.PositionChanged += (_, _) =>
        {
            if (_suppressEditorStateSync) return;

            _editorDebouncer.Debounce("editor-caret", TimeSpan.FromMilliseconds(DebounceDispatcher.DefaultCaretDebounceMilliseconds), () =>
            {
                _suppressEditorStateSync = true;
                try
                {
                    // Update caret offset when caret position changes
                    _vm.EditorCaretOffset = Editor.CaretOffset;

                    SimpleLogger.Log($"Editor caret synced to ViewModel: Caret={Editor.CaretOffset}");
                }
                finally
                {
                    _suppressEditorStateSync = false;
                }
            });
        };

        // ViewModel -> Editor synchronization
        _vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressEditorStateSync) return;

        switch (e.PropertyName)
        {
            case nameof(_vm.DiagramText):
                if (Editor.Text != _vm.DiagramText)
                {
                    _editorDebouncer.Debounce("vm-text", TimeSpan.FromMilliseconds(DebounceDispatcher.DefaultTextDebounceMilliseconds), () =>
                    {
                        SimpleLogger.Log($"ViewModel text changed, updating Editor ({Editor.Text.Length} -> {_vm.DiagramText.Length} chars)");
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
                    });
                }
                break;

            case nameof(_vm.EditorSelectionStart):
            case nameof(_vm.EditorSelectionLength):
            case nameof(_vm.EditorCaretOffset):
                _editorDebouncer.Debounce("vm-selection", TimeSpan.FromMilliseconds(DebounceDispatcher.DefaultCaretDebounceMilliseconds), () =>
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

                            SimpleLogger.Log($"ViewModel selection synced to Editor: Start={validSelectionStart}, Length={validSelectionLength}, Caret={validCaretOffset}");
                        }
                    }
                    finally
                    {
                        _suppressEditorStateSync = false;
                    }
                });
                break;
        }
    }

    private void BringFocusToEditor()
    {
        Dispatcher.UIThread.Post(() =>
        {
            // Make sure caret is visible:
            Editor.TextArea.Caret.CaretBrush = new SolidColorBrush(Colors.Red);

            // Ensure selection is visible
            Editor.TextArea.SelectionBrush = new SolidColorBrush(Colors.SteelBlue);
            if (!Editor.IsFocused)
            {
                Editor.Focus();
            }

            // after focusing, ensure the caret is visible
            Editor.TextArea.Caret.BringCaretToView();
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
            // TODO - show a message to the user
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

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        SimpleLogger.Log("Close button clicked");
        Close();
    }
}
