// MIT License
// Copyright (c) 2025 Dave Black
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.Services.Platforms;

public interface IPlatformServices
{
    /// <summary>
    /// Shows a native OS dialog with the specified title and message.
    /// </summary>
    /// <param name="title">Dialog title</param>
    /// <param name="message">Dialog message</param>
    void ShowNativeDialog(string title, string message);
}

public static class PlatformServiceFactory
{
    public static IPlatformServices Instance { get; } = Create();

    [SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "The factory pattern allows for easy extension and platform-specific implementations.")]
    private static IPlatformServices Create()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsPlatformServices();
        }
        if (OperatingSystem.IsLinux())
        {
            return new LinuxPlatformServices();
        }
        if (OperatingSystem.IsMacOS())
        {
            return new MacPlatformServices();
        }

        Debug.Fail("Unsupported operating system. Only Windows, Linux, and macOS are supported.");
        throw new PlatformNotSupportedException("Unsupported operating system. Only Windows, Linux, and macOS are supported.");
    }
}
