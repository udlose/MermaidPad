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

using Microsoft.Extensions.DependencyInjection;

namespace MermaidPad.Factories;

/// <summary>
/// Generic factory for creating ViewModel instances with full dependency injection support.
/// </summary>
/// <remarks>
/// <para>
/// This factory uses <see cref="ActivatorUtilities.CreateInstance{T}(IServiceProvider, object[])"/>
/// to create instances, which provides several benefits:
/// </para>
/// <list type="bullet">
///     <item>
///         <description>
///             Automatically resolves constructor dependencies from the DI container.
///         </description>
///     </item>
///     <item>
///         <description>
///             Allows passing additional parameters that aren't registered in DI.
///         </description>
///     </item>
///     <item>
///         <description>
///             Creates a new instance on each call (explicit transient behavior).
///         </description>
///     </item>
/// </list>
/// </remarks>
public sealed class ViewModelFactory : IViewModelFactory
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ViewModelFactory"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for resolving dependencies.</param>
    public ViewModelFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public T Create<T>() where T : class
    {
        return ActivatorUtilities.CreateInstance<T>(_serviceProvider);
    }

    /// <inheritdoc/>
    public T Create<T>(params object[] parameters) where T : class
    {
        return ActivatorUtilities.CreateInstance<T>(_serviceProvider, parameters);
    }
}
