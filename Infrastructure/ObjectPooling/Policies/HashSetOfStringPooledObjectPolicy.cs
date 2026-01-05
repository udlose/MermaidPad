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

using Microsoft.Extensions.ObjectPool;

namespace MermaidPad.Infrastructure.ObjectPooling.Policies;

/// <summary>
/// Provides a pooling policy for <see cref="HashSet{string}"/> instances using ordinal string comparison.
/// </summary>
/// <remarks>This policy is intended for use with object pools that manage <see cref="HashSet{string}"/> objects.
/// Each created hash set uses <see cref="StringComparer.Ordinal"/> for string comparisons. When a hash set is returned
/// to the pool, it is cleared to remove all elements, ensuring it is ready for reuse.</remarks>
public sealed class HashSetOfStringPooledObjectPolicy : IPooledObjectPolicy<HashSet<string>>
{
    /// <summary>
    /// Creates a new empty set of strings that uses ordinal comparison for string values.
    /// </summary>
    /// <remarks>The returned set uses <see cref="StringComparer.Ordinal"/> for all string comparisons, which
    /// is case-sensitive and culture-insensitive. This is suitable for scenarios where exact byte-wise string matching
    /// is required.</remarks>
    /// <returns>A new <see cref="HashSet{string}"/> instance that compares strings using ordinal rules.</returns>
    public HashSet<string> Create() => new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Returns a pooled <see cref="HashSet{string}"/> to the pool after clearing its contents.
    /// </summary>
    /// <remarks>After calling this method, the <paramref name="pooledHashSet"/> will be empty. The instance
    /// should not be used after it is returned to the pool.</remarks>
    /// <param name="pooledHashSet">The <see cref="HashSet{string}"/> instance to return to the pool. Cannot be <see langword="null"/>.</param>
    /// <returns>true if the set was successfully returned to the pool; otherwise, false.</returns>
    public bool Return(HashSet<string> pooledHashSet)
    {
        ArgumentNullException.ThrowIfNull(pooledHashSet);

        pooledHashSet.Clear();
        return true;
    }
}
