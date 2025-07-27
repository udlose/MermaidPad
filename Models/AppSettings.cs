namespace MermaidPad.Models;

public class AppSettings
{
    public string? LastDiagram { get; set; }
    public string BundledMermaidVersion { get; set; } = "11.9.0";
    public string? LatestCheckedMermaidVersion { get; set; }
    public bool AutoUpdateMermaid { get; set; } = true;
    public bool LivePreviewEnabled { get; set; } = true;
}
