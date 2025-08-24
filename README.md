# MermaidPad

A minimal cross-platform-ready Mermaid diagram editor built with .NET 9 + Avalonia. The editor uses [MermaidJS](https://mermaid.js.org/) to render Mermaid Diagrams.
It is cross-platform and runs on:
- Windows x64/arm64
- Linux x64/arm64
- macOS x64/arm64

It has an optional "Live Preview" feature to render in real-time. I plan to add syntax highlighting and SVG/PNG exporting. If there are features you'd like to see,
open a feature request.

I hope you enjoy it and feel welcome to contribute!

## Features
- Paste/edit Mermaid markup on the left, click **Render** to preview on the right.
- Click **Clear** to clear the Editor pane.
- Bundled `mermaid.min.js` for offline use.
- On startup attempts to fetch the latest Mermaid version (if online) and self-updates the local bundle.
- Persists last diagram in user settings (`AppData\MermaidPad\settings.json`).
- Drag the divider to resize the editor pane.
- **Native OS dialogs for error and information messages:**
  - Windows: `MessageBox`
  - Linux: `zenity`, `kdialog`, `yad`, `Xdialog`, `gxmessage`, or console fallback
  - macOS: `NSAlert` via native APIs
- **Checks platform compatibility at startup to prevent architecture/OS mismatches**

## Requirements
- .NET 9 Runtime ([Download here](https://dotnet.microsoft.com/download/dotnet/9.0))
- Windows: WebView2 Runtime ([Download here](https://developer.microsoft.com/en-us/microsoft-edge/webview2/?form=MA13LH#download))
- Linux: libwebkit2gtk-4.0-37+ (for WebKit support)
  - For graphical dialogs: zenity, kdialog, yad, Xdialog, or gxmessage (otherwise falls back to console)
- macOS: WebKit (included on macOS by default - [Download](https://webkit.org/downloads/))

## Planned
- Syntax highlighting (AvaloniaEdit custom definition)
- SVG/PNG export
- App Update mechanism

## Usage
<img width="2318" height="1381" alt="image" src="https://github.com/user-attachments/assets/bba73b63-e908-493e-829c-99a74adeba61" />

## Examples
Here are some examples - you can find these [here](https://github.com/udlose/MermaidPad/blob/main/MermaidSamples.txt).

### Graphs
```
graph TD
    A[Start Build] --> B{runtimeFlavor specified?}
    B -->|No| C[Default to CoreCLR + subset logic]
    B -->|Yes| D{Which flavor?}
    D -->|coreclr| E[CoreCLR only mode]
    D -->|mono| F[Mono only mode]
    C --> G[Build CoreCLR components + whatever's in subset]
    E --> H{subset contains mono?}
    F --> I{needs CoreCLR deps?}
    H -->|Yes| J[CONFLICT - can't build mono in CoreCLR-only mode]
    H -->|No| K[Build CoreCLR only]
    I -->|Yes| L[FAIL - missing corehost dependencies]
    I -->|No| M[Build Mono only]
    G --> N[SUCCESS - builds both as needed]
```
<img width="3833" height="2070" alt="image" src="https://github.com/user-attachments/assets/75df1b41-f573-4a99-acfc-72ee978af17a" />

### Sequence Diagrams
```
sequenceDiagram
    participant User
    participant System
    User->>System: Request data
    System-->>User: Send data
    User->>System: Process data
    System-->>User: Confirmation
    User->>System: Logout
```
<img width="2317" height="1364" alt="image" src="https://github.com/user-attachments/assets/4a263479-4d43-41c3-a86d-c7e3263d7959" />

### Class Diagrams
```
classDiagram
    class Animal {
        +String name
        +int age
        +makeSound()
    }
    class Dog {
        +String breed
        +bark()
    }
    Animal <|-- Dog
    class Cat {
        +String color
        +meow()
    }
    Animal <|-- Cat
    class Bird {
        +String species
        +fly()
    }
    Animal <|-- Bird
    class Fish {
        +String type
        +swim()
    }
    Animal <|-- Fish
    class Reptile {
        +String habitat
        +crawl()
    }
    Animal <|-- Reptile
    class Insect {
        +String wingspan
        +buzz()
    }
    Animal <|-- Insect
```
<img width="2319" height="1365" alt="image" src="https://github.com/user-attachments/assets/51c8a3e2-d48f-4090-959c-90cc7537bf2a" />

### State Diagrams
```
stateDiagram
    [*] --> Idle
    Idle --> Processing : start
    Processing --> Completed : finish
    Processing --> Error : fail
    Completed --> Idle : reset
    Error --> Idle : reset
```
<img width="2320" height="1361" alt="image" src="https://github.com/user-attachments/assets/9c7bf077-9272-4a34-89b4-f0572e5168be" />

### Gantt Charts
```
gantt
    title Project Timeline
    dateFormat  YYYY-MM-DD
    section Planning
    Task 1 :a1, 2023-10-01, 30d
    Task 2 :after a1, 20d
    section Development
    Task 3 :2023-11-01, 40d
    Task 4 :after a3, 30d
    section Testing
    Task 5 :2024-01-01, 20d
    Task 6 :after a5, 15d
```
<img width="2318" height="1360" alt="image" src="https://github.com/user-attachments/assets/8b3179ea-e73c-4877-9284-e0b33f9f9390" />

### Pie Charts
```
pie
    title Browser Usage
    "Chrome": 45
    "Firefox": 30
    "Safari": 15
    "Others": 10
```
<img width="2315" height="1362" alt="image" src="https://github.com/user-attachments/assets/3f7a7c94-6ce9-4954-a4de-bdc061027a2b" />
