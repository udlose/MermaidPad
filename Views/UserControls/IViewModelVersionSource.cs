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

namespace MermaidPad.Views.UserControls;

/// <summary>
/// Represents a source that provides the current view model instance and its associated version for change tracking or
/// synchronization scenarios.
/// </summary>
/// <typeparam name="TViewModel">The type of the view model provided by the source. Must be a reference type.</typeparam>
internal interface IViewModelVersionSource<out TViewModel> where TViewModel : class
{
    /// <summary>
    /// Gets the currently active view model instance, or null if no view model is active.
    /// </summary>
    TViewModel? CurrentViewModel { get; }

    /// <summary>
    /// Gets the current version number of the underlying data or resource.
    /// </summary>
    long CurrentVersion { get; }
}
