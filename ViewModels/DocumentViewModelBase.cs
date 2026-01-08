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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using MermaidPad.Infrastructure.Messages;
using Microsoft.Extensions.DependencyInjection;

namespace MermaidPad.ViewModels;

/// <summary>
/// Provides a base class for document view models that supports both application-wide and document-specific messaging,
/// enabling coordinated communication across documents within the application.
/// </summary>
/// <remarks>
/// <para>
/// This class maintains two messenger scopes:
/// </para>
/// <list type="bullet">
///     <item>
///         <description>
///             <see cref="AppMessenger"/> - Application-wide communication (cross-document).
///             Requires manual unregistration in <see cref="OnDeactivated"/>.
///         </description>
///     </item>
///     <item>
///         <description>
///             <see cref="ObservableRecipient.Messenger"/> (inherited) - Document-specific communication.
///             Automatically unregistered by <see cref="ObservableRecipient"/> on deactivation.
///         </description>
///     </item>
/// </list>
/// <para>
/// This class extends ObservableRecipient to facilitate message handling for document view models. It
/// maintains separate messenger instances for global (application-wide) and document-level communication, allowing
/// derived classes to participate in both scopes. Manual unregistration of global message subscriptions is required to
/// prevent memory leaks when the view model is deactivated.
/// </para>
/// <para>
/// <b>Usage:</b> Use <c>Messenger</c> for document-scoped messages (e.g., text changes within
/// a single document). Use <c>AppMessenger</c> for cross-document coordination (e.g.,
/// active document changed, global settings updates).
/// </para>
/// </remarks>
internal abstract class DocumentViewModelBase : ObservableRecipient
{
    /// <summary>
    /// Initializes a new instance of the DocumentViewModelBase class with the specified application and document
    /// messengers.
    /// </summary>
    /// <remarks>
    /// Both messenger instances are expected to be resolved from keyed services and must remain
    /// valid for the lifetime of the view model.
    /// </remarks>
    /// <param name="appMessenger">
    /// The messenger for application-wide (cross-document) communication.
    /// Stored in <see cref="AppMessenger"/>.
    /// </param>
    /// <param name="documentMessenger">
    /// The messenger for document-specific communication.
    /// Passed to base class and accessible via inherited <see cref="ObservableRecipient.Messenger"/>.
    /// </param>
    private protected DocumentViewModelBase(
        [FromKeyedServices(MessengerKeys.App)] IMessenger appMessenger,
        [FromKeyedServices(MessengerKeys.Document)] IMessenger documentMessenger)
        : base(documentMessenger)
    {
        AppMessenger = appMessenger;
    }

    /// <summary>
    /// The global application messenger for cross-document communication.
    /// </summary>
    /// <remarks>
    /// Because the global messenger is not the same instance as <see cref="ObservableRecipient.Messenger" />,
    /// it needs manual unregistration (active-only behavior).
    /// </remarks>
    private protected IMessenger AppMessenger { get; }

    /// <summary>
    /// Handles cleanup when the recipient is deactivated, ensuring that all message subscriptions associated with this
    /// instance are unregistered.
    /// </summary>
    /// <remarks>In addition to the default unregistration performed by the base class, this method explicitly
    /// removes any global message subscriptions for this recipient by calling AppMessenger.UnregisterAll(this). This
    /// helps prevent memory leaks and unintended message delivery after deactivation.</remarks>
    protected override void OnDeactivated()
    {
        try
        {
            // ObservableRecipient automatically unregisters from this.Messenger by default
            // Also detach global subscriptions explicitly:
            AppMessenger.UnregisterAll(this);
        }
        finally
        {
            // Ensure base class cleanup occurs even if additional cleanup fails
            base.OnDeactivated();
        }
    }
}
