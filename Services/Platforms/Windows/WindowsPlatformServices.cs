// ReSharper disable CheckNamespace

using MermaidPad.Services.Platforms;
using System.Runtime.Versioning;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace MermaidPad.Services;
#pragma warning restore IDE0130 // Namespace does not match folder structure

[SupportedOSPlatform("windows")]
public sealed class WindowsPlatformServices : IPlatformServices
{
    //public string GetAssetsDirectory() => Path.Combine(AppContext.BaseDirectory, "Assets");
}
