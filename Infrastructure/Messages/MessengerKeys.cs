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

using CommunityToolkit.Mvvm.Messaging;

namespace MermaidPad.Infrastructure.Messages;

/// <summary>
/// Provides constant keys used for identifying messages within the application's messaging system using <see cref="IMessenger"/>
/// and <see cref="Microsoft.Extensions.DependencyInjection.FromKeyedServicesAttribute"/>.
/// </summary>
internal static class MessengerKeys
{
    /// <summary>
    /// Represents the DI key used to identify the application-wide messenger instance via FromKeyedServices.
    /// </summary>
    internal const string App = "App";

    /// <summary>
    /// Represents the DI key used to identify the document-specific messenger instance via FromKeyedServices.
    /// </summary>
    internal const string Document = "Document";
}
