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

namespace MermaidPad.Services;

/// <summary>
/// Provides a base class for services.
/// </summary>
internal abstract class ServiceBase
{
    private protected const string ApplicationName = "MermaidPad";

    /// <summary>
    /// Initializes a new instance of the ServiceBase class.
    /// </summary>
    protected ServiceBase()
    {
    }

    ///// <summary>
    ///// Returns the per-user configuration directory path used by the application.
    ///// Typically resolves to "%APPDATA%\MermaidPad" on Windows.
    ///// </summary>
    ///// <returns>The full path to the application's config directory for the current user.</returns>
    //private protected static string GetConfigDirectoryPath()
    //{
    //    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    //    return Path.Combine(appData, ApplicationName);
    //}
}
