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

        Opened += async (_, _) =>
        {
            SimpleLogger.Log("Window opened event triggered");
            await OnOpenedAsync();
            BringFocusToEditor();
        };
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
            if (_suppressEditorTextChanged) return;

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

        _vm.PropertyChanged += (s, e) =>
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
        //// Make sure caret is visible:
        //Editor.TextArea.Caret.CaretBrush = new SolidColorBrush(Colors.Red);

        //// Ensure selection is visible
        //Editor.TextArea.SelectionBrush = new SolidColorBrush(Colors.SteelBlue);
        //Dispatcher.UIThread.Post(() => Editor.TextArea.Caret.BringCaretToView(), DispatcherPriority.Background);
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
            // Restore editor state from ViewModel (source of truth)
            Editor.Text = _vm.DiagramText;
            Editor.SelectionStart = _vm.EditorSelectionStart;
            Editor.SelectionLength = _vm.EditorSelectionLength;
            Editor.CaretOffset = _vm.EditorCaretOffset;
            SimpleLogger.Log("Editor state restored successfully");

            // Step 3: Initialize WebView
            SimpleLogger.Log("Step 3: Initializing WebView...");
            string? assetsPath = Path.GetDirectoryName(_updateService.BundledMermaidPath);
            if (assetsPath == null)
            {
                const string error = "BundledMermaidPath does not contain a directory component";
                SimpleLogger.LogError(error);
                throw new InvalidOperationException(error);
            }

            SimpleLogger.Log($"Assets path resolved: {assetsPath}");
            await InitializeWebViewAsync(assetsPath);

            // Step 4: Update command states
            SimpleLogger.Log("Step 4: Updating command states...");
            _vm.RenderCommand.NotifyCanExecuteChanged();
            _vm.ClearCommand.NotifyCanExecuteChanged();

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

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        SimpleLogger.Log("Window closing, saving current state...");

        try
        {
            // When the window is closing, save the current state
            SynchronizeViewModelWithEditor();
            _vm.Persist();
            SimpleLogger.Log("Window state saved successfully");
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError("Failed to save window state during close", ex);
        }
    }

    private void SynchronizeViewModelWithEditor()
    {
        _vm.DiagramText = Editor.Text;
        _vm.EditorSelectionStart = Editor.SelectionStart;
        _vm.EditorSelectionLength = Editor.SelectionLength;
        _vm.EditorCaretOffset = Editor.CaretOffset;

        SimpleLogger.Log($"ViewModel synchronized with editor state: {Editor.Text.Length} chars, selection {Editor.SelectionStart}:{Editor.SelectionLength}, caret {Editor.CaretOffset}");
    }

    private async Task InitializeWebViewAsync(string assets)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        SimpleLogger.Log("=== WebView Initialization Started ===");
        SimpleLogger.Log($"Assets directory: {assets}");

        try
        {
            // Step 1: Validate index.html exists
            string indexPath = Path.Combine(assets, "index.html");
            SimpleLogger.Log($"Checking for index.html at: {indexPath}");

            if (!File.Exists(indexPath))
            {
                SimpleLogger.Log("index.html not found, creating fallback content");
                await File.WriteAllTextAsync(indexPath, "<html><body>Missing index.html</body></html>");
                SimpleLogger.LogAsset("created fallback", "index.html", true);
            }
            else
            {
                FileInfo indexInfo = new FileInfo(indexPath);
                SimpleLogger.LogAsset("validated", "index.html", true, indexInfo.Length);
            }

            // Step 2: Validate mermaid.min.js exists
            string mermaidPath = Path.Combine(assets, "mermaid.min.js");
            if (File.Exists(mermaidPath))
            {
                FileInfo mermaidInfo = new FileInfo(mermaidPath);
                SimpleLogger.LogAsset("validated", "mermaid.min.js", true, mermaidInfo.Length);
            }
            else
            {
                SimpleLogger.LogError($"Critical asset missing: mermaid.min.js at {mermaidPath}");
            }

            // Step 3: Set WebView URL
            Uri indexUri = new Uri(indexPath);
            SimpleLogger.Log($"Setting WebView URL to: {indexUri}");
            Preview.Url = indexUri;

            // Step 4: Attach renderer
            SimpleLogger.Log("Attaching MermaidRenderer to WebView...");
            _renderer.Attach(Preview);

            // Step 5: Allow initial load time
            SimpleLogger.Log("Waiting for WebView initial load (500ms delay)...");
            await Task.Delay(500);

            // Step 6: Attempt initial render
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
            Debug.WriteLine($"WebView init failed: {ex}");
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        SimpleLogger.Log("Close button clicked");
        Close();
    }
}
