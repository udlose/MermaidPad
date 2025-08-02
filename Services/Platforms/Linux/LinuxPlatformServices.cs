// ReSharper disable CheckNamespace

using MermaidPad.Services.Platforms;
using System.Runtime.Versioning;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace MermaidPad.Services;
#pragma warning restore IDE0130 // Namespace does not match folder structure

//TODO - add implementation
[SupportedOSPlatform("linux")]
public sealed class LinuxPlatformServices : IPlatformServices
{
    public string GetAssetsDirectory()
    {
        //TODO - add implementation (likely same strategy as Windows)
        return ".";
    }
}
