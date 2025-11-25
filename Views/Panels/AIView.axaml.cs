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

using Avalonia.Controls;

namespace MermaidPad.Views.Panels;

/// <summary>
/// Represents a user interface control for displaying AI-related content or functionality within an application.
/// </summary>
/// <remarks>This control is intended to be used as part of a Windows Forms application. It is a sealed partial
/// class, which means it cannot be inherited and may be extended in other partial class definitions. The control should
/// be added to a form or container to present AI features to users.</remarks>
public sealed partial class AIView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the AIView class and sets up the user interface components.
    /// </summary>
    /// <remarks>Call this constructor to create and display a new AIView. The user interface elements are
    /// initialized and ready for interaction after construction.</remarks>
    public AIView()
    {
        InitializeComponent();
    }
}
