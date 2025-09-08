﻿// MIT License
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

using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.Exceptions.Assets;

/// <summary>
/// Represents errors that occur when asset integrity validation fails.
/// This exception is thrown when an asset is found to be corrupted, tampered, or does not match expected integrity checks.
/// </summary>
[Serializable]
[SuppressMessage("Major Code Smell", "S3925:\"ISerializable\" should be implemented correctly", Justification = "SerializationInfo and StreamingContext overloads are obsolete")]
public class AssetIntegrityException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssetIntegrityException"/> class.
    /// </summary>
    public AssetIntegrityException() : base()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetIntegrityException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public AssetIntegrityException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetIntegrityException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public AssetIntegrityException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
