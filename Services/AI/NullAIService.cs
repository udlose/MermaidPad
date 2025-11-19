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

using System;
using System.Threading;
using System.Threading.Tasks;

namespace MermaidPad.Services.AI;

/// <summary>
/// Null object pattern implementation when AI is not configured.
/// </summary>
public sealed class NullAIService : IAIService
{
    public bool IsConfigured => false;

    public Task<bool> ValidateApiKeyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task<string> GenerateDiagramFromPromptAsync(string prompt, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("AI service is not configured. Please configure your API key in Settings.");
    }

    public Task<string> ExplainDiagramAsync(string mermaidCode, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("AI service is not configured. Please configure your API key in Settings.");
    }

    public Task<string> SuggestImprovementsAsync(string mermaidCode, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("AI service is not configured. Please configure your API key in Settings.");
    }
}
