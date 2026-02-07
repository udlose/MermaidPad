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

namespace MermaidPad.ViewModels.Dialogs.Configs;

/// <summary>
/// Specifies the icon type to display in a dialog, indicating the nature of the message such as success, error,
/// warning, or information. Default is <see cref="None"/> which indicates no icon should be displayed.
/// </summary>
/// <remarks>Use this enumeration to select an appropriate icon for dialog boxes based on the context of the
/// message. The values represent common dialog states and help users quickly identify the purpose or severity of the
/// dialog.</remarks>
internal enum DialogIcon
{
    None = 0,   // uninitialized/default value
    Success,
    SuccessCircled,
    Error,
    Warning,
    Information
}
