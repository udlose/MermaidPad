# Avalonia UI Control Lifecycle Documentation

## Overview

This document describes the complete lifecycle of Avalonia UI controls, with specific details about how MermaidPad's MainWindow and dock panel system initializes.

## Core Lifecycle Events (In Order)

### 1. Construction Phase

**Event**: Constructor
**When**: Object instantiation via `new` or DI container
**Thread**: UI Thread (typically)
**Purpose**: Initialize fields, subscribe to services

**Characteristics**:
- Control does NOT exist in visual tree yet
- `IsInitialized` is `false`
- `IsLoaded` is `false`
- `DataContext` may be null
- Data bindings NOT processed
- Named controls (x:Name) NOT accessible yet
- Child controls NOT created from DataTemplates

**MermaidPad Specific**:
```csharp
public MainWindow(...)
{
    InitializeComponent();  // Loads XAML, creates named controls
    _syntaxHighlightingService.Initialize();
    // MainDock exists here but IsInitialized = false
    // ✗ DON'T wire events here - control not ready!
}
```

---

### 2. Initialization Phase

**Event**: `OnInitialized` (internal, not commonly overridden)
**When**: After constructor, before attached to visual tree
**Thread**: UI Thread
**Purpose**: Final initialization before visual tree attachment

**Characteristics**:
- Control structure created
- Still NOT in visual tree
- Still NOT visible

---

### 3. Visual Tree Attachment Phase

**Event**: `OnAttachedToVisualTree`
**When**: Control added to active visual tree
**Thread**: UI Thread
**Purpose**: Wire event handlers, start animations, access visual ancestors

**Characteristics**:
- `IsAttachedToVisualTree` = `true`
- Can access visual ancestors/descendants
- Can query rendering properties
- Perfect time to wire window-level events
- Data bindings still processing

**MermaidPad Specific**:
```csharp
protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
{
    base.OnAttachedToVisualTree(e);

    // ✓ Good place to wire window events
    ActualThemeVariantChanged += _themeChangedHandler;
    Activated += _activatedHandler;
    ViewModel.EditorViewModel.PropertyChanged += _viewModelPropertyChangedHandler;
}
```

---

### 4. Loaded Phase

**Event**: `OnLoaded`
**When**: Control loaded and ready for interaction
**Thread**: UI Thread
**Purpose**: Final setup, wire complex events, start async operations

**Characteristics**:
- `IsLoaded` = `true`
- `IsInitialized` = `true`
- Control fully constructed in visual tree
- Parent controls loaded
- **Named controls accessible**
- Data bindings mostly processed
- Control ready for user interaction

**MermaidPad Specific**:
```csharp
protected override void OnLoaded(RoutedEventArgs e)
{
    base.OnLoaded(e);

    // ✓ NOW MainDock.IsInitialized = true
    // ✓ Safe to wire LayoutUpdated event
    if (MainDock is not null)
    {
        _dockControlLayoutUpdatedHandler = OnDockControlLayoutUpdated;
        MainDock.LayoutUpdated += _dockControlLayoutUpdatedHandler;
    }
}
```

---

### 5. Opened Phase (Window-specific)

**Event**: `OnOpened`
**When**: Window is visible and interactive
**Thread**: UI Thread
**Purpose**: Post-load initialization, start background tasks

**Characteristics**:
- Window visible to user
- All controls loaded
- Safe to show dialogs
- Good place for async initialization

**MermaidPad Specific**:
```csharp
protected override void OnOpened(EventArgs e)
{
    base.OnOpened(e);

    OnOpenedCoreAsync()
        .SafeFireAndForget(onException: ex => _logger.LogError(ex, "..."));
}
```

---

### 6. Data Binding Processing (Async)

**When**: Throughout initialization, async with visual tree construction
**What Happens**:
- `DataContext` propagated down visual tree
- Binding expressions evaluated
- `DataTemplate` instances created based on bound data
- Controls created dynamically from templates

