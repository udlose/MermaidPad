namespace MermaidPad.Models;

public class AppSettings
{
    public string? LastDiagram { get; set; }
    public string BundledMermaidVersion { get; set; } = "UNKNOWN";
    public string? LatestCheckedMermaidVersion { get; set; }
    public bool AutoUpdateMermaid { get; set; } = true;
}
