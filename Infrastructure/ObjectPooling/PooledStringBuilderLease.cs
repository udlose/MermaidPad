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
/// This type is an <see cref="IDisposable"/> lease/handle for a pooled <see cref="StringBuilder"/> instance,
/// ensuring the builder is returned to the pool when disposed.
/// </summary>
/// <remarks>
/// <para>
/// Instances of this type must be disposed by the caller, as indicated by the <see cref="MustDisposeResourceAttribute"/>,
/// to return the leased <see cref="StringBuilder"/> to the originating pool.
/// </para>
/// <para>
/// A <see cref="PooledStringBuilderLease"/> is typically obtained from an object pool and should be
/// disposed when no longer needed to return the <see cref="StringBuilder"/> to the pool. Accessing the <see
/// cref="Builder"/> property after disposal will throw an <see cref="ObjectDisposedException"/>. This type is not
/// thread-safe.
/// </para>
/// </remarks>
[MustDisposeResource]
public sealed class PooledStringBuilderLease : IDisposable
{
    private readonly ObjectPool<StringBuilder> _pool;
    private readonly StringBuilder _builder;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the PooledStringBuilderLease class, associating it with the specified object pool
    /// and StringBuilder instance.
    /// </summary>
    /// <remarks>This constructor is intended for internal use when managing pooled StringBuilder resources.
    /// The lease ensures that the StringBuilder is returned to the pool when disposed.</remarks>
    /// <param name="pool">The object pool that manages the lifecycle of the leased StringBuilder instance. Cannot be null.</param>
    /// <param name="builder">The StringBuilder instance to lease from the pool. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pool"/> is null.</exception>
    internal PooledStringBuilderLease(ObjectPool<StringBuilder> pool, StringBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(builder);

        _pool = pool;
        _builder = builder;
        _isDisposed = false;
    }

    /// <summary>
    /// Gets the underlying <see cref="StringBuilder"/> instance associated with this lease.
    /// </summary>
    /// <remarks>Accessing this property after the lease has been disposed will throw a
    /// <see cref="ObjectDisposedException"/>. Modifications to the returned <see cref="StringBuilder"/> affect the leased
    /// buffer until the lease is released.</remarks>
    internal StringBuilder Builder
    {
        get
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            return _builder;
        }
    }

    /// <summary>
    /// Releases all resources used by the instance and returns any pooled objects to their respective pools.
    /// </summary>
    /// <remarks>After calling this method, the instance should not be used. Multiple calls to this method
    /// have no effect.</remarks>
    public void Dispose()
    {
        if (!_isDisposed)
        {
            _pool.Return(_builder);
            _isDisposed = true;
        }
    }
}
