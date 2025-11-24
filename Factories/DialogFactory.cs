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

namespace MermaidPad.Factories;

/// <summary>
/// Factory for creating dialog windows with their ViewModels properly injected via dependency injection.
/// </summary>
/// <remarks>
/// This factory creates complete dialog instances (Window + ViewModel together) using the service provider.
/// Dialog ViewModels are registered as Transient services, so each dialog gets a fresh ViewModel instance.
/// The factory uses ActivatorUtilities to automatically resolve ViewModel constructor dependencies.
/// </remarks>
public interface IDialogFactory
{
    /// <summary>
    /// Creates a dialog window with its ViewModel injected via dependency injection.
    /// </summary>
    /// <typeparam name="T">The dialog window type to create.</typeparam>
    /// <returns>A fully initialized dialog window with its ViewModel set.</returns>
    /// <remarks>
    /// <para>
    /// This method uses ActivatorUtilities to create the dialog, which automatically resolves
    /// constructor parameters from the DI container. For example, calling:
    /// </para>
    /// <para>
    ///     <![CDATA[CreateDialog<SettingsDialog>()]]>
    /// </para>
    /// <para>
    /// will automatically create a SettingsDialogViewModel (registered as Transient) and pass it to
    /// the SettingsDialog constructor.
    /// </para>
    /// </remarks>
    T CreateDialog<T>() where T : Window;
}

/// <summary>
/// Provides a factory for creating dialog window instances using dependency injection.
/// </summary>
/// <remarks>Use this class to instantiate dialog windows that require constructor injection of services. The
/// factory resolves dependencies from the provided service container, ensuring dialogs are created with all required
/// services. This class is sealed and cannot be inherited.</remarks>
public sealed class DialogFactory : IDialogFactory
{
    private readonly IServiceProvider _serviceProvider;

    public DialogFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public T CreateDialog<T>() where T : Window
    {
        return ActivatorUtilities.CreateInstance<T>(_serviceProvider);
    }
}
