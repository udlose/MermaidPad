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
using MermaidPad.Infrastructure.ObjectPooling;
using System.Text;

namespace MermaidPad.Services;

/// <summary>
/// Provides a base class for services.
/// </summary>
internal abstract class ServiceBase
{
    private protected IPooledStringBuilderLeaseFactory PooledStringBuilderLeaseFactory { get; }

    /// <summary>
    /// Initializes a new instance of the ServiceBase class with the specified pooled string builder lease factory.
    /// </summary>
    /// <param name="pooledStringBuilderLeaseFactory">The factory used to obtain pooled string builder leases for
    /// efficient string manipulation. Cannot be null.</param>
    protected ServiceBase(IPooledStringBuilderLeaseFactory pooledStringBuilderLeaseFactory)
    {
        PooledStringBuilderLeaseFactory = pooledStringBuilderLeaseFactory;
    }

    /// <summary>
    /// Rents a pooled <see cref="StringBuilder"/> instance wrapped in a <see cref="PooledStringBuilderLease"/>.
    /// </summary>
    /// <remarks>
    /// <para>Use the returned lease within a using statement to ensure the <see cref="StringBuilder"/> is returned to
    /// the pool. The lease should not be used after it has been disposed.
    /// </para>
    /// <para>
    /// The caller:
    ///     1. Must dispose of the returned <see cref="PooledStringBuilderLease"/> to return the <see cref="StringBuilder"/> to the pool.
    ///     2. Must avoid holding onto the <see cref="StringBuilder"/> reference beyond the scope of the lease.
    ///     3. Must use the return value as indicated by the <see cref="MustUseReturnValueAttribute"/>.
    /// </para>
    /// </remarks>
    /// <returns>A <see cref="PooledStringBuilderLease"/> that provides access to a pooled <see cref="StringBuilder"/> and ensures its proper
    /// return to the pool when disposed.</returns>
    [MustDisposeResource]
    [MustUseReturnValue]
    private protected PooledStringBuilderLease RentPooledStringBuilderLease() => PooledStringBuilderLeaseFactory.Rent();

    /// <summary>
    /// Rents a pooled <see cref="StringBuilder"/> instance wrapped in a <see cref="PooledStringBuilderLease"/>.
    /// </summary>
    /// <param name="minimumCapacity">The minimum capacity the rented <see cref="StringBuilder"/> should have.</param>
    /// <remarks>
    /// <para>Use the returned lease within a using statement to ensure the <see cref="StringBuilder"/> is returned to
    /// the pool. The lease should not be used after it has been disposed.
    /// </para>
    /// <para>
    /// The caller:
    ///     1. Must dispose of the returned <see cref="PooledStringBuilderLease"/> to return the <see cref="StringBuilder"/> to the pool.
    ///     2. Must avoid holding onto the <see cref="StringBuilder"/> reference beyond the scope of the lease.
    ///     3. Must use the return value as indicated by the <see cref="MustUseReturnValueAttribute"/>.
    /// </para>
    /// </remarks>
    /// <returns>A <see cref="PooledStringBuilderLease"/> that provides access to a pooled <see cref="StringBuilder"/> and ensures its proper
    /// return to the pool when disposed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="minimumCapacity"/> is less than or equal to zero.</exception>
    [MustDisposeResource]
    [MustUseReturnValue]
    private protected PooledStringBuilderLease RentPooledStringBuilderLease(int minimumCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumCapacity);

        return PooledStringBuilderLeaseFactory.Rent(minimumCapacity);
    }
}
