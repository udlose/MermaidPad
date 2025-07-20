
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MermaidPad.Services;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MermaidPad.ViewModels.MainWindow;
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly MermaidService _mermaidService;
    private readonly Timer _debounceTimer;
    private Window? _mainWindow;
    private const int DebounceDelayMs = 500;

    [ObservableProperty]
    private string _mermaidSource = @"graph TD
    A[Start] --> B{Is it working?}
    B -->|Yes| C[Great! 🎉]
    B -->|No| D[Debug 🔍]
    C --> E[Deploy 🚀]
    D --> B
    
    style A fill:#e1f5fe
    style C fill:#e8f5e8
    style D fill:#fff3e0
    style E fill:#f3e5f5";

    [ObservableProperty]
    private string _previewUrl = "about:blank";

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private string _currentFilePath = "";

    public MainWindowViewModel()
    {
        _mermaidService = new MermaidService();
        _debounceTimer = new Timer(OnDebounceTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);

        // Initialize with sample diagram
        _ = GeneratePreviewAsync();
    }

    // Set the main window reference for file operations
    public void SetMainWindow(Window window)
    {
        _mainWindow = window;
    }

    partial void OnMermaidSourceChanged(string value)
    {
        // Reset debounce timer for live preview
        _debounceTimer.Change(DebounceDelayMs, Timeout.Infinite);
        HasError = false;
        StatusMessage = "Typing...";
    }

    private async void OnDebounceTimerElapsed(object? state)
    {
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(Callback);
        return;

        // Switch to UI thread for async operations
        async Task Callback()
        {
            await GeneratePreviewAsync();
        }
    }

    private async Task GeneratePreviewAsync()
    {
        try
        {
            IsProcessing = true;
            StatusMessage = "Rendering diagram...";

            string url = await _mermaidService.GeneratePreviewAsync(MermaidSource);
            PreviewUrl = url;

            HasError = false;
            StatusMessage = "Ready";
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    public async Task RenderAsync()
    {
        StatusMessage = "Rendering diagram...";
        await GeneratePreviewAsync();
    }

    [RelayCommand]
    public void Clear()
    {
        MermaidSource = string.Empty;
        StatusMessage = "Cleared";
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (_mainWindow is null) return;

        try
        {
            string suggestedName = string.IsNullOrEmpty(CurrentFilePath)
                ? "diagram.mmd"
                : Path.GetFileName((string?)CurrentFilePath);

            bool saved = await FileService.SaveFileAsync(_mainWindow, MermaidSource, suggestedName);

            if (saved)
            {
                StatusMessage = "File saved successfully";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save error: {ex.Message}";
            HasError = true;
        }
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (_mainWindow is null) return;

        try
        {
            string? content = await FileService.OpenFileAsync(_mainWindow);
            if (!string.IsNullOrEmpty(content))
            {
                MermaidSource = content;
                StatusMessage = "File loaded successfully";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Load error: {ex.Message}";
            HasError = true;
        }
    }

    [RelayCommand]
    public async Task ExportAsync()
    {
        if (_mainWindow is null) return;

        try
        {
            // For now, just copy the current preview URL
            // In a full implementation, you could extract SVG from the WebView
            StatusMessage = "Export functionality - copy preview URL to clipboard";

            // TODO: Implement actual SVG extraction from WebView
            // This would require JavaScript execution in the WebView
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export error: {ex.Message}";
            HasError = true;
        }
    }

    [RelayCommand]
    public void Close()
    {
        _mermaidService.Cleanup();
        Environment.Exit(0);
    }

    [RelayCommand]
    public void InsertSample(string sampleType)
    {
        string sample = sampleType switch
        {
            "flowchart" => @"graph TD
    A[Start] --> B{Decision}
    B -->|Yes| C[Action 1]
    B -->|No| D[Action 2]
    C --> E[End]
    D --> E",

            "sequence" => @"sequenceDiagram
    participant A as Alice
    participant B as Bob
    A->>B: Hello Bob, how are you?
    B-->>A: Great!
    A-)B: See you later!",

            "class" => @"classDiagram
    class Animal {
        +String name
        +int age
        +makeSound()
    }
    class Dog {
        +String breed
        +bark()
    }
    Animal <|-- Dog",

            "gantt" => @"gantt
    title Project Timeline
    dateFormat  YYYY-MM-DD
    section Planning
    Research           :a1, 2024-01-01, 30d
    Design            :a2, after a1, 20d
    section Development
    Implementation    :a3, after a2, 45d
    Testing          :a4, after a3, 15d",

            _ => MermaidSource
        };

        MermaidSource = sample;
        StatusMessage = $"Inserted {sampleType} sample";
    }
}