**MermaidPad Dock Specific**:
```csharp
// In XAML:
<dock:DockControl Layout="{Binding Layout}" />

// Binding processing sequence:
1. DockControl created
2. DataContext set to MainViewModel
3. Layout binding evaluated
4. Layout property set on DockControl
5. DockControl processes layout
6. DataTemplates applied (Editor/Preview/AI panels created)
7. LayoutUpdated event fires
```

---

### 7. Layout Update Phase

**Event**: `LayoutUpdated`
**When**: After layout measurements/arrangements complete
**Thread**: UI Thread
**Frequency**: Can fire MULTIPLE times during initialization and on any layout change

**Characteristics**:
- Layout calculations complete
- Controls positioned and sized
- DataTemplate instances may now exist in visual tree
- **This is when dynamically created controls appear**

**MermaidPad Specific**:
```csharp
private void OnDockControlLayoutUpdated(object? sender, EventArgs e)
{
    // Check if Layout binding processed
    if (dockControl.Layout is null) return;  // Not ready yet

    // Try to find panels created from DataTemplates
    EditorPanel? editorPanel = dockControl.GetVisualDescendants()
        .OfType<EditorPanel>().FirstOrDefault();

    if (editorPanel is null) return;  // Not created yet, wait for next LayoutUpdated

    // ✓ Found panels! Unsubscribe and initialize
    dockControl.LayoutUpdated -= OnDockControlLayoutUpdated;
    InitializePanels(editorPanel, previewPanel, aiPanel);
}
```

**CRITICAL**: Unsubscribe after successful initialization to prevent repeated execution!

---

### 8. Running Phase

**Events**: Property changes, user interactions, timer ticks, etc.
**When**: Normal application runtime
**Thread**: Various (handle marshaling to UI thread as needed)

---

### 9. Unloaded Phase

**Event**: `OnUnloaded`
**When**: Control removed from loaded tree
**Thread**: UI Thread
**Purpose**: Cleanup event handlers, stop timers/animations

**Characteristics**:
- `IsLoaded` = `false`
- Control still in visual tree (might reload)
- Good place to unsubscribe events
- Stop animations/timers

**MermaidPad Specific**:
```csharp
protected override void OnUnloaded(RoutedEventArgs e)
{
    // Unsubscribe ALL event handlers
    UnsubscribeAllEventHandlers();
    base.OnUnloaded(e);
}
```

---

### 10. Visual Tree Detachment Phase

**Event**: `OnDetachedFromVisualTree`
**When**: Control removed from visual tree
**Thread**: UI Thread
**Purpose**: Release visual resources, dispose native controls

**Characteristics**:
- `IsAttachedToVisualTree` = `false`
- Dispose of heavyweight resources
- Clear visual element references

**MermaidPad Specific**:
```csharp
protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
{
    // Dispose WebView and other visual resources
    _preview?.Dispose();
    base.OnDetachedFromVisualTree(e);
}
```

---

### 11. Disposal Phase

**Event**: `Dispose` / `DisposeAsync`
**When**: Object being garbage collected or explicitly disposed
**Thread**: Any
**Purpose**: Final cleanup, release unmanaged resources

---

## MermaidPad Dock Panel Initialization Flow

### The Complete Sequence

