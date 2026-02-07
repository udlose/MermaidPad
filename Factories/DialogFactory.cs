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
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.Factories;

/// <summary>
/// Factory for creating dialog windows with proper dependency injection support.
/// </summary>
/// <remarks>
/// <para>
/// This factory uses <see cref="ActivatorUtilities.CreateInstance{T}(IServiceProvider, object[])"/>
/// to create dialog instances, which automatically resolves constructor dependencies from the
/// DI container while also accepting additional parameters (such as config objects) that are
/// not registered in DI.
/// </para>
/// <para>
/// The correct dependency flow is: Factory creates Dialog => Dialog receives ViewModel via
/// constructor injection => ViewModel is configured from the config object. This ensures
/// that dialogs and their ViewModels are always created through DI, never via <c>new</c>.
/// </para>
/// </remarks>
internal interface IDialogFactory
{
    /// <summary>
    /// Creates a dialog window with all constructor dependencies resolved from DI.
    /// </summary>
    /// <typeparam name="T">The type of dialog window to create. Must inherit from <see cref="Window"/>.</typeparam>
    /// <returns>A new instance of <typeparamref name="T"/> with all dependencies injected.</returns>
    /// <remarks>
    /// Use this overload for dialogs that require no additional configuration (e.g., <c>AboutDialog</c>,
    /// <c>ExportDialog</c>) where the ViewModel is entirely self-contained.
    /// </remarks>
    T CreateDialog<T>() where T : Window;

    /// <summary>
    /// Creates a new dialog window of the specified type using the provided configuration.
    /// </summary>
    /// <typeparam name="T">The type of window to create. Must derive from Window.</typeparam>
    /// <typeparam name="TConfig">The type of the configuration object used to initialize the dialog. Cannot be null.</typeparam>
    /// <param name="configuration">An object containing the settings or parameters used to configure the dialog window.</param>
    /// <returns>A new instance of type T representing the created dialog window.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the configuration parameter is null.</exception>
    T CreateDialog<T, TConfig>([DisallowNull] TConfig configuration) where T : Window;
}

/// <summary>
/// Implementation of <see cref="IDialogFactory"/> using the DI service provider.
/// </summary>
/// <remarks>
/// This factory is registered as a singleton in the DI container because it is stateless
/// and only holds a reference to the <see cref="IServiceProvider"/>. All dialog instances
/// created by this factory are <see cref="Microsoft.Extensions.DependencyInjection.ServiceLifetime.Transient"/>:
/// a new instance is created on each call.
/// </remarks>
internal sealed class DialogFactory : IDialogFactory
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialogFactory"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for resolving dialog dependencies.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="serviceProvider"/> is null.</exception>
    [SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "Used by the DI container.")]
    public DialogFactory(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Creates a new instance of the specified dialog window type using dependency injection.
    /// </summary>
    /// <remarks>Use this method to instantiate dialog windows that require constructor injection of services.
    /// The returned window is not shown automatically; you must call Show or ShowDialog as appropriate.</remarks>
    /// <typeparam name="T">The type of window to create. Must derive from Window.</typeparam>
    /// <returns>A new instance of type T with its dependencies resolved from the service provider.</returns>
    public T CreateDialog<T>() where T : Window
    {
        return ActivatorUtilities.CreateInstance<T>(_serviceProvider);
    }

    /// <summary>
    /// Creates a new instance of a dialog window of the specified type using the provided configuration object.
    /// </summary>
    /// <remarks>This method uses dependency injection to resolve and instantiate the dialog window. The
    /// configuration object is passed to the constructor of the dialog window. Ensure that the dialog type <typeparamref name="T"/> has a
    /// constructor that accepts a parameter of type <typeparamref name="TConfig"/>.</remarks>
    /// <typeparam name="T">The type of the dialog window to create. Must derive from Window.</typeparam>
    /// <typeparam name="TConfig">The type of the configuration object to pass to the dialog window's constructor.</typeparam>
    /// <param name="configuration">The configuration object to use when constructing the dialog window. Cannot be null.</param>
    /// <returns>A new instance of type <typeparamref name="T"/>, initialized with the specified configuration.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <typeparamref name="TConfig"/> parameter is null.</exception>
    public T CreateDialog<T, TConfig>([DisallowNull] TConfig configuration) where T : Window
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return ActivatorUtilities.CreateInstance<T>(_serviceProvider, configuration);
    }
}
