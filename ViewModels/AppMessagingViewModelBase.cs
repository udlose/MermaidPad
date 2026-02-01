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

namespace MermaidPad.ViewModels;

/// <summary>
/// Provides a base class for view models that participate in application-scoped messaging.
/// </summary>
/// <remarks>
/// <para>
/// This base class is intentionally separate from <seealso cref="ViewModelBase"/> so that application-scoped messaging is opt-in
/// and messaging policy is not applied to all view models.
/// </para>
/// <para>
/// On deactivation, this class unregisters the recipient from the application-scoped messenger by calling
/// <seealso cref="IMessenger.UnregisterAll(object)"/> with <see langword="this"/> as the recipient.
/// </para>
/// </remarks>
internal abstract class AppMessagingViewModelBase : ViewModelBase
{
    /// <summary>
    /// The global application messenger for cross-document communication using the CommunityToolkit.Mvvm messaging system (pub/sub).
    /// </summary>
    /// <remarks>
    /// Because the global messenger is not the same instance as <see cref="CommunityToolkit.Mvvm.ComponentModel.ObservableRecipient.Messenger" />,
    /// it needs manual unregistration (active-only behavior).
    /// </remarks>
    private protected IMessenger AppMessenger { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AppMessagingViewModelBase"/> class with the specified application-scoped messenger.
    /// </summary>
    /// <param name="appMessenger">The application-scoped messenger instance. Cannot be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="appMessenger"/> is <see langword="null"/>.</exception>
    protected AppMessagingViewModelBase(IMessenger appMessenger)
        : base()
    {
        ArgumentNullException.ThrowIfNull(appMessenger);
        AppMessenger = appMessenger;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AppMessagingViewModelBase"/> class with the specified application-scoped and
    /// document-scoped messengers.
    /// </summary>
    /// <param name="appMessenger">The application-scoped messenger instance. Cannot be <see langword="null"/>.</param>
    /// <param name="documentMessenger">The document-scoped messenger instance. Cannot be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="appMessenger"/> or <paramref name="documentMessenger"/> is <see langword="null"/>.
    /// </exception>
    protected AppMessagingViewModelBase(IMessenger appMessenger, IMessenger documentMessenger)
        : base(documentMessenger)
    {
        ArgumentNullException.ThrowIfNull(appMessenger);
        ArgumentNullException.ThrowIfNull(documentMessenger);

        AppMessenger = appMessenger;
    }

    /// <summary>
    /// Handles cleanup when the recipient is deactivated, ensuring that all application-scoped message subscriptions associated with this
    /// instance are unregistered.
    /// </summary>
    /// <remarks>
    /// In addition to the default unregistration performed by <seealso cref="CommunityToolkit.Mvvm.ComponentModel.ObservableRecipient"/>,
    /// this method explicitly removes any application-scoped message subscriptions for this recipient by calling
    /// <seealso cref="IMessenger.UnregisterAll(object)"/> on <seealso cref="AppMessenger"/> (passing <see langword="this"/> as the recipient).
    /// </remarks>
    protected override void OnDeactivated()
    {
        try
        {
            // ObservableRecipient automatically unregisters from this.Messenger by default
            // Also detach global subscriptions explicitly
            AppMessenger.UnregisterAll(this);
        }
        finally
        {
            // Ensure base class cleanup occurs even if additional cleanup fails
            base.OnDeactivated();
        }
    }
}
