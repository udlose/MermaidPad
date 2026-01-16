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

using CommunityToolkit.Mvvm.Messaging.Messages;

namespace MermaidPad.Infrastructure.Messages;

/// <summary>
/// Message sent when the DiagramView has attached to the visual tree and wired up its
/// <see cref="ViewModels.UserControls.DiagramViewModel.InitializeActionAsync"/> delegate.
/// </summary>
/// <remarks>
/// <para>
/// This message solves the timing issue where <see cref="Views.MainWindow.OnOpenedAsync"/> or
/// <see cref="ViewModels.MainWindowViewModel.ResetLayoutAsync"/> attempts to initialize the diagram
/// before the DiagramView has attached. This happens when the diagram panel starts in a pinned
/// (auto-hide) state or in a floating window.
/// </para>
/// <para>
/// <b>Flow:</b>
/// <list type="number">
///     <item><description>DiagramView attaches to visual tree (OnAttachedToVisualTree or OnDataContextChanged)</description></item>
///     <item><description>DiagramView.SetupViewModelBindings() wires up InitializeActionAsync delegate</description></item>
///     <item><description>DiagramView sends DiagramViewReadyMessage with RequiresInitialization flag</description></item>
///     <item><description>MainWindowViewModel.Receive() checks if initialization is pending</description></item>
///     <item><description>If pending and RequiresInitialization=true, MainWindowViewModel triggers InitializeDiagramAsync()</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Architecture:</b> This message uses the document-scoped messenger (keyed by <see cref="MessengerKeys.Document"/>)
/// rather than the application-wide messenger. This design prepares for MDI migration where each document
/// has its own messenger instance.
/// </para>
/// </remarks>
internal sealed class DiagramViewReadyMessage : ValueChangedMessage<DiagramViewReadyInfo>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DiagramViewReadyMessage"/> class.
    /// </summary>
    /// <param name="value">The ready state information indicating whether initialization is required.</param>
    public DiagramViewReadyMessage(DiagramViewReadyInfo value)
        : base(value)
    {
    }
}
