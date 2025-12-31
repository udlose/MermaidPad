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

using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MermaidPad.ViewModels;

/// <summary>
/// Provides a base class for view models that supports property change notification and common functionality for
/// Avalonia applications.
/// </summary>
/// <remarks>Inherit from this class to implement view models that require observable properties and integration
/// with Avalonia's application and window lifetime management. This class is intended for use in MVVM architectures
/// within Avalonia desktop applications.</remarks>
internal abstract partial class ViewModelBase : ObservableObject
{
    /// <summary>
    /// Retrieves the main window of the current desktop-style Avalonia application, if available.
    /// </summary>
    /// <remarks>This method returns <see langword="null"/> if the application is not using a classic
    /// desktop-style lifetime or if no main window is set. It is typically used to obtain a window to act as an owner
    /// for dialogs or other UI elements.</remarks>
    /// <returns>The main <see cref="Window"/> instance if the application is running with a desktop-style lifetime; otherwise,
    /// <see langword="null"/>.</returns>
    protected static Window? GetParentWindow()
    {
        if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }
        return null;
    }
}
