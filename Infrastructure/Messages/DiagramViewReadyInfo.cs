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

namespace MermaidPad.Infrastructure.Messages;

/// <summary>
/// Contains information about the DiagramView's ready state when it attaches to the visual tree.
/// </summary>
/// <remarks>
/// <para>
/// This record is the payload for <see cref="DiagramViewReadyMessage"/>. It provides context
/// about whether the ViewModel requires initialization when the View attaches.
/// </para>
/// <para>
/// <b>Initialization Scenarios:</b>
/// <list type="bullet">
///     <item>
///         <description>
///             <c>RequiresInitialization = true</c>: The ViewModel's <see cref="ViewModels.UserControls.DiagramViewModel.IsReady"/>
///             was <c>false</c> when the View attached. This indicates either initial app startup or
///             a Reset Layout operation where a new ViewModel was created. The receiver should trigger
///             diagram initialization.
///         </description>
///     </item>
///     <item>
///         <description>
///             <c>RequiresInitialization = false</c>: The ViewModel's <see cref="ViewModels.UserControls.DiagramViewModel.IsReady"/>
///             was already <c>true</c> when the View attached. This indicates a dock state change
///             (float/dock/pin) where the View was recreated but the ViewModel retains its state.
///             The DiagramView handles re-initialization automatically via its <c>TriggerReinitialization()</c> method.
///         </description>
///     </item>
/// </list>
/// </para>
/// </remarks>
/// <param name="RequiresInitialization">
/// <c>true</c> if the ViewModel was not ready when the View attached (initial load or Reset Layout);
/// <c>false</c> if the ViewModel was already initialized (dock state change).
/// </param>
internal sealed record DiagramViewReadyInfo(bool RequiresInitialization);
