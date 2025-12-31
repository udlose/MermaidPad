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

using JetBrains.Annotations;
using Microsoft.Extensions.ObjectPool;
using System.Text;

namespace MermaidPad.Infrastructure.ObjectPooling;

/// <summary>
/// Provides a factory for leasing pooled <see cref="StringBuilder"/> instances using multiple capacity buckets.
/// </summary>
/// <remarks>
/// <para>
/// This implementation routes rent requests to one of several underlying pools (buckets) based on the requested
/// minimum capacity, to avoid "large builder contamination" of small-builder call sites.
/// </para>
/// <para>
/// <see cref="StringBuilder"/> instances are cleared before being provided to the caller to ensure a consistent initial state.
/// </para>
/// </remarks>
internal sealed class PooledStringBuilderBucketedLeaseFactory : IPooledStringBuilderLeaseFactory
{
    private const int DefaultMinimumCapacity = 256;

    private readonly ObjectPool<StringBuilder> _pool256;
    private readonly ObjectPool<StringBuilder> _pool1024;
    private readonly ObjectPool<StringBuilder> _pool4096;
    private readonly ObjectPool<StringBuilder> _pool16384;

    /// <summary>
    /// Initializes a new instance of the PooledStringBuilderBucketedLeaseFactory class using the specified
    /// StringBuilder object pools for different buffer sizes.
    /// </summary>
    /// <param name="pool256">The object pool used for StringBuilder instances with a buffer size of 256 characters. Cannot be null.</param>
    /// <param name="pool1024">The object pool used for StringBuilder instances with a buffer size of 1,024 characters. Cannot be null.</param>
    /// <param name="pool4096">The object pool used for StringBuilder instances with a buffer size of 4,096 characters. Cannot be null.</param>
    /// <param name="pool16384">The object pool used for StringBuilder instances with a buffer size of 16,384 characters. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when any of the provided object pools are null.</exception>
    internal PooledStringBuilderBucketedLeaseFactory(
        ObjectPool<StringBuilder> pool256,
        ObjectPool<StringBuilder> pool1024,
        ObjectPool<StringBuilder> pool4096,
        ObjectPool<StringBuilder> pool16384)
    {
        ArgumentNullException.ThrowIfNull(pool256);
        ArgumentNullException.ThrowIfNull(pool1024);
        ArgumentNullException.ThrowIfNull(pool4096);
        ArgumentNullException.ThrowIfNull(pool16384);

        _pool256 = pool256;
        _pool1024 = pool1024;
        _pool4096 = pool4096;
        _pool16384 = pool16384;
    }

    /// <summary>
    /// Rents a pooled <see cref="StringBuilder"/> instance wrapped in a <see cref="PooledStringBuilderLease"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The caller:
    ///     1. Must dispose of the returned <see cref="PooledStringBuilderLease"/> to return the <see cref="StringBuilder"/> to the pool
    ///     as indicated by the <see cref="MustDisposeResourceAttribute"/>.
    ///     2. Must avoid holding onto the <see cref="StringBuilder"/> reference beyond the scope of the lease.
    ///     3. Must use the return value as indicated by the <see cref="MustUseReturnValueAttribute"/>.
    /// </para>
    /// </remarks>
    /// <returns>A <see cref="PooledStringBuilderLease"/> that provides exclusive access to a pooled <see cref="StringBuilder"/>. The lease
    /// must be disposed to return the <see cref="StringBuilder"/> to the pool.</returns>
    [MustDisposeResource]
    [MustUseReturnValue]
    public PooledStringBuilderLease Rent() => Rent(DefaultMinimumCapacity);

    /// <summary>
    /// Rents a pooled <see cref="StringBuilder"/> instance wrapped in a <see cref="PooledStringBuilderLease"/>.
    /// </summary>
    /// <param name="minimumCapacity">The minimum capacity required for the rented <see cref="StringBuilder"/>.</param>
    /// <remarks>
    /// <para>
    /// The caller:
    ///     1. Must dispose of the returned <see cref="PooledStringBuilderLease"/> to return the <see cref="StringBuilder"/> to the pool
    ///     as indicated by the <see cref="MustDisposeResourceAttribute"/>.
    ///     2. Must avoid holding onto the <see cref="StringBuilder"/> reference beyond the scope of the lease.
    ///     3. Must use the return value as indicated by the <see cref="MustUseReturnValueAttribute"/>.
    /// </para>
    /// </remarks>
    /// <returns>A <see cref="PooledStringBuilderLease"/> that provides exclusive access to a pooled <see cref="StringBuilder"/>. The lease
    /// must be disposed to return the <see cref="StringBuilder"/> to the pool.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="minimumCapacity"/> is less than or equal to zero.</exception>
    [MustDisposeResource]
    [MustUseReturnValue]
    public PooledStringBuilderLease Rent(int minimumCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumCapacity);

        ObjectPool<StringBuilder> selectedPool = SelectPool(minimumCapacity);
        StringBuilder builder = selectedPool.Get();

        // This is already done internally by the backing StringBuilderPooledObjectPolicy, but we do it here to be safe.
        // Debugging ObjectPool issues is a beyotch!
        builder.Clear();

        if (minimumCapacity > builder.Capacity)
        {
            builder.EnsureCapacity(minimumCapacity);
        }

        return new PooledStringBuilderLease(selectedPool, builder);
    }

    /// <summary>
    /// Selects an object pool for <see cref="StringBuilder"/> instances based on the specified minimum capacity.
    /// </summary>
    /// <param name="minimumCapacity">The minimum required capacity, in characters, for the <see cref="StringBuilder"/> instances to be obtained from
    /// the pool. Must be a non-negative value.</param>
    /// <returns>An <see cref="ObjectPool{StringBuilder}"/> that provides <see cref="StringBuilder"/> instances with at least the
    /// specified minimum capacity.</returns>
    private ObjectPool<StringBuilder> SelectPool(int minimumCapacity) =>
        minimumCapacity switch
        {
            <= 256 => _pool256,
            <= 1_024 => _pool1024,
            <= 4_096 => _pool4096,
            _ => _pool16384
        };
}
