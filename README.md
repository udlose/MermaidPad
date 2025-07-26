# MermaidPad

A minimal cross-platform-ready (Windows-first) Mermaid diagram editor built with .NET 9 + Avalonia.

## Features (MVP)
- Paste/edit Mermaid markup on the left, click **Render** to preview on the right.
- Bundled `mermaid.min.js` for offline use.
- On startup attempts to fetch the latest Mermaid version (if online) and self-updates the local bundle.
- Persists last diagram in user settings (AppData\MermaidPad\settings.json).

## Planned / Stubbed
- Real-time live preview (debounced)
- Syntax highlighting (AvaloniaEdit custom definition) //TODO
- SVG/PNG export //TODO
- Telemetry (opt-in) //TODO
- Cross-platform Linux/macOS implementations //TODO

## Build
```bash
dotnet restore
dotnet run --project src/MermaidPad
