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

namespace MermaidPad.Factories;

/// <summary>
/// Generic factory interface for creating ViewModel instances with dependency injection support.
/// </summary>
/// <remarks>
/// <para>
/// This factory pattern is used instead of direct transient DI registration because:
/// </para>
/// <list type="number">
///     <item>
///         <description>
///             Makes instance creation explicit - callers know they're getting a new instance,
///             preventing confusion about ViewModel lifecycle.
///         </description>
///     </item>
///     <item>
///         <description>
///             Supports scenarios where each document/editor/tab needs its own ViewModel instance
///             with independent state (e.g., MDI applications).
///         </description>
///     </item>
///     <item>
///         <description>
///             Avalonia doesn't have built-in scoped DI like ASP.NET Core, so a factory
///             provides explicit control over instance creation and lifecycle.
///         </description>
///     </item>
///     <item>
///         <description>
///             Allows passing additional constructor parameters at creation time if needed
///             via the <see cref="Create(object[])"/> overload.
///         </description>
///     </item>
/// </list>
/// <para>
/// Usage example:
/// <code>
/// // In consuming class
/// public MyClass(IViewModelFactory viewModelFactory)
/// {
///     _viewModelTypeX = viewModelFactory.Create{TypeX}();
///     _viewModelTypeY = viewModelFactory.Create{TypeY}();
/// }
/// </code>
/// </para>
/// </remarks>
public interface IViewModelFactory
{
    /// <summary>
    /// Creates a new instance of <typeparamref name="T"/> with all dependencies resolved from DI.
    /// </summary>
    /// <returns>A new instance of <typeparamref name="T"/>.</returns>
    T Create<T>() where T : class;

    /// <summary>
    /// Creates a new instance of <typeparamref name="T"/> with the specified additional constructor parameters.
    /// </summary>
    /// <param name="parameters">
    /// Additional parameters to pass to the constructor. These are matched by type to constructor parameters
    /// that cannot be resolved from DI. DI-resolvable dependencies are still injected automatically.
    /// </param>
    /// <returns>A new instance of <typeparamref name="T"/>.</returns>
    /// <remarks>
    /// Use this overload when the ViewModel constructor requires parameters that are not registered in DI,
    /// such as runtime configuration or parent references.
    /// </remarks>
    T Create<T>(params object[] parameters) where T : class;
}
