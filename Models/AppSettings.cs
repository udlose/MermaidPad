namespace MermaidPad.Models;

public class AppSettings
{
    public string? LastDiagramText { get; set; }
    public string BundledMermaidVersion { get; set; } = "11.9.0";
    public string? LatestCheckedMermaidVersion { get; set; }
    public bool AutoUpdateMermaid { get; set; } = true;
    public bool LivePreviewEnabled { get; set; } = true;
    public int EditorSelectionStart { get; set; }
    public int EditorSelectionLength { get; set; }
    public int EditorCaretOffset { get; set; }
}
