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

namespace MermaidPad.Threading;

/// <summary>
/// Provides atomic operations for reading and incrementing version numbers represented as 64-bit integers.
/// </summary>
/// <remarks>This class offers thread-safe methods for manipulating version values in concurrent scenarios. All
/// members are static and intended for use with shared version fields that may be accessed by multiple threads
/// simultaneously.</remarks>
internal static class AtomicVersion
{
    /// <summary>
    /// Reads a 64-bit value from the specified variable in a thread-safe manner.
    /// </summary>
    /// <remarks>This method ensures that the read operation is performed atomically, making it safe to use in
    /// multithreaded scenarios where the value may be updated concurrently.</remarks>
    /// <param name="version">A reference to the 64-bit integer to read. The value is read atomically.</param>
    /// <returns>The current value of the specified 64-bit integer.</returns>
    internal static long Read(ref long version) => Interlocked.Read(ref version);

    /// <summary>
    /// Atomically increments the specified 64-bit integer value and returns the incremented value.
    /// </summary>
    /// <remarks>This method is thread-safe and can be used to increment a shared counter across multiple
    /// threads without additional synchronization.</remarks>
    /// <param name="version">A reference to the 64-bit integer to increment. The value is
    /// incremented in a thread-safe manner.</param>
    /// <returns>The incremented value of the specified 64-bit integer.</returns>
    internal static long Increment(ref long version) => Interlocked.Increment(ref version);
}
