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

using CommunityToolkit.Mvvm.Messaging;
using MermaidPad.Infrastructure.Messages;
using Microsoft.Extensions.DependencyInjection;

namespace MermaidPad.ViewModels;

/// <summary>
/// Provides a base class for document view models that support both application-scoped and document-scoped messaging.
/// </summary>
/// <remarks>
/// <para>
/// This class maintains two messenger scopes:
/// <list type="bullet">
///     <item>
///         <description>
///             <seealso cref="AppMessagingViewModelBase.AppMessenger"/> - Application-scoped communication (cross-document).
///             Application-scoped subscriptions are cleaned up when the view model is deactivated via
///             <seealso cref="AppMessagingViewModelBase.OnDeactivated"/>.
///         </description>
///     </item>
///     <item>
///         <description>
///             <seealso cref="CommunityToolkit.Mvvm.ComponentModel.ObservableRecipient.Messenger"/> - Document-scoped communication.
///             This messenger instance is provided via the base <seealso cref="CommunityToolkit.Mvvm.ComponentModel.ObservableRecipient"/>
///             constructor and is accessible through the inherited <seealso cref="CommunityToolkit.Mvvm.ComponentModel.ObservableRecipient.Messenger"/>
///             property.
///         </description>
///     </item>
/// </list>
/// </para>
/// <para>
/// Usage: Use <seealso cref="CommunityToolkit.Mvvm.ComponentModel.ObservableRecipient.Messenger"/> for document-scoped messages (for example,
/// notifications within a single document). Use <seealso cref="AppMessagingViewModelBase.AppMessenger"/> for application-scoped messages (for
/// example, cross-document coordination).
/// </para>
/// </remarks>
internal abstract class DocumentMessagingViewModelBase : AppMessagingViewModelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentMessagingViewModelBase"/> class with the specified application-scoped and
    /// document-scoped messengers.
    /// </summary>
    /// <remarks>
    /// Both messenger instances are expected to be resolved from keyed services and must remain valid for the lifetime of the view model.
    /// </remarks>
    /// <param name="appMessenger">
    /// The messenger for application-scoped communication, keyed by <seealso cref="MessengerKeys.App"/>.
    /// </param>
    /// <param name="documentMessenger">
    /// The messenger for document-scoped communication, keyed by <seealso cref="MessengerKeys.Document"/>.
    /// </param>
    private protected DocumentMessagingViewModelBase(
        [FromKeyedServices(MessengerKeys.App)] IMessenger appMessenger,
        [FromKeyedServices(MessengerKeys.Document)] IMessenger documentMessenger)
        : base(appMessenger, documentMessenger)
    {
    }
}
