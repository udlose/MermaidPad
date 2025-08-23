namespace MermaidPad.Models;

public sealed class AppSettings
{
    public string? LastDiagramText { get; set; }
    public string BundledMermaidVersion { get; set; } = "11.10.1";
    public string? LatestCheckedMermaidVersion { get; set; }
    public bool AutoUpdateMermaid { get; set; }
    public bool LivePreviewEnabled { get; set; } = true;
    public int EditorSelectionStart { get; set; }
    public int EditorSelectionLength { get; set; }
    public int EditorCaretOffset { get; set; }
}
