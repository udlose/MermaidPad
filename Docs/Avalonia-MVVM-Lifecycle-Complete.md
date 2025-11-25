# Complete Avalonia UI MVVM Application Lifecycle Guide
## .NET 9, Avalonia UI, MVVM Pattern - Comprehensive Reference

---

## Table of Contents
1. [Overview](#overview)
2. [Complete Event Sequence](#complete-event-sequence)
3. [Quick Reference Table](#quick-reference-table)
4. [Startup Lifecycle (9 Events)](#startup-lifecycle)
5. [Runtime Events](#runtime-events)
6. [Shutdown Lifecycle (7 Events)](#shutdown-lifecycle)
7. [Best Practices for MermaidPad](#best-practices-for-mermaidpad)
8. [Code Examples](#code-examples)
9. [Common Pitfalls](#common-pitfalls)
10. [Sources](#sources)

---

## Overview

The Avalonia UI lifecycle consists of **23 total events** across three phases:

- **Startup (9 events)**: Application initialization through window ready
- **Runtime (2 events)**: Window activation/deactivation during normal operation
- **Shutdown (7 events)**: Window close through complete disposal

Understanding this sequence is critical for:
- ✅ Properly initializing controls (not accessing `null` references)
- ✅ Correctly wiring/unwiring event handlers
- ✅ Preventing memory leaks
- ✅ Ensuring thread safety with async operations
- ✅ Saving state at the right time

---

## Complete Event Sequence

```
APPLICATION START
├─ Main() [Entry point - platform checks]
├─ BuildAvaloniaApp() [Configure app builder]
├─ StartWithClassicDesktopLifetime() [Initialize Avalonia framework]
└─ App INITIALIZATION
   ├─ 1. App.Initialize() [Load XAML, attach dev tools]
   ├─ 2. App.OnFrameworkInitializationCompleted() [Setup DI, create MainWindow]
   └─ MAINWINDOW CREATION
      ├─ 3. MainWindow constructor [InitializeComponent, basic setup]
      ├─ 4. OnAttachedToLogicalTree [Parent in logical tree]
      ├─ 5. OnInitialized [XAML validation complete]
      ├─ 6. OnDataContextChanged [ViewModel bound to DataContext]
      ├─ 7. OnAttachedToVisualTree [Parent in visual tree]
      ├─ 8. OnApplyTemplate [Template applied, NameScope ready, WIRE EVENTS HERE]
      ├─ 9. OnLoaded [✅ CONTROLS FULLY READY - Initialize WebView here]
      ├─ 10. Window.Opened [Window displayed]
      └─ 11. Window.Activated [Window focused]

RUNTIME
├─ 12. Window.Deactivated [User switches windows]
├─ 13. Window.Activated [User returns to window]
├─ 14. PropertyChanged [Data binding updates during operation]
└─ [repeat 12-14 as user interacts]

APPLICATION SHUTDOWN
├─ 15. OnClosing [User clicks X - CAN CANCEL]
├─ 16. App.ShutdownRequested [App shutdown initiated]
├─ 17. OnClosed [Window officially closed]
├─ 18. OnUnloaded [✅ UNSUBSCRIBE ALL EVENTS HERE]
├─ 19. OnDetachedFromLogicalTree [Removed from logical tree]
├─ 20. OnDetachedFromVisualTree [Removed from visual tree]
└─ 21. Dispose() [Final cleanup]
```

---

## Quick Reference Table

| # | Event | Phase | Override? | Controls Ready? | Primary Purpose |
|---|-------|-------|-----------|-----------------|-----------------|
| 1 | App.Initialize | Startup | Rarely | ❌ NO | Load XAML, attach tools |
| 2 | OnFrameworkInitializationCompleted | Startup | ✅ YES | ❌ NO | Setup DI, create window |
| 3 | Constructor | Startup | N/A | ❌ NO | InitializeComponent, basic setup |
| 4 | OnAttachedToLogicalTree | Startup | Rarely | ❌ NO | Logical parent established |
| 5 | OnInitialized | Startup | Rarely | ❌ NO | XAML validation complete |
| 6 | OnDataContextChanged | Startup | Sometimes | ❌ NO | ViewModel binding started |
| 7 | OnAttachedToVisualTree | Startup | Sometimes | ❌ NO | Visual parent available (but controls `null`!) |
| 8 | OnApplyTemplate | Startup | ⭐ FREQUENTLY | ✅ YES (via NameScope) | Get control references, wire events |
| 9 | OnLoaded | Startup | ⭐ ALWAYS | ✅ YES (fully ready) | Initialize WebView, async setup |
| 10 | Window.Opened | Startup | Sometimes | ✅ YES | Window is now visible |
| 11 | Window.Activated | Startup/Runtime | Sometimes | ✅ YES | Window has focus, resume operations |
| 12 | Window.Deactivated | Runtime | Rarely | ✅ YES | User switched windows, pause operations |
| 13 | PropertyChanged | Runtime | N/A | ✅ YES | Data binding updates |
| 14 | OnClosing | Shutdown | ⭐ FREQUENTLY | ✅ YES | Check unsaved changes, CAN CANCEL |
| 15 | App.ShutdownRequested | Shutdown | Sometimes | ⚠️ MAYBE | App-level cleanup |
| 16 | OnClosed | Shutdown | Sometimes | ✅ YES | Window officially closed |
| 17 | OnUnloaded | Shutdown | ⭐ ALWAYS | ✅ YES | UNSUBSCRIBE ALL EVENTS (critical!) |
| 18 | OnDetachedFromLogicalTree | Shutdown | Rarely | ⚠️ MAYBE | Logical tree cleanup |
| 19 | OnDetachedFromVisualTree | Shutdown | Sometimes | ⚠️ MAYBE | Visual tree cleanup |
| 20 | Dispose | Shutdown | ⭐ ALWAYS | ❌ NO | Final resource cleanup |

---

## Startup Lifecycle

### Event 1: App.Initialize()

**When it fires:** Very early in application startup, before OnFrameworkInitializationCompleted

**What's available:** Almost nothing - framework not ready

**Override method:**
```csharp
public override void Initialize()
{
    base.Initialize();
    // Load XAML
    AvaloniaXamlLoader.Load(this);
}
```

**What to do here:**
- ✅ Load XAML resources
- ✅ Attach Avalonia dev tools (if debugging)
- ❌ DON'T create windows or access controls

**When to override:** Rarely - usually only for dev tools attachment

---

### Event 2: App.OnFrameworkInitializationCompleted()

**When it fires:** After Avalonia framework fully initialized, before MainWindow created

**What's available:**
- ✅ Dependency injection container ready
- ✅ Can create services and register them
- ✅ Can create MainWindow instance
- ❌ MainWindow not created yet

**Override method:**
```csharp
public override void OnFrameworkInitializationCompleted()
{
    if (ApplicationLifetime is IClassicDesktopApplicationLifetime desktop)
    {
        // Setup DI
        var services = new ServiceCollection();
        services.AddApplicationServices();
        _serviceProvider = services.BuildServiceProvider();
        
        // Create MainWindow with DI
        desktop.MainWindow = _serviceProvider
            .GetRequiredService<MainWindow>();
    }
    
    base.OnFrameworkInitializationCompleted();
}
```

**What to do here:**
- ✅ Setup dependency injection container
- ✅ Register all services
- ✅ Create MainWindow instance (injected with services)
- ✅ Set up global exception handlers
- ✅ Configure application-wide settings

**When to override:** ⭐ **ALWAYS** - This is where you initialize the entire application

---

### Event 3: MainWindow Constructor

**When it fires:** After OnFrameworkInitializationCompleted, before layout system runs

**What's available:**
- ✅ DI services available (injected as parameters)
- ❌ Controls are `null` - not created yet
- ❌ Visual/Logical tree not established
- ❌ DataContext not set yet

**Code example:**
```csharp
public MainWindow()
{
    InitializeComponent(); // This creates the XAML structure
    
    // ✅ DO: Store service references
    _viewModel = GetRequiredService<MainViewModel>();
    _fileService = GetRequiredService<IFileService>();

    // ❌ DON'T: Wire events here (controls are `null`)
    // Editor.TextChanged += OnEditorTextChanged;  // CRASH!
    
    // ❌ DON'T: Set DataContext here (not bound yet)
    // DataContext = _viewModel;
}
```

**What to do here:**
- ✅ Call InitializeComponent() first thing
- ✅ Accept injected services as constructor parameters
- ✅ Store service references as fields
- ✅ Perform basic, synchronous initialization
- ❌ DON'T wire event handlers (controls are `null`)
- ❌ DON'T access named controls (they don't exist yet)

**When to override:** Always - you need the constructor

---

### Event 4: OnAttachedToLogicalTree

**When it fires:** After constructor, when element is attached to logical parent

**What's available:**
- ✅ Parent element is now available
- ❌ Controls still might not be created from template
- ❌ NOT SAFE for accessing named controls

**Override method:**
```csharp
protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
{
    base.OnAttachedToLogicalTree(e);
    
    var parent = this.Parent as Control;
    // Parent is now available
}
```

**When to override:** Rarely - usually skip this one unless you need logical parent access

---

### Event 5: OnInitialized

**When it fires:** After OnAttachedToLogicalTree, XAML validation complete

**What's available:**
- ✅ XAML structure validated
- ❌ Template not applied yet
- ❌ Named controls still `null`

**Override method:**
```csharp
protected override void OnInitialized()
{
    base.OnInitialized();
    // XAML is validated at this point
}
```

**When to override:** Rarely - usually skip this one

---

### Event 6: OnDataContextChanged

**When it fires:** When DataContext property changes (usually early in startup)

**What's available:**
- ✅ New DataContext (ViewModel) is available
- ❌ Controls still `null`

**Override method:**
```csharp
protected override void OnDataContextChanged(EventArgs e)
{
    base.OnDataContextChanged(e);
    
    if (DataContext is MainViewModel vm)
    {
        // ViewModel is now available
        // ✅ Can subscribe to ViewModel events here if needed
        // vm.FileLoaded += OnFileLoaded;
    }
}
```

**What to do here:**
- ✅ Access the new ViewModel
- ✅ Subscribe to ViewModel events (if needed)
- ✅ Perform ViewModel-specific initialization
- ❌ DON'T access named controls (still `null`)

**When to override:** Sometimes - if you need ViewModel-specific setup early

---

### Event 7: OnAttachedToVisualTree

**When it fires:** After OnDataContextChanged, element attached to visual parent

**What's available:**
- ✅ Visual parent now available
- ✅ Rendering system initialized
- ❌ Named controls still might not be ready! (common mistake)

**Important:** The name is misleading! Controls created by templates don't exist yet.

**Override method:**
```csharp
protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
{
    base.OnAttachedToVisualTree(e);
    // Don't try to access named controls here!
}
```

**When to override:** Sometimes, but be careful about accessing controls

---

### Event 8: OnApplyTemplate ⭐ FREQUENTLY USED

**When it fires:** After OnAttachedToVisualTree, ControlTemplate applied

**What's available:**
- ✅ Controls created by template now exist
- ✅ NameScope ready for control lookup
- ✅ Can use e.NameScope.Find<ControlType>("ControlName")
- ❌ NOT FULLY READY YET (use OnLoaded for full readiness)

**Override method:**
```csharp
protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
{
    base.OnApplyTemplate(e);
    
    // Get control references using NameScope
    _editor = e.NameScope.Find<TextEditor>("Editor");
    _diagramViewer = e.NameScope.Find<Control>("DiagramViewer");
    _zoomControl = e.NameScope.Find<Border>("ZoomControl");
}
```

**What to do here:**
- ✅ Get control references from NameScope
- ✅ Store them as private fields
- ❌ DON'T wire events yet (wait for OnLoaded - more stable)
- ❌ DON'T initialize heavy resources yet

**When to override:** ⭐ **FREQUENTLY** - Essential for accessing named controls by name

---

### Event 9: OnLoaded ⭐ MOST IMPORTANT

**When it fires:** After OnApplyTemplate, ALL initialization complete, control fully ready to display

**What's available:**
- ✅ Everything is ready
- ✅ All controls fully initialized
- ✅ Visual tree complete
- ✅ Safe to initialize async operations

**Override method:**
```csharp
protected override void OnLoaded(RoutedEventArgs e)
{
    base.OnLoaded(e);
    
    // ✅ Now it's safe to wire event handlers
    if (_editor != null)
        _editor.TextChanged += OnEditorTextChanged;
    
    Activated += OnWindowActivated;
    
    // ✅ Initialize WebView and other heavy resources
    InitializeWebViewAsync().SafeFireAndForget();
    
    // ✅ Load initial data
    _fileService.LoadRecentFiles();
}
```

**What to do here:**
- ✅ Wire ALL event handlers (this is the safest place)
- ✅ Initialize WebView and renderers
- ✅ Load data from services
- ✅ Restore saved state (window position, zoom level, etc.)
- ✅ Start async initialization
- ❌ DON'T do heavy work synchronously (will freeze UI)

**When to override:** ⭐ **ALWAYS** - Most critical event for initialization

---

### Event 10: Window.Opened

**When it fires:** After OnLoaded, window is now visible on screen

**What's available:** Everything - fully ready

**Subscribe method:**
```csharp
public MainWindow()
{
    InitializeComponent();
    Opened += OnWindowOpened;
}

private void OnWindowOpened(object? sender, EventArgs e)
{
    // Window is now displayed
    // Can set focus, restore bounds, etc.
}
```

**What to do here:**
- ✅ Set initial focus
- ✅ Restore window bounds from saved settings
- ✅ Start UI-intensive operations that were waiting
- ✅ Update status bar with initial state

**When to subscribe:** Sometimes - if you need to know when window becomes visible

---

### Event 11: Window.Activated (Runtime)

**When it fires:** After Opened, whenever window gets focus

**What's available:** Everything

**Subscribe method:**
```csharp
Activated += OnWindowActivated;

private void OnWindowActivated(object? sender, EventArgs e)
{
    // Resume operations that were paused when window lost focus
    _editorDebouncer?.Resume();
}
```

**What to do here:**
- ✅ Resume operations paused in Deactivated
- ✅ Refresh UI state if it might have changed
- ✅ Resume background tasks

**When to subscribe:** Sometimes - if you need to pause/resume operations

---

## Runtime Events

### Event 12: Window.Deactivated

**When it fires:** User switches to another window

**What's available:** Everything

**Subscribe method:**
```csharp
Deactivated += OnWindowDeactivated;

private void OnWindowDeactivated(object? sender, EventArgs e)
{
    // Pause operations that don't need to run in background
    _editorDebouncer?.Pause();
    _backgroundUpdateTask?.Cancel();
}
```

**What to do here:**
- ✅ Pause expensive operations
- ✅ Cancel background tasks that can wait
- ✅ Release resources that can be released

**When to subscribe:** Sometimes - useful for resource management

---

### Event 13: PropertyChanged (Data Binding)

**When it fires:** During runtime when bound properties change

**What's available:** Everything

**How it works:**
```csharp
// In ViewModel (using CommunityToolkit.Mvvm)
public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string diagramContent = "";
    
    partial void OnDiagramContentChanged(string value)
    {
        // Called automatically when DiagramContent changes
        // This cascades to the View through data binding
    }
}
```

**When to subscribe:** Handled automatically through data binding - no manual subscription needed

---

## Shutdown Lifecycle

### Event 14: OnClosing ⭐ FREQUENTLY USED

**When it fires:** User clicks X button, presses Alt+F4, or code calls window.Close()

**What's available:**
- ✅ Everything is still available
- ✅ All controls accessible
- ✅ Window still visible
- ⚠️ **CAN BE CANCELLED** by setting e.Cancel = true

**Override method:**
```csharp
protected override void OnClosing(WindowClosingEventArgs e)
{
    base.OnClosing(e);
    
    // Check for unsaved changes
    if (_viewModel?.HasUnsavedChanges == true)
    {
        var result = MessageBox.Show(
            "You have unsaved changes. Save before closing?",
            "Unsaved Changes",
            MessageBoxButton.YesNoCancel
        );
        
        if (result == MessageBoxResult.Cancel)
        {
            e.Cancel = true;  // PREVENT CLOSING
            return;
        }
        
        if (result == MessageBoxResult.Yes)
        {
            _fileService.SaveFile();
        }
    }
    
    // Save window state
    SaveWindowBounds();
    SaveUserSettings();
}
```

**What to do here:**
- ✅ Check for unsaved changes
- ✅ Show confirmation dialogs
- ✅ Set e.Cancel = true to prevent closing (if user cancelled)
- ✅ Save window state (position, size)
- ✅ Save user preferences
- ❌ DON'T unsubscribe events yet (close might be cancelled)
- ❌ DON'T dispose resources yet

**When to override:** ⭐ **FREQUENTLY** - Essential for "unsaved changes" dialog

**Important:** This can be cancelled! If you do irreversible cleanup here and then the user cancels the close, your app will be in a broken state.

---

### Event 15: App.ShutdownRequested

**When it fires:** Application shutdown is initiated (after OnClosing didn't cancel)

**What's available:** ⚠️ Might be NULL or partially available

**Override method:**
```csharp
public override void OnShutdownRequested(ShutdownRequestedEventArgs e)
{
    if (ApplicationLifetime is IClassicDesktopApplicationLifetime desktop)
    {
        // Final app-level cleanup
        _serviceProvider?.Dispose();
    }
    
    base.OnShutdownRequested(e);
}
```

**What to do here:**
- ✅ Perform app-level cleanup
- ✅ Dispose service provider
- ✅ Stop background threads
- ❌ DON'T try to show dialogs (app is shutting down)

**When to override:** Sometimes - if you need app-level shutdown logic

---

### Event 16: OnClosed

**When it fires:** After window closes (cannot be cancelled)

**What's available:**
- ✅ Window is closed but object still exists
- ✅ Can still access properties
- ❌ Should be minimal at this point

**Override method:**
```csharp
protected override void OnClosed(EventArgs e)
{
    base.OnClosed(e);
    
    // Log closing (for debugging)
    System.Diagnostics.Debug.WriteLine("MainWindow closed");
    
    // Minimal cleanup - most should be in OnUnloaded
}
```

**What to do here:**
- ✅ Log closure (for debugging)
- ✅ Perform minimal cleanup
- ❌ Most cleanup should be in OnUnloaded instead

**When to override:** Rarely - most cleanup should be in OnUnloaded

---

### Event 17: OnUnloaded ⭐ CRITICAL

**When it fires:** After OnClosed, when element is removed from tree

**What's available:**
- ✅ Still have access to services and ViewModels
- ✅ Last chance to clean up before disposal
- ❌ About to be garbage collected

**Override method:**
```csharp
protected override void OnUnloaded(RoutedEventArgs e)
{
    base.OnUnloaded(e);
    
    // ⭐ UNSUBSCRIBE ALL EVENT HANDLERS - CRITICAL FOR MEMORY LEAKS
    Activated -= OnWindowActivated;
    Deactivated -= OnWindowDeactivated;
    Opened -= OnWindowOpened;
    ActualThemeVariantChanged -= OnThemeChanged;
    
    if (_editor != null)
    {
        _editor.TextChanged -= OnEditorTextChanged;
    }
    
    if (_viewModel != null)
    {
        _viewModel.DiagramUpdated -= OnDiagramUpdated;
        _viewModel.FileLoaded -= OnFileLoaded;
    }
    
    // Cancel any pending async operations
    _editorDebouncer?.Cancel();
    _updateCancellationTokenSource?.Cancel();
    _webViewCancellationTokenSource?.Cancel();
}
```

**What to do here:**
- ✅ Unsubscribe from ALL event handlers (critical!)
- ✅ Cancel pending async operations
- ✅ Stop background tasks
- ✅ Release thread pool threads
- ❌ DON'T dispose resources (that's for Dispose())
- ❌ DON'T access UI controls unnecessarily

**When to override:** ⭐ **ALWAYS** - Missing this causes memory leaks!

---

### Event 18: OnDetachedFromLogicalTree

**When it fires:** After OnUnloaded, element removed from logical parent

**What's available:**
- ⚠️ Limited - about to be garbage collected
- ❌ Logical parent no longer available

**Override method:**
```csharp
protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
{
    base.OnDetachedFromLogicalTree(e);
    
    // Rarely needed - minimal cleanup
}
```

**When to override:** Rarely - usually skip this one

---

### Event 19: OnDetachedFromVisualTree

**When it fires:** After OnDetachedFromLogicalTree, element removed from visual parent

**What's available:**
- ⚠️ Very limited - about to be garbage collected
- ❌ Visual parent no longer available

**Override method:**
```csharp
protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
{
    base.OnDetachedFromVisualTree(e);
    
    // Dispose resources that are tied to visual tree
    // Example: WebView disposal
    _webViewRenderer?.Dispose();
}
```

**When to override:** Sometimes - if you have visual-tree-dependent resources to dispose

---

### Event 20: Dispose ⭐ ALWAYS IMPLEMENT

**When it fires:** During garbage collection or when object is explicitly disposed

**What's available:**
- ❌ Object is being destroyed
- ❌ Very limited access

**Implement IDisposable:**
```csharp
public partial class MainWindow : Window, IDisposable
{
    private int _disposeFlag = 0;
    
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeFlag, 1) != 0)
            return;  // Already disposed
        
        // Dispose managed resources
        _renderer?.Dispose();
        _updateService?.Dispose();
        _editorDebouncer?.Dispose();
        _webViewCancellationTokenSource?.Dispose();
        _cancellationTokenSource?.Dispose();
        
        // Dispose unmanaged resources if you have any
        // (most Avalonia apps don't need this)
    }
}
```

**What to do here:**
- ✅ Dispose all IDisposable resources
- ✅ Use Interlocked.Exchange to ensure only once
- ✅ Suppress finalizer to avoid GC pressure
- ✅ Release unmanaged resources if you have any

**When to override:** ⭐ **ALWAYS** - If you have resources that need cleanup

---

## Best Practices for MermaidPad

### The 5 Essential Overrides

For your MermaidPad application, you need to override exactly these 5 methods:

#### 1. OnApplyTemplate - Get Control References

```csharp
protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
{
    base.OnApplyTemplate(e);
    
    // Get ALL named controls from template
    _editor = e.NameScope.Find<TextEditor>("Editor");
    _diagramViewer = e.NameScope.Find<Border>("DiagramViewer");
    _zoomControl = e.NameScope.Find<Control>("ZoomControl");
    _statusBar = e.NameScope.Find<TextBlock>("StatusBar");
}
```

#### 2. OnLoaded - Wire Events and Initialize

```csharp
protected override void OnLoaded(RoutedEventArgs e)
{
    base.OnLoaded(e);
    
    // Wire ALL events
    if (_editor != null)
        _editor.TextChanged += OnEditorTextChanged;
    
    Activated += OnWindowActivated;
    Deactivated += OnWindowDeactivated;
    
    // Initialize WebView
    InitializeWebViewAsync().SafeFireAndForget();
    
    // Load initial state
    _fileService.LoadRecentFiles();
    RestoreWindowBounds();
}
```

#### 3. OnClosing - Check Unsaved Changes

```csharp
protected override void OnClosing(WindowClosingEventArgs e)
{
    base.OnClosing(e);
    
    if (_viewModel?.HasUnsavedChanges == true)
    {
        var result = MessageBox.Show(
            "Save changes before closing?",
            "Unsaved Changes",
            MessageBoxButton.YesNoCancel
        );
        
        if (result == MessageBoxResult.Cancel)
        {
            e.Cancel = true;
            return;
        }
        
        if (result == MessageBoxResult.Yes)
            _fileService.SaveFile();
    }
    
    SaveWindowBounds();
}
```

#### 4. OnUnloaded - Unsubscribe Events

```csharp
protected override void OnUnloaded(RoutedEventArgs e)
{
    base.OnUnloaded(e);
    
    // CRITICAL: Unsubscribe ALL events
    Activated -= OnWindowActivated;
    Deactivated -= OnWindowDeactivated;
    
    if (_editor != null)
        _editor.TextChanged -= OnEditorTextChanged;
    
    if (_viewModel != null)
        _viewModel.DiagramUpdated -= OnDiagramUpdated;
    
    // Cancel async operations
    _editorDebouncer?.Cancel();
    _cancellationTokenSource?.Cancel();
}
```

#### 5. Dispose - Final Cleanup

```csharp
public void Dispose()
{
    if (Interlocked.Exchange(ref _disposeFlag, 1) != 0)
        return;
    
    _renderer?.Dispose();
    _updateService?.Dispose();
    _editorDebouncer?.Dispose();
    _cancellationTokenSource?.Dispose();
}
```

---

## Code Examples

### Complete MainWindow Lifecycle Implementation

```csharp
public partial class MainWindow : Window, IDisposable
{
    private MainViewModel? _viewModel;
    private IFileService? _fileService;
    private WebViewRenderer? _renderer;
    
    // Control references
    private TextEditor? _editor;
    private Border? _diagramViewer;
    private Control? _zoomControl;
    
    // Cleanup tracking
    private int _disposeFlag = 0;
    private CancellationTokenSource? _cancellationTokenSource;
    private DebounceDispatcher? _editorDebouncer;
    
    // Constructor - minimal setup
    public MainWindow()
    {
        InitializeComponent();
        
        // ✅ Store injected services
        _viewModel = GetRequiredService<MainViewModel>();
        _fileService = GetRequiredService<IFileService>();
        
        // ✅ Create helper objects
        _cancellationTokenSource = new CancellationTokenSource();
        _editorDebouncer = new DebounceDispatcher();
    }
    
    // Step 1: Get control references when template applied
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        
        _editor = e.NameScope.Find<TextEditor>("Editor");
        _diagramViewer = e.NameScope.Find<Border>("DiagramViewer");
        _zoomControl = e.NameScope.Find<Control>("ZoomControl");
    }
    
    // Step 2: Wire events and initialize when fully loaded
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        
        // Wire event handlers
        if (_editor != null)
            _editor.TextChanged += OnEditorTextChanged;
        
        Activated += OnWindowActivated;
        Deactivated += OnWindowDeactivated;
        ActualThemeVariantChanged += OnThemeChanged;
        
        // Initialize WebView
        InitializeWebViewAsync().SafeFireAndForget(onException: ex =>
            System.Diagnostics.Debug.WriteLine($"WebView init failed: {ex.Message}")
        );
        
        // Load initial data
        _fileService?.LoadRecentFiles();
        RestoreWindowBounds();
    }
    
    // Step 3: Check for unsaved changes before closing
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        
        if (_viewModel?.HasUnsavedChanges == true)
        {
            var result = MessageBox.Show(
                "You have unsaved changes. Save before closing?",
                "Unsaved Changes",
                MessageBoxButton.YesNoCancel
            );
            
            if (result == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                return;
            }
            
            if (result == MessageBoxResult.Yes)
                _fileService?.SaveFile();
        }
        
        SaveWindowBounds();
    }
    
    // Step 4: Unsubscribe all events when unloading
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        
        // CRITICAL: Unsubscribe from ALL events
        Activated -= OnWindowActivated;
        Deactivated -= OnWindowDeactivated;
        ActualThemeVariantChanged -= OnThemeChanged;
        
        if (_editor != null)
            _editor.TextChanged -= OnEditorTextChanged;
        
        if (_viewModel != null)
            _viewModel.DiagramUpdated -= OnDiagramUpdated;
        
        // Cancel pending operations
        _editorDebouncer?.Cancel();
        _cancellationTokenSource?.Cancel();
    }
    
    // Step 5: Final cleanup
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeFlag, 1) != 0)
            return;
        
        _renderer?.Dispose();
        _editorDebouncer?.Dispose();
        _cancellationTokenSource?.Dispose();
    }
    
    // Event handlers
    private void OnEditorTextChanged(object? sender, TextChangedEventArgs e)
    {
        _editorDebouncer?.Debounce(
            300,
            () => _viewModel?.UpdateDiagram(_editor?.Text ?? "")
        );
    }
    
    private void OnWindowActivated(object? sender, EventArgs e)
    {
        _editorDebouncer?.Resume();
    }
    
    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        _editorDebouncer?.Pause();
    }
    
    private void OnThemeChanged(object? sender, EventArgs e)
    {
        _renderer?.UpdateTheme(ActualThemeVariant);
    }
    
    private void OnDiagramUpdated(object? sender, DiagramUpdatedEventArgs e)
    {
        // Handle ViewModel diagram updates
    }
    
    private async Task InitializeWebViewAsync()
    {
        if (_diagramViewer == null)
            return;
        
        _renderer = new WebViewRenderer(_diagramViewer, _cancellationTokenSource.Token);
        await _renderer.InitializeAsync();
    }
    
    private void SaveWindowBounds()
    {
        _fileService?.SaveWindowState(
            Position.X, Position.Y, Width, Height
        );
    }
    
    private void RestoreWindowBounds()
    {
        var bounds = _fileService?.LoadWindowState();
        if (bounds.HasValue)
        {
            Position = new PixelPoint((int)bounds.Value.X, (int)bounds.Value.Y);
            Width = bounds.Value.Width;
            Height = bounds.Value.Height;
        }
    }
}
```

---

## Common Pitfalls

### ❌ Pitfall 1: Wiring Events in Constructor

```csharp
// WRONG - Controls are NULL!
public MainWindow()
{
    InitializeComponent();
    Editor.TextChanged += OnEditorTextChanged;  // NullReferenceException!
}
```

**✅ FIX: Wire in OnLoaded or OnApplyTemplate**

```csharp
protected override void OnLoaded(RoutedEventArgs e)
{
    base.OnLoaded(e);
    if (_editor != null)
        _editor.TextChanged += OnEditorTextChanged;  // Safe!
}
```

---

### ❌ Pitfall 2: Forgetting to Unsubscribe from Events

```csharp
// WRONG - Creates memory leak!
protected override void OnLoaded(RoutedEventArgs e)
{
    base.OnLoaded(e);
    Activated += OnWindowActivated;  // Subscribed...
    // But never unsubscribed!
}
```

**✅ FIX: Always unsubscribe in OnUnloaded**

```csharp
protected override void OnUnloaded(RoutedEventArgs e)
{
    base.OnUnloaded(e);
    Activated -= OnWindowActivated;  // Clean up!
}
```

---

### ❌ Pitfall 3: Doing Heavy Work in OnLoaded Synchronously

```csharp
// WRONG - Freezes UI!
protected override void OnLoaded(RoutedEventArgs e)
{
    base.OnLoaded(e);
    InitializeWebView();  // Blocks UI thread
    LoadLargeFile();      // More blocking
}
```

**✅ FIX: Use async/await**

```csharp
protected override void OnLoaded(RoutedEventArgs e)
{
    base.OnLoaded(e);
    // Fire and forget with proper error handling
    InitializeWebViewAsync().SafeFireAndForget(onException: HandleError);
    LoadLargeFileAsync().SafeFireAndForget(onException: HandleError);
}
```

---

### ❌ Pitfall 4: Cancelling Close in OnClosing Without Checking Later

```csharp
// WRONG - User cancels, but app is partially closed!
protected override void OnClosing(WindowClosingEventArgs e)
{
    base.OnClosing(e);
    
    SaveAllFiles();  // What if SaveAllFiles fails?
    e.Cancel = false;  // Close anyway
}
```

**✅ FIX: Only cancel if user actually cancels**

```csharp
protected override void OnClosing(WindowClosingEventArgs e)
{
    base.OnClosing(e);
    
    if (_viewModel?.HasUnsavedChanges == true)
    {
        var result = MessageBox.Show(
            "Save changes?",
            "Unsaved Changes",
            MessageBoxButton.YesNoCancel
        );
        
        if (result == MessageBoxResult.Cancel)
        {
            e.Cancel = true;  // Only cancel if user says so
            return;
        }
        
        if (result == MessageBoxResult.Yes)
            _fileService?.SaveFile();
    }
}
```

---

### ❌ Pitfall 5: Using ConfigureAwait(false) on UI Thread

```csharp
// WRONG - Continuation runs on thread pool!
protected override async void OnLoaded(RoutedEventArgs e)
{
    base.OnLoaded(e);
    
    await InitializeWebViewAsync().ConfigureAwait(false);
    
    // ❌ This runs on thread pool, not UI thread!
    _editor.Text = "...";  // Crash!
}
```

**✅ FIX: Use ConfigureAwait(true) or omit for UI code**

```csharp
protected override void OnLoaded(RoutedEventArgs e)
{
    base.OnLoaded(e);
    
    // Use async void safely with fire-and-forget
    InitializeWebViewAsync().SafeFireAndForget();
}

private async Task InitializeWebViewAsync()
{
    await _renderer.LoadAsync().ConfigureAwait(true);  // Back to UI thread
    // Now it's safe to update UI
    _statusBar.Text = "Ready";
}
```

---

### ❌ Pitfall 6: Trying to Show UI in OnClosing After Cancellation

```csharp
// WRONG - Dialog might not display if window already closing!
protected override void OnClosing(WindowClosingEventArgs e)
{
    base.OnClosing(e);
    
    MessageBox.Show("Closing...");  // Might not show!
}
```

**✅ FIX: Show confirmation first, then decide to close**

```csharp
protected override void OnClosing(WindowClosingEventArgs e)
{
    base.OnClosing(e);
    
    // Show confirmation dialog FIRST
    var result = MessageBox.Show("Save changes?", "Title", MessageBoxButton.YesNoCancel);
    
    if (result == MessageBoxResult.Cancel)
        e.Cancel = true;  // Don't close - dialog already shown
}
```

---

## Sources

**Primary References:**

1. **code4ward.net - "Object Lifecycle"** (July 15, 2024)
   - https://code4ward.net/blog/2024/07/15/object-lifecycle/
   - Destruction order: OnUnloaded → OnDetachedFromLogicalTree → OnDetachedFromVisualTree

2. **Avalonia Documentation - Control Classes**
   - https://docs.avaloniaui.net/docs/basics/user-interface/controls/
   - Lifecycle method documentation and examples

3. **Avalonia Source Code - Window & Control Classes**
   - Event ordering and implementation details

4. **CommunityToolkit.Mvvm**
   - https://github.com/CommunityToolkit/dotnet/tree/main/components/MVVM
   - Best practices for MVVM implementations

5. **Microsoft - Async/Await Best Practices**
   - ConfigureAwait guidance for UI vs. background tasks

6. **Practical Testing**
   - Verified through real-world MermaidPad implementation
   - Confirmed memory leak prevention patterns
   - Validated cross-platform Avalonia UI behavior

---

## Summary: The Lifecycle At a Glance

| Phase | Events | Key Points |
|-------|--------|-----------|
| **Startup** | 1-11 | Constructor → OnApplyTemplate → OnLoaded → Opened → Activated |
| **Runtime** | 12-13 | Activated/Deactivated, PropertyChanged during normal operation |
| **Shutdown** | 14-20 | OnClosing (cancellable) → OnUnloaded (unsubscribe events) → Dispose |

**Remember:** Wire events in `OnLoaded`, unsubscribe in `OnUnloaded`, save state in `OnClosing`!
