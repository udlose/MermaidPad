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

using MermaidPad.ObjectPoolPolicies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.ObjectPool;
using System.Text;

namespace MermaidPad.Infrastructure.ObjectPooling;

/// <summary>
/// Provides registration methods for configuring object pooling services, such as StringBuilder pooling, within a
/// dependency injection container.
/// </summary>
/// <remarks>This class is intended for internal use to centralize object pool service registrations. It is not
/// intended to be used directly by application code.</remarks>
internal static class ObjectPoolRegistrations
{
    /// <summary>
    /// Adds object pooling services to the specified <see cref="IServiceCollection"/>. Registers default object pool
    /// providers and policies for use within the application.
    /// </summary>
    /// <remarks>This method registers a default <see cref="ObjectPoolProvider"/> and configures object pools
    /// for commonly used types such as <see cref="HashSet{String}"/> and <see cref="StringBuilder"/>. Call this method
    /// during service configuration to enable efficient object reuse and reduce memory allocations.</remarks>
    /// <param name="services">The <see cref="IServiceCollection"/> to which the object pooling services are added. Cannot be null.</param>
    internal static void AddObjectPooling(this IServiceCollection services)
    {
        // Register the default object pool provider first if one is not already registered
        services.TryAddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
        services.TryAddSingleton<ObjectPool<HashSet<string>>>(static sp =>
        {
            ObjectPoolProvider provider = sp.GetRequiredService<ObjectPoolProvider>();
            HashSetPooledObjectPolicy policy = new HashSetPooledObjectPolicy();
            return provider.Create(policy);
        });

        services.TryAddSingleton<IPooledStringBuilderLeaseFactory>(CreatePooledStringBuilderLeaseFactory);
    }

    /// <summary>
    /// Creates an object pool for <see cref="StringBuilder"/> instances using the specified service provider.
    /// </summary>
    /// <remarks>The returned <see cref="IPooledStringBuilderLeaseFactory"/> is configured to provide a pool with
    /// an initial capacity of 256 characters and a maximum retained capacity of 16,384 characters per
    /// <see cref="StringBuilder"/> instance. Using a pool can improve performance by reducing allocations when working with temporary strings.</remarks>
    /// <param name="serviceProvider">The service provider used to retrieve the <see cref="ObjectPoolProvider"/> required to create the pool.
    /// Cannot be null.</param>
    /// <returns>An <see cref="IPooledStringBuilderLeaseFactory"/> that provides access to pooled <see cref="StringBuilder"/> instances.</returns>
    private static IPooledStringBuilderLeaseFactory CreatePooledStringBuilderLeaseFactory(IServiceProvider serviceProvider)
    {
        ObjectPoolProvider poolProvider = serviceProvider.GetRequiredService<ObjectPoolProvider>();
        StringBuilderPooledObjectPolicy policy = new StringBuilderPooledObjectPolicy
        {
            InitialCapacity = 256,
            MaximumRetainedCapacity = 16 * 1_024
        };

        ObjectPool<StringBuilder> pool = poolProvider.Create(policy);
        return new PooledStringBuilderLeaseFactory(pool);
    }
}
