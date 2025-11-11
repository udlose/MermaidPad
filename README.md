# MermaidPad

[![Release](https://img.shields.io/github/v/release/udlose/MermaidPad?style=flat-square)](https://github.com/udlose/MermaidPad/releases/latest)
[![Build and Release](https://github.com/udlose/MermaidPad/actions/workflows/build-and-release.yml/badge.svg)](https://github.com/udlose/MermaidPad/actions/workflows/build-and-release.yml)
[![Contributors](https://img.shields.io/github/contributors/udlose/MermaidPad?style=flat-square)](https://github.com/udlose/MermaidPad/graphs/contributors)
[![Stars](https://img.shields.io/github/stars/udlose/MermaidPad?style=flat-square)](https://github.com/udlose/MermaidPad/stargazers)
[![Forks](https://img.shields.io/github/forks/udlose/MermaidPad?style=flat-square)](https://github.com/udlose/MermaidPad/network/members)
[![Issues](https://img.shields.io/github/issues/udlose/MermaidPad?style=flat-square)](https://github.com/udlose/MermaidPad/issues)
[![Issues Closed](https://img.shields.io/github/issues-closed-raw/udlose/MermaidPad?style=flat-square)](https://github.com/udlose/MermaidPad/issues?q=is%3Aissue+is%3Aclosed)
[![Top Language](https://img.shields.io/github/languages/top/udlose/MermaidPad?style=flat-square)](https://github.com/udlose/MermaidPad)
[![Last Commit](https://img.shields.io/github/last-commit/udlose/MermaidPad?style=flat-square)](https://github.com/udlose/MermaidPad/commits/main)
[![License](https://img.shields.io/github/license/udlose/MermaidPad?style=flat-square)](https://github.com/udlose/MermaidPad/blob/main/LICENSE)

---

## Table of Contents
- [Overview](#overview)
- [Features](#features)
- [Supported Diagrams & Examples](#supported-diagrams--examples)
- [Requirements](#requirements)
- [Technologies Used](#technologies-used)
- [Installation](#installation)
  - [macOS](#macos)
  - [Windows](#windows)
  - [Linux](#linux)
  - [Advanced Users / Developers](#advanced-users--developers)
- [Downloads](#downloads)
- [Usage](#usage)
- [Distribution & Build Process](#distribution--build-process)
- [Building & Publishing](#building--publishing)
- [JavaScript/HTML Linting (ESLint v9)](#javascripthtml-linting-eslint-v9)
- [Roadmap](#roadmap)
- [Contributing](#contributing)
- [License](#license)
- [Support](#support)
- [Contact](#contact)

---

## Overview

**MermaidPad** is a cross-platform Mermaid chart editor built with .NET 9 and Avalonia.
It leverages [MermaidJS](https://mermaid.js.org/) for rendering diagrams and supports Windows, Linux, and macOS (x64/arm64).
MermaidPad offers a streamlined experience for editing, previewing, and exporting Mermaid diagrams.

---

## Features

- 23 different chart and diagram types supported (see examples below)
- Edit Mermaid markup and preview diagrams in real-time
- "Live Preview" for instant rendering
- Includes bundled `mermaid.min.js` and other JavaScript assets for offline use
- Persistent storage of last diagram in user settings (`AppData\MermaidPad\settings.json`)
- Adjustable editor pane via draggable divider
- SHA256 integrity check for bundled file assets (verifies assets at startup and during updates)
- Adaptive layout support for ELK (improves automatic layout fitting and reflow)
- Export diagrams to `.png` and `.svg` (high-fidelity exports that preserve viewBox and styles)
- Highlight connectors on hover for easier tracing of relationships
- Performance optimizations across rendering, export and asset verification for faster, lower-memory operation
- Native OS dialogs for error and information messages:
  - **Windows:** MessageBox
  - **Linux:** zenity, kdialog, yad, Xdialog, gxmessage, or console fallback
  - **macOS:** NSAlert via native APIs
- Platform compatibility checks at startup
---

## Supported Diagrams & Examples


<details>
<summary>Architecture Diagrams</summary>

```
architecture-beta
    group api(cloud)[API]

    service db(database)[Database] in api
    service disk1(disk)[Storage] in api
    service disk2(disk)[Storage] in api
    service server(server)[Server] in api

    db:L -- R:server
    disk1:T -- B:server
    disk2:T -- B:db
```
</details>

<details>
<summary>Block Diagrams</summary>

```
block-beta
columns 1
  db(("DB"))
  blockArrowId6<["&nbsp;&nbsp;&nbsp;"]>(down)
  block:ID
    A
    B["A wide one in the middle"]
    C
  end
  space
  D
  ID --> D
  C --> D
  style B fill:#969,stroke:#333,stroke-width:4px
```
</details>

<details>
<summary>C4 Diagrams</summary>

```
C4Context
    title System Context diagram for Internet Banking System
    Enterprise_Boundary(b0, "BankBoundary0") {
        Person(customerA, "Banking Customer A", "A customer of the bank, with personal bank accounts.")
        Person(customerB, "Banking Customer B")
        Person_Ext(customerC, "Banking Customer C", "desc")

        Person(customerD, "Banking Customer D", "A customer of the bank, <br/> with personal bank accounts.")

        System(SystemAA, "Internet Banking System", "Allows customers to view information about their bank accounts, and make payments.")

        Enterprise_Boundary(b1, "BankBoundary") {
            SystemDb_Ext(SystemE, "Mainframe Banking System", "Stores all of the core banking information about customers, accounts, transactions, etc.")

            System_Boundary(b2, "BankBoundary2") {
                System(SystemA, "Banking System A")
                System(SystemB, "Banking System B", "A system of the bank, with personal bank accounts. next line.")
            }

            System_Ext(SystemC, "E-mail system", "The internal Microsoft Exchange e-mail system.")
            SystemDb(SystemD, "Banking System D Database", "A system of the bank, with personal bank accounts.")

            Boundary(b3, "BankBoundary3", "boundary") {
                SystemQueue(SystemF, "Banking System F Queue", "A system of the bank.")
                SystemQueue_Ext(SystemG, "Banking System G Queue", "A system of the bank, with personal bank accounts.")
            }
        }
    }

    BiRel(customerA, SystemAA, "Uses")
    BiRel(SystemAA, SystemE, "Uses")
    Rel(SystemAA, SystemC, "Sends e-mails", "SMTP")
    Rel(SystemC, customerA, "Sends e-mails to")
```
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
<summary>Entity Relationship Diagrams</summary>

```
erDiagram
    CUSTOMER ||--o{ ORDER : places
    ORDER ||--|{ ORDER_ITEM : contains
    PRODUCT ||--o{ ORDER_ITEM : includes
    CUSTOMER {
        string id
        string name
        string email
    }
    ORDER {
        string id
        date orderDate
        string status
    }
    PRODUCT {
        string id
        string name
        float price
    }
    ORDER_ITEM {
        int quantity
        float price
    }
```
</details>

<details>
<summary>Flowcharts</summary>

```
---
config:
  layout: elk
---
flowchart TD
    %% Nodes
    Arcade["<strong>Arcade SDK</strong><br/>(dotnet/arcade)<br/><strong>Not Shippable<br/>Status:✅</strong>"]
    Runtime["*Runtime*<br/>(dotnet/runtime)<br/><strong>Status:✅</strong>"]
    Extensions["Extensions<br/>(dotnet/extensions)"]
    EFCore["Entity Framework Core<br/>(dotnet/efcore)<br/><strong>Status:✅</strong>"]
    ASPNetCore["ASP.NET Core<br/>(dotnet/aspnetcore)<br/><strong>Status:✅</strong>"]
    WinForms["WinForms<br/>(dotnet/winforms)<br/><strong>Status:✅</strong>"]
    WPF["WPF<br/>(dotnet/wpf)<br/><strong>Status:✅</strong>"]
    WindowsDesktop["Windows Desktop<br/>(dotnet/windowsdesktop)<br/><strong>Status:✅</strong>"]
    SDK["SDK<br/>(dotnet/sdk)<br/><strong>Status:✅</strong>"]
    Installer["Installer<br/>(dotnet/installer)<br/><strong>Status:✅</strong>"]

    %% Build-time (Arcade SDK) dependencies
    Arcade --> Runtime
    Arcade --> Extensions
    Arcade --> EFCore
    Arcade --> ASPNetCore
    Arcade --> WinForms
    Arcade --> WPF
    Arcade --> WindowsDesktop
    Arcade --> SDK
    Arcade --> Installer

    %% Runtime as foundational dependency
    Runtime --> Extensions
    Runtime --> EFCore
    Runtime --> ASPNetCore
    Runtime --> WinForms
    Runtime --> WPF
    Runtime --> WindowsDesktop
    Runtime --> SDK
    Runtime --> Installer

    %% Cross-component dependencies
    EFCore --> ASPNetCore
    EFCore --> SDK
    
    WinForms --> WPF
    WinForms --> WindowsDesktop
    WinForms --> Installer

    WPF --> WindowsDesktop
    WPF --> SDK
    WPF --> Installer

    ASPNetCore --> SDK
    ASPNetCore --> Installer

    WindowsDesktop --> SDK
    WindowsDesktop --> Installer

    SDK --> Installer
```
</details>

<details>
<summary>Flowchart-elk</summary>

```
flowchart-elk TD
    %% Nodes
    Arcade["<strong>Arcade SDK</strong><br/>(dotnet/arcade)<br/><strong>Not Shippable<br/>Status:✅</strong>"]
    Runtime["*Runtime*<br/>(dotnet/runtime)<br/><strong>Status:✅</strong>"]
    Extensions["Extensions<br/>(dotnet/extensions)"]
    EFCore["Entity Framework Core<br/>(dotnet/efcore)<br/><strong>Status:✅</strong>"]
    ASPNetCore["ASP.NET Core<br/>(dotnet/aspnetcore)<br/><strong>Status:✅</strong>"]
    WinForms["WinForms<br/>(dotnet/winforms)<br/><strong>Status:✅</strong>"]
    WPF["WPF<br/>(dotnet/wpf)<br/><strong>Status:✅</strong>"]
    WindowsDesktop["Windows Desktop<br/>(dotnet/windowsdesktop)<br/><strong>Status:✅</strong>"]
    SDK["SDK<br/>(dotnet/sdk)<br/><strong>Status:✅</strong>"]
    Installer["Installer<br/>(dotnet/installer)<br/><strong>Status:✅</strong>"]

    %% Build-time (Arcade SDK) dependencies
    Arcade --> Runtime
    Arcade --> Extensions
    Arcade --> EFCore
    Arcade --> ASPNetCore
    Arcade --> WinForms
    Arcade --> WPF
    Arcade --> WindowsDesktop
    Arcade --> SDK
    Arcade --> Installer

    %% Runtime as foundational dependency
    Runtime --> Extensions
    Runtime --> EFCore
    Runtime --> ASPNetCore
    Runtime --> WinForms
    Runtime --> WPF
    Runtime --> WindowsDesktop
    Runtime --> SDK
    Runtime --> Installer

    %% Cross-component dependencies
    EFCore --> ASPNetCore
    EFCore --> SDK
    
    WinForms --> WPF
    WinForms --> WindowsDesktop
    WinForms --> Installer

    WPF --> WindowsDesktop
    WPF --> SDK
    WPF --> Installer

    ASPNetCore --> SDK
    ASPNetCore --> Installer

    WindowsDesktop --> SDK
    WindowsDesktop --> Installer

    SDK --> Installer
```
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
<summary>Git Graphs</summary>

```
gitGraph
    commit
    branch develop
    checkout develop
    commit
    commit
    checkout main
    merge develop
    commit
    branch feature
    checkout feature
    commit
    commit
    checkout main
    merge feature
```
</details>

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
<summary>Kanban Diagrams</summary>

```
---
config:
  kanban:
    ticketBaseUrl: 'https://github.com/mermaid-js/mermaid/issues/#TICKET#'
---
kanban
  Todo
    [Create Documentation]
    docs[Create Blog about the new diagram]
  [In progress]
    id6[Create renderer so that it works in all cases. We also add some extra text here for testing purposes. And some more just for the extra flare.]
  id9[Ready for deploy]
    id8[Design grammar]@{ assigned: 'knsv' }
  id10[Ready for test]
    id4[Create parsing tests]@{ ticket: 2038, assigned: 'K.Sveidqvist', priority: 'High' }
    id66[last item]@{ priority: 'Very Low', assigned: 'knsv' }
  id11[Done]
    id5[define getData]
    id2[Title of diagram is more than 100 chars when user duplicates diagram with 100 char]@{ ticket: 2036, priority: 'Very High'}
    id3[Update DB function]@{ ticket: 2037, assigned: knsv, priority: 'High' }

  id12[Can't reproduce]
    id3[Weird flickering in Firefox]
```
</details>

<details>
<summary>Mindmap Diagrams</summary>

```
mindmap
  root((mindmap))
    Origins
      Long history
      ::icon(fa fa-book)
      Popularisation
        British popular psychology author Tony Buzan
    Research
      On effectiveness<br/>and features
      On Automatic creation
        Uses
            Creative techniques
            Strategic planning
            Argument mapping
    Tools
      Pen and paper
      Mermaid
```
</details>

<details>
<summary>Packet Diagrams</summary>

```
---
title: "TCP Packet"
---
packet-beta
0-15: "Source Port"
16-31: "Destination Port"
32-63: "Sequence Number"
64-95: "Acknowledgment Number"
96-99: "Data Offset"
100-105: "Reserved"
106: "URG"
107: "ACK"
108: "PSH"
109: "RST"
110: "SYN"
111: "FIN"
112-127: "Window"
128-143: "Checksum"
144-159: "Urgent Pointer"
160-191: "(Options and Padding)"
192-255: "Data (variable length)"
```
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

<details>
<summary>Quadrant Charts</summary>

```
quadrantChart
    title Reach and engagement of campaigns
    x-axis Low Reach --> High Reach
    y-axis Low Engagement --> High Engagement
    quadrant-1 We should expand
    quadrant-2 Need to promote
    quadrant-3 Re-evaluate
    quadrant-4 May be improved
    Campaign A: [0.3, 0.6]
    Campaign B: [0.45, 0.23]
    Campaign C: [0.57, 0.69]
    Campaign D: [0.78, 0.34]
    Campaign E: [0.40, 0.34]
    Campaign F: [0.35, 0.78]
```
</details>

<details>
<summary>Radar Charts</summary>

```
radar-beta
  title Skill Radar — Dave
  axis csharp["C#"], dotnet[".NET Runtime"], aspnet["ASP.NET Core"], efcore["EF Core"], wpf["WPF"], cve["CVE Analysis"], perf["Performance"], maui[".NET MAUI"]
  curve current["Current Strengths"]{10,9,7,6,6,7,8,5}
  curve target["Growth Goals 2025"]{9,10,9,8,7,9,9,7}
  showLegend true
  max 10
  min 0
  graticule polygon
  ticks 5
```
</details>

<details>
<summary>Requirement Diagrams</summary>

```
requirementDiagram

    requirement test_req {
    id: 1
    text: the test text.
    risk: high
    verifymethod: test
    }

    element test_entity {
    type: simulation
    }

    test_entity - satisfies -> test_req
```
</details>

<details>
<summary>Sankey Diagrams</summary>

```
---
config:
  sankey:
    showValues: false
---
sankey-beta
  Agricultural 'waste',Bio-conversion,124.729
  Bio-conversion,Liquid,0.597
  Bio-conversion,Losses,26.862
  Bio-conversion,Solid,280.322
  Bio-conversion,Gas,81.144
  Biofuel imports,Liquid,35
  Biomass imports,Solid,35
  Coal imports,Coal,11.606
  Coal reserves,Coal,63.965
  Coal,Solid,75.571
  District heating,Industry,10.639
  District heating,Heating and cooling - commercial,22.505
  District heating,Heating and cooling - homes,46.184
  Electricity grid,Over generation / exports,104.453
  Electricity grid,Heating and cooling - homes,113.726
  Electricity grid,H2 conversion,27.14
  Electricity grid,Industry,342.165
  Electricity grid,Road transport,37.797
  Electricity grid,Agriculture,4.412
  Electricity grid,Heating and cooling - commercial,40.858
  Electricity grid,Losses,56.691
  Electricity grid,Rail transport,7.863
  Electricity grid,Lighting & appliances - commercial,90.008
  Electricity grid,Lighting & appliances - homes,93.494
  Gas imports,NGas,40.719
  Gas reserves,NGas,82.233
  Gas,Heating and cooling - commercial,0.129
  Gas,Losses,1.401
  Gas,Thermal generation,151.891
  Gas,Agriculture,2.096
  Gas,Industry,48.58
  Geothermal,Electricity grid,7.013
  H2 conversion,H2,20.897
  H2 conversion,Losses,6.242
  H2,Road transport,20.897
  Hydro,Electricity grid,6.995
  Liquid,Industry,121.066
  Liquid,International shipping,128.69
  Liquid,Road transport,135.835
  Liquid,Domestic aviation,14.458
  Liquid,International aviation,206.267
  Liquid,Agriculture,3.64
  Liquid,National navigation,33.218
  Liquid,Rail transport,4.413
  Marine algae,Bio-conversion,4.375
  NGas,Gas,122.952
  Nuclear,Thermal generation,839.978
  Oil imports,Oil,504.287
  Oil reserves,Oil,107.703
  Oil,Liquid,611.99
  Other waste,Solid,56.587
  Other waste,Bio-conversion,77.81
  Pumped heat,Heating and cooling - homes,193.026
  Pumped heat,Heating and cooling - commercial,70.672
  Solar PV,Electricity grid,59.901
  Solar Thermal,Heating and cooling - homes,19.263
  Solar,Solar Thermal,19.263
  Solar,Solar PV,59.901
  Solid,Agriculture,0.882
  Solid,Thermal generation,400.12
  Solid,Industry,46.477
  Thermal generation,Electricity grid,525.531
  Thermal generation,Losses,787.129
  Thermal generation,District heating,79.329
  Tidal,Electricity grid,9.452
  UK land based bioenergy,Bio-conversion,182.01
  Wave,Electricity grid,19.013
  Wind,Electricity grid,289.366
```
</details>

<details>
<summary>Sequence Diagrams</summary>

```
sequenceDiagram
    participant Alice
    participant Bob
    Alice->>John: Hello John, how are you?
    loop HealthCheck
        John->>John: Fight against hypochondria
    end
    Note right of John: Rational thoughts<br/>prevail...
    John-->>Alice: Great!
    John->>Bob: How about you?
    Bob-->>John: Jolly good!
```
<img width="2317" height="1364" alt="Sequence Diagram Example" src="https://github.com/user-attachments/assets/4a263479-4d43-41c3-a86d-c7e3263d7959" />
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
<summary>Timelines</summary>

```
%%{init: {"theme":"forest","themeVariables":{"fontSize":"14px"}}}%%
timeline
    title History of Social Media Platform
    2002 : LinkedIn
    2004 : Facebook
         : Google
    2005 : YouTube
    2006 : Twitter
```
</details>

<details>
<summary>Tree Maps</summary>

```
treemap-beta
"Section 1"
    "Leaf 1.1": 12
    "Section 1.2"
      "Leaf 1.2.1": 12
"Section 2"
    "Leaf 2.1": 20
    "Leaf 2.2": 25
```
</details>

<details>
<summary>User Journey</summary>

```
%%{init: {"theme":"dark","themeVariables":{"fontSize":"14px"}}}%%
journey
  title CVE Remediation Process
  section Investigation
    Identify potential CVE in .NET runtime: 5: Dave
    Reproduce issue in test environment: 4: Alice
  section Analysis
    Review affected components (SDK, ASP.NET Core, WPF): 4: Juan
    Evaluate exploit severity and attack vector: 3: Jamal
  section Remediation
    Patch vulnerable code and write regression tests: 5: Takashi
    Internal security review of the patch: 4: Priya
    Submit PR and request security review: 4: Dave
  section Verification
    Validate mitigation effectiveness: 5: QA Team
    Merge into main and tag security release: 4: Release Eng.

```
</details>

<details>
<summary>XY Charts</summary>

```
xychart-beta
  title "Performance Improvements"
  x-axis ["2020","2021","2022","2023","2024","2025"]
  y-axis "Requests per second" 0 --> 1200
  line [400,650,800,950,1100,1180]
  line [300,500,680,760,820,900]
  bar [200,300,400,500,600,700]
```
</details>
---

## Requirements

- **.NET 9 (or greater) Runtime**: ([Download](https://dotnet.microsoft.com/download/dotnet))
- **Windows:** WebView2 Runtime ([Download](https://developer.microsoft.com/en-us/microsoft-edge/webview2/?form=MA13LH#download))
- **Linux:** `libwebkit2gtk-4.0-37+` (for WebKit support)
  - For graphical dialogs: zenity, kdialog, yad, Xdialog, or gxmessage
- **macOS:** WebKit (included by default, [Download](https://webkit.org/downloads/))

---

## Installation

### macOS

#### Universal DMG - One Download for All Macs (Recommended)

**Download:** `MermaidPad-[version]-universal.dmg`

The Universal DMG is the preferred installation method for macOS users. This single disk image contains optimized binaries for both Intel and Apple Silicon Macs, automatically using the correct architecture for your system.

**Benefits of the Universal DMG:**
- **One file works everywhere**: No need to determine your Mac's architecture
- **Standard macOS experience**: Familiar drag-and-drop installation to Applications folder
- **Preserves permissions**: No manual `chmod +x` commands needed
- **Clean installation**: Mounts as a virtual drive, no unzipping required
- **Smaller download**: More efficient than downloading separate architecture-specific files

**Installation Steps:**
1. Download `MermaidPad-[version]-universal.dmg` from the [latest release](https://github.com/udlose/MermaidPad/releases/latest)
2. Double-click the DMG file to mount it
3. Drag **MermaidPad.app** to the **Applications** folder shortcut
4. Eject the DMG (right-click → Eject)
5. Launch MermaidPad from Applications or Spotlight search

**First Launch (Unsigned App):**
Since the app is not code-signed by Apple, macOS Gatekeeper will prevent normal launching:
1. **Right-click** MermaidPad.app → **"Open"** (don't double-click)
2. Click **"Open"** when prompted about the unidentified developer
3. Subsequent launches will work normally with double-click

**Alternative via Terminal:**
```bash
# Remove quarantine attribute and launch
xattr -cr /Applications/MermaidPad.app && open /Applications/MermaidPad.app
```

#### Alternative: Homebrew
MermaidPad is also available via a [Homebrew tap](https://github.com/udlose/homebrew-tap) for easy installation and updates:

```bash
brew tap udlose/tap
brew install --cask udlose/tap/mermaidpad
```

The Homebrew cask is automatically updated after each release.

#### Advanced Users: Individual Architecture Downloads
Architecture-specific downloads are available for developers and advanced users:
- **Intel x64:** `MermaidPad-[version]-osx-x64.app.zip`
- **Apple Silicon arm64:** `MermaidPad-[version]-osx-arm64.app.zip`

These require manual unzipping and permission setting (`chmod +x MermaidPad`).

### Windows

Download the appropriate version for your processor:
- **x64 (64-bit Intel/AMD):** `MermaidPad-[version]-win-x64.zip`
- **arm64 (Surface Pro X, etc.):** `MermaidPad-[version]-win-arm64.zip`

**Installation:**
1. Download and extract the appropriate .zip file
2. Run `MermaidPad.exe`

### Linux

Download the appropriate version for your processor:
- **x64 (64-bit Intel/AMD):** `MermaidPad-[version]-linux-x64.zip`
- **arm64:** Not supported (CefGlue limitation – see [here](https://github.com/OutSystems/CefGlue/blob/main/LINUX.md#arm64-issues))

**Installation:**
1. Download and extract the appropriate .zip file
2. Make executable: `chmod +x MermaidPad`
3. Run: `./MermaidPad`

### Advanced Users / Developers

Individual architecture-specific downloads are also available for all platforms:
- **macOS Intel x64:** `MermaidPad-[version]-osx-x64.zip`
- **macOS Apple Silicon arm64:** `MermaidPad-[version]-osx-arm64.zip`
- **macOS App Bundles:** `MermaidPad-[version]-osx-[arch].app.zip`

These are provided for developers, CI/CD systems, and users who need specific architectures.

---

## Downloads

Visit the [Releases page](https://github.com/udlose/MermaidPad/releases/latest) to download the latest version.

**Recommended Downloads by Platform:**
- **macOS:** `MermaidPad-[version]-universal.dmg` (works on all Macs)
- **Windows:** `MermaidPad-[version]-win-x64.zip` or `MermaidPad-[version]-win-arm64.zip`
- **Linux:** `MermaidPad-[version]-linux-x64.zip`

---

## Usage

<img width="2318" height="1381" alt="MermaidPad UI" src="https://github.com/user-attachments/assets/bba73b63-e908-493e-829c-99a74adeba61" />

---

## Distribution & Build Process

MermaidPad uses an automated build and release process that creates optimized distributions for each platform:

- **Cross-platform builds** for Windows (x64/arm64), Linux (x64/arm64), and macOS (x64/arm64)
- **Universal macOS DMG** combining both Intel and Apple Silicon binaries
- **macOS app bundles** with proper code signing and Gatekeeper compatibility

---

## Building & Publishing
### For maintainers & contributors

This section documents how builds and releases are produced (CI) and how you can produce and test the same published artifacts locally. It is important to test the published artifact (the CI-produced zip/dmg/app) and not rely only on local dev/debug runs.

Source of truth:
- Project-level build settings: `Directory.Build.props` (net9.0, RIDs, publish configuration)
- Asset integrity: `MermaidPad.csproj.targets` generates `Generated/AssetHashes.cs` at build time
- CI/workflow: `.github/workflows/build-and-release.yml` — uses a matrix to publish and upload artifacts, then creates releases

Key settings
- Target framework: net9.0 (see `Directory.Build.props`)
- Supported RIDs (publish targets): win-x64; win-arm64; linux-x64; osx-arm64; osx-x64
- Publishing defaults: framework-dependent, not single-file (`PublishSelfContained=false`, `PublishSingleFile=false`)

CI behavior (summary)
- The GitHub Action `build-and-release.yml`:
  - Extracts version from a tag (`vX.Y.Z`) or manual input
  - Builds a matrix across RIDs: `win-x64`, `win-arm64`, `linux-x64`, `osx-x64`, `osx-arm64`
  - Runs `dotnet restore` and `dotnet publish` for each RID:
    `dotnet publish MermaidPad.csproj -c Release -r <RID> -o ./publish -p:Version=<version> -p:AssemblyVersion=<version> -p:FileVersion=<version>`
  - Prepares artifacts and uploads zip files named: `MermaidPad-<version>-<rid>.zip`
  - Bundles macOS .app for each arch and creates a universal DMG via additional workflows
  - Creates a GitHub release and attaches the artifacts

Local reproducible publish
1. Ensure prerequisites are installed (see Requirements).
2. Restore:
   `dotnet restore MermaidPad.csproj`
3. Publish for a specific RID (replace `<rid>` and `<version>`):
   - Example (Linux x64):
     `dotnet publish MermaidPad.csproj -c Release -r linux-x64 -o ./publish -p:Version=1.2.3 -p:AssemblyVersion=1.2.3 -p:FileVersion=1.2.3`
   - RIDs available: `win-x64`, `win-arm64`, `linux-x64`, `osx-x64`, `osx-arm64`
4. Package the publish folder the same way CI does:
   - macOS/Linux:
     ```bash
     chmod +x ./publish/MermaidPad
     zip -r "MermaidPad-1.2.3-linux-x64.zip" ./publish/* -x "*.DS_Store*"
     ```
   - Windows (PowerShell):
     ```powershell
     Compress-Archive -Path .\publish\* -DestinationPath "MermaidPad-1.2.3-win-x64.zip"
     ```

Asset integrity
- `MermaidPad.csproj.targets` runs a `GenerateAssetHashes` target (BeforeBuild) which computes SHA256 hashes for embedded web assets and writes `Generated/AssetHashes.cs`. This file is used at runtime to verify the bundled assets — ensure your publish includes the `Assets/` content unchanged.

Testing published artifacts (do this — do not assume local debug == published)
- Download the artifact produced by CI (Release assets or artifact zip produced by `build-and-release.yml`) or use the zip you created locally.
- macOS:
  - For universal DMG: mount the `.dmg`, drag the `.app` to `/Applications`, then run via Finder.
  - For unsigned apps: Right-click → Open on first launch (Gatekeeper).
- Linux:
  - Unzip, set executable, and run:
    ```bash
    chmod +x ./MermaidPad
    ./MermaidPad
    ```
- Windows:
  - Unzip and run `MermaidPad.exe`
  - Ensure WebView2 runtime is installed

Why test published artifacts?
- CI publish packs the app with the runtime/config and file layout the user receives (assets, native libs, packaging differences).
- Local debug builds or F5 runs may differ (different working directory, dev files available, different asset bundling).
- The project adds asset-hash verification — mismatch in bundled assets will surface only in published artifacts.

Release flow (how CI creates a release)
- Tag `vX.Y.Z` and push, or run the workflow manually via `workflow_dispatch` with `version` input.
- The workflow runs a build matrix, produces per-RID zips, bundles macOS .app and a universal DMG, then creates a GitHub Release attaching artifacts.
- Note: The workflow contains a guard that restricts manual runs to the repository owner (`udlose`) — see `restrict-user` job in `.github/workflows/build-and-release.yml`.

Tips for maintainers
- Keep the `Assets/` files in sync with expected hashes or update assets and bump version so `Generated/AssetHashes.cs` is regenerated.
- When testing fixes that affect packaging or runtime behavior, always validate by installing/running the CI-produced artifact or by reproducing the CI publish + packaging steps locally.
- If you change publish settings (single-file, self-contained), update `Directory.Build.props` and update CI accordingly.

---

## JavaScript/HTML Linting (ESLint v9)

MermaidPad ships web assets (e.g., `Assets/index.html`). We use **ESLint v9** with the **flat config** to keep these tidy.

### Prerequisites
- **Node.js 18+** (Node 20+ recommended)

### Install (once per clone)
```bash
npm init -y
npm i -D eslint @eslint/js eslint-plugin-html globals
```

### How to run
Run ESLint against the sources only (avoids crawling `bin/` or WebView profiles):

```bash
npx eslint "Assets/**/*.{html,js,ts}"
```

> Windows note: keep the **double quotes** around the glob patterns.


### Common issues
- **“ESLint couldn't find an eslint.config…”**  
  Ensure `eslint.config.mjs` exists **at repo root** (ESLint v9 uses flat config by default).
- **“Cannot use import statement outside a module” when loading config**  
  Use `eslint.config.mjs` (ESM). Alternatively, set `"type": "module"` in `package.json`—but that affects all `.js` files in the package.
- **ESLint is linting build output/WebView2 files**  
  Verify the **global** `ignores` block above and run ESLint with the explicit `Assets/**/*` glob as shown.
- **“'import' and 'export' may appear only with 'sourceType: module'” in HTML**  
  Keep the HTML override (`files: ['Assets/**/*.html']` + `sourceType: 'module'`).
- **See what ESLint will apply**  
  `npx eslint --print-config "Assets/index.html"` shows the merged config; `npx eslint "Assets/**/*.{html,js,ts}" --debug` shows which files are being linted.

## Roadmap
- ✅ SVG/PNG export (completed)
- ✅ SHA256 asset integrity verification (completed)
- ✅ Adaptive ELK layout support (completed)
- ✅ Highlight connectors on hover (completed)
- ✅ Performance optimizations (completed)
- ✅ Syntax highlighting (completed)
- Pan/Zoom support
- Application update mechanism

---

## Contributing

Contributions are welcome! Please open an issue for feature requests or bug reports. For pull requests, ensure your code adheres to the project's style and includes relevant tests.

---

## License

This project is licensed under the [MIT License](https://github.com/udlose/MermaidPad/blob/main/LICENSE).

---

## Support

If you find MermaidPad useful, consider supporting development:

[![Buy Me A Coffee](https://img.shields.io/badge/Donate-Buy%20Me%20A%20Coffee-FFDD00?style=flat-square&logo=buy-me-a-coffee&logoColor=black)](https://www.buymeacoffee.com/daveblack)

---

## Contact

For questions or feedback, please open an issue or reach out via [GitHub Discussions](https://github.com/udlose/MermaidPad/discussions).
