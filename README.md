# MermaidPad

[![Release](https://img.shields.io/github/v/release/udlose/MermaidPad?style=flat-square)](https://github.com/udlose/MermaidPad/releases/latest)
[![Build and Release](https://github.com/udlose/MermaidPad/actions/workflows/build-and-release.yml/badge.svg)](https://github.com/udlose/MermaidPad/actions/workflows/build-and-release.yml)
[![Bundle macOS .app](https://github.com/udlose/MermaidPad/actions/workflows/macos-bundle.yml/badge.svg)](https://github.com/udlose/MermaidPad/actions/workflows/macos-bundle.yml)
[![Contributors](https://img.shields.io/github/contributors/udlose/MermaidPad?style=flat-square)](https://github.com/udlose/MermaidPad/graphs/contributors)
[![Stars](https://img.shields.io/github/stars/udlose/MermaidPad?style=flat-square)](https://github.com/udlose/MermaidPad/stargazers)
[![Forks](https://img.shields.io/github/forks/udlose/MermaidPad?style=flat-square)](https://github.com/udlose/MermaidPad/network/members)
[![Issues](https://img.shields.io/github/issues/udlose/MermaidPad?style=flat-square)](https://github.com/udlose/MermaidPad/issues)
[![Issues Closed](https://img.shields.io/github/issues-closed-raw/udlose/MermaidPad?style=flat-square)](https://github.com/udlose/MermaidPad/issues?q=is%3Aissue+is%3Aclosed)
[![Top Language](https://img.shields.io/github/languages/top/udlose/MermaidPad?style=flat-square)](https://github.com/udlose/MermaidPad)
[![Last Commit](https://img.shields.io/github/last-commit/udlose/MermaidPad?style=flat-square)](https://github.com/udlose/MermaidPad/commits/main)
[![License](https://img.shields.io/github/license/udlose/MermaidPad?style=flat-square)](https://github.com/udlose/MermaidPad/blob/main/LICENSE)

---

## Overview

**MermaidPad** is a cross-platform Mermaid chart editor built with .NET 9 and Avalonia. It leverages [MermaidJS](https://mermaid.js.org/) for rendering diagrams and supports Windows, Linux, and macOS (x64/arm64). MermaidPad offers a streamlined experience for editing, previewing, and exporting Mermaid diagrams.

---

## Features

- Edit Mermaid markup and preview diagrams in real-time
- "Live Preview" for instant rendering
- Bundled `mermaid.min.js` for offline use
- Automatic update of MermaidJS bundle on startup (if online)
- Persistent storage of last diagram in user settings (`AppData\MermaidPad\settings.json`)
- Adjustable editor pane via draggable divider
- Native OS dialogs for error and information messages:
  - **Windows:** MessageBox
  - **Linux:** zenity, kdialog, yad, Xdialog, gxmessage, or console fallback
  - **macOS:** NSAlert via native APIs
- Platform compatibility checks at startup

---

## Requirements

- [.NET 9 Runtime](https://dotnet.microsoft.com/download/dotnet/9.0)
- **Windows:** [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/?form=MA13LH#download)
- **Linux:** `libwebkit2gtk-4.0-37+` (for WebKit support)
  - For graphical dialogs: zenity, kdialog, yad, Xdialog, or gxmessage
- **macOS:** WebKit (included by default, [Download](https://webkit.org/downloads/))

---

## Installation

### macOS (via Homebrew)

MermaidPad’s macOS `.app` bundle is distributed via a [Homebrew tap](https://github.com/udlose/homebrew-tap) for easy installation and updates.

```
brew tap udlose/tap
brew install --cask udlose/tap/mermaidpad
```

The [Cask file](https://github.com/udlose/homebrew-tap/blob/main/Casks/mermaidpad.rb) is automatically updated after each release.

---

## Usage

<img width="2318" height="1381" alt="MermaidPad UI" src="https://github.com/user-attachments/assets/bba73b63-e908-493e-829c-99a74adeba61" />

---

## Examples

Find more samples in [Assets/Samples/MermaidSamples.txt](https://github.com/udlose/MermaidPad/blob/main/Assets/Samples/MermaidSamples.txt).

<details>
<summary>Graphs</summary>

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
<img width="3833" height="2070" alt="Graph Example" src="https://github.com/user-attachments/assets/75df1b41-f573-4a99-acfc-72ee978af17a" />
</details>

<details>
<summary>Sequence Diagrams</summary>

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
<img width="2317" height="1364" alt="Sequence Diagram Example" src="https://github.com/user-attachments/assets/4a263479-4d43-41c3-a86d-c7e3263d7959" />
</details>

<details>
<summary>Class Diagrams</summary>

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
<img width="2319" height="1365" alt="Class Diagram Example" src="https://github.com/user-attachments/assets/51c8a3e2-d48f-4090-959c-90cc7537bf2a" />
</details>

<details>
<summary>State Diagrams</summary>

```
stateDiagram
    [*] --> Idle
    Idle --> Processing : start
    Processing --> Completed : finish
    Processing --> Error : fail
    Completed --> Idle : reset
    Error --> Idle : reset
```
<img width="2320" height="1361" alt="State Diagram Example" src="https://github.com/user-attachments/assets/9c7bf077-9272-4a34-89b4-f0572e5168be" />
</details>

<details>
<summary>Gantt Charts</summary>

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
<img width="2318" height="1360" alt="Gantt Chart Example" src="https://github.com/user-attachments/assets/8b3179ea-e73c-4877-9284-e0b33f9f9390" />
</details>

<details>
<summary>Pie Charts</summary>

```
pie
    title Browser Usage
    "Chrome": 45
    "Firefox": 30
    "Safari": 15
    "Others": 10
```
<img width="2315" height="1362" alt="Pie Chart Example" src="https://github.com/user-attachments/assets/3f7a7c94-6ce9-4954-a4de-bdc061027a2b" />
</details>

---

## Roadmap

- Syntax highlighting (AvaloniaEdit custom definition)
- SVG/PNG export
- Application update mechanism

---

## Contributing

Contributions are welcome! Please open an issue for feature requests or bug reports. For pull requests, ensure your code adheres to the project’s style and includes relevant tests.

---

## License

This project is licensed under the [MIT License](https://github.com/udlose/MermaidPad/blob/main/LICENSE).

---

## Support

If you find MermaidPad useful, consider supporting development:

[![Donate](https://img.shields.io/badge/Donate-PayPal-green?style=flat-square&logo=paypal)](https://www.paypal.com/donate/?hosted_button_id=YJY8X9LZ6U8NQ)
[![Buy Me A Coffee](https://img.shields.io/badge/Donate-Buy%20Me%20A%20Coffee-FFDD00?style=flat-square&logo=buy-me-a-coffee&logoColor=black)](https://www.buymeacoffee.com/daveblack)

---

## Contact

For questions or feedback, please open an issue or reach out via [GitHub Discussions](https://github.com/udlose/MermaidPad/discussions).