```
1. App.OnFrameworkInitializationCompleted()
   │
   ├─► MainViewModel constructor
   │   ├─► Creates EditorViewModel (via DI)
   │   ├─► Creates PreviewViewModel (via DI)
   │   ├─► Creates AIPanelViewModel (manual)
   │   ├─► InitializeContextLocator()
   │   │   └─► Maps panel IDs → ViewModels
   │   │       • "Editor" → EditorViewModel
   │   │       • "Preview" → PreviewViewModel
   │   │       • "AIAssistant" → AIPanelViewModel
   │   └─► LoadLayout()
   │       └─► Creates default IRootDock with 3 Tool panels
   │           (or loads from layout.json)
   │
   ├─► new MainWindow(...)
   │   └─► Constructor
   │       ├─► InitializeComponent()
   │       │   └─► MainDock created (x:Name="MainDock")
   │       │       IsInitialized = false ❌
   │       └─► _syntaxHighlightingService.Initialize()
   │
   └─► desktop.MainWindow = mainWindow

2. MainWindow.OnAttachedToVisualTree()
   ├─► Wire ActualThemeVariantChanged event
   ├─► Wire Activated event
   └─► Wire EditorViewModel.PropertyChanged event

3. MainWindow.OnLoaded()
   │   MainDock.IsInitialized = true ✓
   │
   ├─► Wire MainDock.LayoutUpdated event
   └─► _logger.LogInformation("LayoutUpdated event wired")

4. DockControl processes Layout binding (ASYNC)
   │
   ├─► DataContext = MainViewModel
   ├─► Layout binding evaluates
   ├─► DockControl.Layout = mainViewModel.Layout
   │
   └─► DockControl processes layout structure
       ├─► Finds Tool with Id="Editor"
       │   ├─► ContextLocator["Editor"] → EditorViewModel
       │   └─► DataTemplate(EditorViewModel) → EditorPanel
       │
       ├─► Finds Tool with Id="Preview"
       │   ├─► ContextLocator["Preview"] → PreviewViewModel
       │   └─► DataTemplate(PreviewViewModel) → PreviewPanel
       │
       └─► Finds Tool with Id="AIAssistant"
           ├─► ContextLocator["AIAssistant"] → AIPanelViewModel
           └─► DataTemplate(AIPanelViewModel) → AIPanel

5. DockControl.LayoutUpdated fires
   │
   └─► OnDockControlLayoutUpdated() called

6. OnDockControlLayoutUpdated() execution
   │
   ├─► Check: dockControl.Layout is not null?
   │   └─► Yes ✓ Continue
   │
   ├─► Search visual tree for panels:
   │   ├─► EditorPanel? Found ✓
   │   ├─► PreviewPanel? Found ✓
   │   └─► AIPanel? Found ✓
   │
   ├─► Unsubscribe LayoutUpdated (prevent re-entry)
   │
   ├─► Initialize EditorPanel:
   │   ├─► _editor = editorPanel.Editor
   │   ├─► Apply syntax highlighting
   │   ├─► Set editor text from ViewModel
   │   └─► Wire editor events (TextChanged, etc.)
   │
   ├─► Initialize PreviewPanel:
   │   ├─► _preview = previewPanel.Preview
   │   └─► InitializeWebViewAsync()
   │       ├─► _renderer.InitializeAsync(_preview)
   │       ├─► First render
   │       └─► Set IsWebViewReady = true
   │
   └─► AIPanel: No special initialization needed
       └─► Already data-bound to AIPanelViewModel

7. MainWindow.OnOpened()
   └─► OnOpenedCoreAsync()
       ├─► Check for updates (disabled)
       └─► Update command states

8. PANELS NOW VISIBLE AND FUNCTIONAL ✓
```

---

## Common Pitfalls & Solutions

### ❌ Pitfall #1: Wiring Events in Constructor

**Problem**:
```csharp
public MainWindow()
{
    InitializeComponent();

    if (MainDock?.IsInitialized == true)  // ❌ Always false!
    {
        MainDock.LayoutUpdated += OnLayoutUpdated;
    }
}
```

**Why It Fails**:
- `IsInitialized` is `false` until `OnLoaded`
- Event never wired
- Functionality never works

**Solution**:
```csharp
protected override void OnLoaded(RoutedEventArgs e)
{
    base.OnLoaded(e);
    MainDock.LayoutUpdated += OnLayoutUpdated;  // ✓ Now it works!
}
```

---

### ❌ Pitfall #2: Accessing Visual Children Too Early

**Problem**:
```csharp
public MainWindow()
{
    InitializeComponent();

    var panel = this.FindControl<EditorPanel>("Editor");  // ❌ null!
    // Panel created from DataTemplate doesn't exist yet
}
```

**Why It Fails**:
- DataTemplate controls don't exist at construction
- Created asynchronously during layout binding
- Must wait for LayoutUpdated

