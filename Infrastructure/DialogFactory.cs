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
using MermaidPad.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MermaidPad.Infrastructure;

/// <summary>
/// Factory for creating dialogs with proper dependency injection
/// </summary>
internal interface IDialogFactory
{
    /// <summary>
    /// Creates a dialog window with DI support
    /// </summary>
    T CreateDialog<T>() where T : Window;

    /// <summary>
    /// Creates a view model with DI support
    /// </summary>
    TViewModel CreateViewModel<TViewModel>() where TViewModel : ViewModelBase;
}

/// <summary>
/// Implementation of dialog factory using the service provider
/// </summary>
internal sealed class DialogFactory : IDialogFactory
{
    private readonly IServiceProvider _serviceProvider;

    public DialogFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public T CreateDialog<T>() where T : Window
    {
        return ActivatorUtilities.CreateInstance<T>(_serviceProvider);
    }

    public TViewModel CreateViewModel<TViewModel>() where TViewModel : ViewModelBase
    {
        return _serviceProvider.GetRequiredService<TViewModel>();
    }
}
