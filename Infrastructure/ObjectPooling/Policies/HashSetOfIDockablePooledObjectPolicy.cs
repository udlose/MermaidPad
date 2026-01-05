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

using Dock.Model.Core;
using Microsoft.Extensions.ObjectPool;

namespace MermaidPad.Infrastructure.ObjectPooling.Policies;

/// <summary>
/// Provides a pooled object policy for managing reusable sets of dockable elements.
/// </summary>
/// <remarks>This class implements <see cref="IPooledObjectPolicy{HashSet{IDockable}}"/> to enable efficient
/// pooling of <see cref="HashSet{IDockable}"/> instances. It is intended for scenarios where dockable objects are
/// frequently collected and reused, reducing allocation overhead. Instances created by this policy have an initial
/// capacity of 16 to optimize performance when adding multiple items. The returned sets are cleared before being
/// returned to the pool, ensuring they are ready for reuse.</remarks>
public sealed class HashSetOfIDockablePooledObjectPolicy : IPooledObjectPolicy<HashSet<IDockable>>
{
    /// <summary>
    /// Creates a new empty set for storing dockable elements.
    /// </summary>
    /// <remarks>The returned set is empty and can be used to collect or manage objects implementing the <see
    /// cref="IDockable"/> interface. The initial capacity of 16 helps optimize performance when adding multiple
    /// items.</remarks>
    /// <returns>A new instance of <see cref="HashSet{IDockable}"/> with an initial capacity of 16, ready to hold dockable items.</returns>
    public HashSet<IDockable> Create() => new HashSet<IDockable>(capacity: 16);

    /// <summary>
    /// Returns a pooled <see cref="HashSet{IDockable}"/> to the pool after clearing its contents.
    /// </summary>
    /// <remarks>After calling this method, the <paramref name="pooledHashSet"/> will be empty. The instance
    /// should not be used after it is returned to the pool.</remarks>
    /// <param name="pooledHashSet">The <see cref="HashSet{IDockable}"/> instance to return to the pool. Cannot be <see langword="null"/>.</param>
    /// <returns>true if the set was successfully returned to the pool; otherwise, false.</returns>
    public bool Return(HashSet<IDockable> pooledHashSet)
    {
        ArgumentNullException.ThrowIfNull(pooledHashSet);

        pooledHashSet.Clear();
        return true;
    }
}