**Solution**:
```csharp
private void OnLayoutUpdated(object? sender, EventArgs e)
{
    var panel = this.GetVisualDescendants()
        .OfType<EditorPanel>()
        .FirstOrDefault();  // ✓ Now it exists!

    if (panel is not null)
    {
        // Unsubscribe to prevent repeated calls
        sender.LayoutUpdated -= OnLayoutUpdated;
        InitializePanel(panel);
    }
}
```

---

### ❌ Pitfall #3: Calling Code That Needs Window Before Window Exists

**Problem**:
```csharp
public MainViewModel()
{
    // ...
    InitializeDockState();  // ❌ Calls GetParentWindow() which returns null!
}

// In App.axaml.cs:
MainViewModel vm = services.GetRequiredService<MainViewModel>();  // VM created
MainWindow window = new MainWindow { DataContext = vm };
desktop.MainWindow = window;  // ❌ Window set AFTER VM constructor
```

**Why It Fails**:
- MainViewModel created before MainWindow
- `GetParentWindow()` returns `null`
- Code silently does nothing

**Solution**:
```csharp
// Don't call InitializeDockState() in constructor
// Let the dock state service manage it automatically
// OR call it from a later lifecycle event like OnLoaded
```

---

### ❌ Pitfall #4: Forgetting to Unsubscribe LayoutUpdated

**Problem**:
```csharp
private void OnLayoutUpdated(object? sender, EventArgs e)
{
    var panel = FindPanel();
    if (panel is not null)
    {
        InitializePanel(panel);
        // ❌ Forgot to unsubscribe!
    }
}
// Result: OnLayoutUpdated called hundreds of times!
```

**Solution**:
```csharp
private void OnLayoutUpdated(object? sender, EventArgs e)
{
    var panel = FindPanel();
    if (panel is not null)
    {
        // ✓ Unsubscribe FIRST
        ((DockControl)sender).LayoutUpdated -= OnLayoutUpdated;
        InitializePanel(panel);
    }
}
```

---

## Best Practices

### ✓ 1. Use Appropriate Lifecycle Events

| Task | Best Event |
|------|------------|
| Wire window-level events | `OnAttachedToVisualTree` |
| Access named controls | `OnLoaded` |
| Find DataTemplate controls | `LayoutUpdated` |
| Start async operations | `OnOpened` |
| Unsubscribe events | `OnUnloaded` |
| Dispose resources | `OnDetachedFromVisualTree` |

### ✓ 2. Always Unsubscribe Event Handlers

Store handlers in fields for proper cleanup:
```csharp
private EventHandler? _myHandler;

protected override void OnLoaded(...)
{
    _myHandler = OnMyEvent;
    Control.MyEvent += _myHandler;
}

protected override void OnUnloaded(...)
{
    if (_myHandler is not null)
    {
        Control.MyEvent -= _myHandler;
        _myHandler = null;
    }
}
```

### ✓ 3. Guard Against Null References

```csharp
private void OnLayoutUpdated(object? sender, EventArgs e)
{
    if (sender is not DockControl dockControl) return;
    if (dockControl.Layout is null) return;  // Binding not ready

    var panel = FindPanel();
    if (panel is null) return;  // Not created yet

    // Now safe to proceed
}
```

### ✓ 4. Use Async Properly

```csharp
protected override void OnOpened(EventArgs e)
{
    base.OnOpened(e);

    // ✓ Use SafeFireAndForget for async void scenarios
    InitializeAsync()
        .SafeFireAndForget(onException: ex => _logger.LogError(ex, "..."));
}
```

---

## Summary

**Key Takeaways**:
1. Constructor: Setup only, no events, no visual tree access
2. OnAttachedToVisualTree: Wire window events
3. OnLoaded: Access named controls, wire control events
4. LayoutUpdated: Find DataTemplate controls, UNSUBSCRIBE after success
5. OnUnloaded: Cleanup event handlers
6. OnDetachedFromVisualTree: Dispose visual resources

**MermaidPad Flow**:
1. ViewModels created → Layout created
2. Window created → Controls created
3. Window loaded → Events wired
4. Layout binding processed → Panels created
5. LayoutUpdated → Panels found & initialized
6. Panels visible ✓
