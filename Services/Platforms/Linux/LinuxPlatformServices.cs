// ReSharper disable CheckNamespace

using MermaidPad.Services.Platforms;
using System.Runtime.Versioning;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace MermaidPad.Services;
#pragma warning restore IDE0130 // Namespace does not match folder structure

[SupportedOSPlatform("linux")]
public sealed class LinuxPlatformServices : IPlatformServices
{
    //public string GetAssetsDirectory() => Path.Combine(AppContext.BaseDirectory, "Assets");
}
