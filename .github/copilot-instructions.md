# MermaidPad

MermaidPad is a cross-platform Mermaid chart editor built with .NET 9 and Avalonia.
It leverages MermaidJS for rendering diagrams and supports Windows, Linux, and
macOS (x64/arm64). MermaidPad offers a streamlined experience for editing, previewing,
and exporting Mermaid diagrams.

## General Guidelines

- Never remove the user's existing comments from their code when editing files. When generating mutated code, preserve the user’s existing code including comments; do not omit or rewrite existing comments when showing changes.
- Expect consistency in guidance; if recommending a pattern (Post vs InvokeAsync), explain trade-offs and context (UI responsiveness vs ordering/backpressure), and avoid contradicting without clarifying assumptions.
- When making requested changes, only perform minimal diffs scoped to the requested area; do not modify unrelated code.
- Avoid providing responses in git patch diff format; provide full blocks/snippets instead.
