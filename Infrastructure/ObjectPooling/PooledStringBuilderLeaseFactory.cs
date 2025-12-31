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
/// Provides a factory for leasing pooled <see cref="StringBuilder"/> instances using an object pool.
/// </summary>
/// <remarks>This class is intended for internal use to efficiently manage the allocation and reuse of <see
/// cref="StringBuilder"/> objects. Leased instances are cleared internally by the
/// underlying <see cref="StringBuilderPooledObjectPolicy"/> before being provided to the caller to
/// ensure a consistent initial state. The factory returns <see cref="PooledStringBuilderLease"/> objects,
/// which should be disposed to return the <see cref="StringBuilder"/> to the pool.</remarks>
internal sealed class PooledStringBuilderLeaseFactory : IPooledStringBuilderLeaseFactory
{
    private readonly ObjectPool<StringBuilder> _pool;

    /// <summary>
    /// Initializes a new instance of the <see cref="PooledStringBuilderLeaseFactory"/> class using the specified object pool.
    /// </summary>
    /// <param name="pool">The object pool that manages <see cref="StringBuilder"/> instances for leasing.</param>
    internal PooledStringBuilderLeaseFactory(ObjectPool<StringBuilder> pool)
    {
        _pool = pool;
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
    ///     3. Must use the return value and either ignore or discard it as indicated by the <see cref="MustUseReturnValueAttribute"/>.
    /// </para>
    /// </remarks>
    /// <returns>A <see cref="PooledStringBuilderLease"/> that provides exclusive access to a pooled <see cref="StringBuilder"/>. The lease
    /// must be disposed to return the <see cref="StringBuilder"/> to the pool.</returns>
    [MustDisposeResource]
    [MustUseReturnValue]
    public PooledStringBuilderLease Rent()
    {
        StringBuilder builder = _pool.Get();
        return new PooledStringBuilderLease(_pool, builder);
    }
}
