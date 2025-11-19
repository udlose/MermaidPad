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

/// <summary>
/// Provides platform-specific services for displaying native operating system dialogs and related functionality.
/// </summary>
/// <remarks>Implementations of this interface enable applications to interact with platform-native features, such
/// as showing dialogs, in a way that is abstracted from the underlying operating system. This allows for consistent
/// behavior across different platforms while leveraging native capabilities.</remarks>
public interface IPlatformServices
{
    /// <summary>
    /// Shows a native OS dialog with the specified title and message.
    /// </summary>
    /// <param name="title">Dialog title</param>
    /// <param name="message">Dialog message</param>
    void ShowNativeDialog(string title, string message);

    /// <summary>
    /// Encrypts a plaintext string using platform-specific secure storage.
    /// Windows uses DPAPI (Data Protection API), Linux/macOS use AES-GCM authenticated encryption.
    /// </summary>
    /// <param name="plaintext">The plaintext string to encrypt</param>
    /// <returns>Base64-encoded encrypted string</returns>
    string EncryptString(string plaintext);

    /// <summary>
    /// Decrypts an encrypted string using platform-specific secure storage.
    /// Windows uses DPAPI (Data Protection API), Linux/macOS use AES-GCM authenticated encryption.
    /// </summary>
    /// <param name="encrypted">The Base64-encoded encrypted string</param>
    /// <returns>Decrypted plaintext string</returns>
    string DecryptString(string encrypted);
}

/// <summary>
/// Provides access to platform-specific services for the current operating system using a factory pattern.
/// </summary>
/// <remarks>This class automatically selects the appropriate implementation of <see cref="IPlatformServices"/>
/// based on the detected operating system at runtime. Only Windows, Linux, and macOS are supported. The factory pattern
/// enables easy extension and customization for additional platforms if needed.</remarks>
public static class PlatformServiceFactory
{
    /// <summary>
    /// Gets the singleton instance of the platform services provider for the current application environment.
    /// </summary>
    /// <remarks>Use this property to access platform-specific functionality through the <see
    /// cref="IPlatformServices"/> interface. The returned instance is initialized once and shared throughout the
    /// application's lifetime.</remarks>
    public static IPlatformServices Instance { get; } = Create();

    /// <summary>
    /// Creates an instance of <see cref="IPlatformServices"/> that provides platform-specific services for the current
    /// operating system.
    /// </summary>
    /// <remarks>This method selects the correct platform services implementation based on the runtime
    /// environment. Only Windows, Linux, and macOS are supported; other platforms will result in an
    /// exception.</remarks>
    /// <returns>An implementation of <see cref="IPlatformServices"/> appropriate for the detected operating system.</returns>
    /// <exception cref="PlatformNotSupportedException">Thrown if the current operating system is not Windows, Linux, or macOS.</exception>
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
