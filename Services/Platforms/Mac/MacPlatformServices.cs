// ReSharper disable CheckNamespace

using MermaidPad.Services.Platforms;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace MermaidPad.Services;
#pragma warning restore IDE0130 // Namespace does not match folder structure

//TODO - add implementation
public sealed class MacPlatformServices : IPlatformServices
{
    public string GetAssetsDirectory()
    {
        //TODO - add implementation (bundle Resources path)
        return ".";
    }
}
