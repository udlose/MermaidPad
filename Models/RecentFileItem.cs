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

using CommunityToolkit.Mvvm.Input;

namespace MermaidPad.Models;

/// <summary>
/// Represents a recent file item for display in the Recent Files menu.
/// </summary>
/// <remarks>
/// This wrapper class solves the Avalonia binding limitation where MenuItem items
/// generated from a simple string collection cannot easily bind to commands on the
/// parent ViewModel. By wrapping the file path with a reference to the command,
/// each menu item can properly bind to both its display text and the open command.
/// </remarks>
internal sealed class RecentFileItem
{
    /// <summary>
    /// Gets the full path to the recent file.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets the file name (without path) for display in the menu.
    /// </summary>
    public string FileName => Path.GetFileName(FilePath);

    /// <summary>
    /// Gets the command to execute when this recent file is selected.
    /// </summary>
    public IAsyncRelayCommand<string> OpenCommand { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RecentFileItem"/> class.
    /// </summary>
    /// <param name="filePath">The full path to the file.</param>
    /// <param name="openCommand">The command to open this file.</param>
    public RecentFileItem(string filePath, IAsyncRelayCommand<string> openCommand)
    {
        FilePath = filePath;
        OpenCommand = openCommand;
    }
}
