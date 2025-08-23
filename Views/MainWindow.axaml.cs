using Avalonia.Controls;
using Avalonia.Input;
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

    private bool _suppressEditorTextChanged = false;

    public MainWindow()
    {
        InitializeComponent();

        IServiceProvider sp = App.Services;
        IDebounceDispatcher editorDebouncer = sp.GetRequiredService<IDebounceDispatcher>();
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

        // All constructors run on UI thread, so it's safe to access Editor control directly without marshaling
        Editor.Text = _vm.DiagramText;
        SimpleLogger.Log($"Editor initialized with {_vm.DiagramText.Length} characters");

        // Ensure selection bounds are valid
        int textLength = _vm.DiagramText.Length;
        int selectionStart = Math.Max(0, Math.Min(_vm.EditorSelectionStart, textLength));
        int selectionLength = Math.Max(0, Math.Min(_vm.EditorSelectionLength, textLength - selectionStart));
        int caretOffset = Math.Max(0, Math.Min(_vm.EditorCaretOffset, textLength));
        Editor.SelectionLength = selectionLength;
        Editor.SelectionStart = selectionStart;
        Editor.CaretOffset = caretOffset;

        SimpleLogger.Log($"Editor selection restored: Start={selectionStart}, Length={selectionLength}, Caret={caretOffset}");

        Editor.TextChanged += (_, _) =>
        {
            if (_suppressEditorTextChanged)
            {
                return;
            }

            // Debounce to avoid excessive updates
            editorDebouncer.Debounce("editor-text", TimeSpan.FromMilliseconds(DebounceDispatcher.DefaultDebounceMilliseconds), () =>
            {
                if (_vm.DiagramText != Editor.Text)
                {
                    SimpleLogger.Log($"Editor text changed, updating ViewModel ({_vm.DiagramText.Length} -> {Editor.Text.Length} chars)");
                    _vm.DiagramText = Editor.Text;
                }
            });
        };

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(_vm.DiagramText) && Editor.Text != _vm.DiagramText)
            {
                // Debounce to avoid excessive updates
                editorDebouncer.Debounce("vm-text", TimeSpan.FromMilliseconds(DebounceDispatcher.DefaultDebounceMilliseconds), () =>
                {
                    SimpleLogger.Log($"ViewModel text changed, updating Editor ({Editor.Text.Length} -> {_vm.DiagramText.Length} chars)");
                    _suppressEditorTextChanged = true;
                    Editor.Text = _vm.DiagramText;
                    _suppressEditorTextChanged = false;
                });
            }
        };

        SimpleLogger.Log("=== MainWindow Initialization Completed ===");
    }

    private void BringFocusToEditor()
    {
        Dispatcher.UIThread.Post(() =>
        {
            IInputElement? focused = FocusManager?.GetFocusedElement();
            SimpleLogger.Log($"Current focused control: {focused?.GetType().Name ?? "null"}");

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

            IInputElement? afterFocus = FocusManager?.GetFocusedElement();
            SimpleLogger.Log($"After focus attempt: {afterFocus?.GetType().Name ?? "null"}");
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

            // Step 2: Restore editor state
            SimpleLogger.Log("Step 2: Restoring editor state from ViewModel...");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Editor.Text = _vm.DiagramText;
                Editor.SelectionStart = _vm.EditorSelectionStart;
                Editor.SelectionLength = _vm.EditorSelectionLength;
                Editor.CaretOffset = _vm.EditorCaretOffset;
                SimpleLogger.Log("Editor state restored successfully");
            });

            // Step 3: Initialize WebView
            SimpleLogger.Log("Step 3: Initializing WebView...");
            string? assetsPath = Path.GetDirectoryName(_updateService.BundledMermaidPath);
            if (assetsPath is null)
            {
                const string error = "BundledMermaidPath does not contain a directory component";
                SimpleLogger.LogError(error);
                throw new InvalidOperationException(error);
            }

            SimpleLogger.Log($"Assets path resolved: {assetsPath}");
            await InitializeWebViewAsync(assetsPath);

            // Step 4: Update command states
            SimpleLogger.Log("Step 4: Updating command states...");
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

            // When the window is closing, save the current state
            SynchronizeViewModelWithEditor();
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

    // Only called from OnClosing so no marshaling needed, but we marshal to UI thread anyway for safety
    private void SynchronizeViewModelWithEditor()
    {
        Dispatcher.UIThread.Post(() =>
        {
            _vm.DiagramText = Editor.Text;
            _vm.EditorSelectionStart = Editor.SelectionStart;
            _vm.EditorSelectionLength = Editor.SelectionLength;
            _vm.EditorCaretOffset = Editor.CaretOffset;

            SimpleLogger.Log($"ViewModel synchronized with editor state: {Editor.Text.Length} chars, selection {Editor.SelectionStart}:{Editor.SelectionLength}, caret {Editor.CaretOffset}");
        });
    }

    private async Task InitializeWebViewAsync(string assets)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        SimpleLogger.Log("=== WebView Initialization Started ===");
        SimpleLogger.Log($"Assets directory: {assets}");

        // CRITICAL FIX: Temporarily disable live preview during WebView initialization
        bool originalLivePreview = _vm.LivePreviewEnabled;
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _vm.LivePreviewEnabled = false;
            SimpleLogger.Log($"Temporarily disabled live preview (was: {originalLivePreview})");
        });

        try
        {
            // Step 1: Initialize the MermaidRenderer
            await _renderer.InitializeAsync(Preview, assets);

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
            // CRITICAL: Re-enable live preview after WebView is ready
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
