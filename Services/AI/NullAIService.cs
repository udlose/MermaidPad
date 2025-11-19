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

namespace MermaidPad.Services.AI;

/// <summary>
/// Null object pattern implementation when AI is not configured.
/// </summary>
/// <remarks>
/// This implementation of <see cref="IAIService"/> is used when no AI provider or API key
/// has been configured. It provides safe, non-operational behavior: validation returns
/// <see langword="false"/> and any generation/explanation/improvement methods throw an
/// <see cref="InvalidOperationException"/> indicating the service must be configured.
/// </remarks>
public sealed class NullAIService : IAIService
{
    /// <summary>
    /// Gets a value indicating whether the component has been configured with the required settings and is ready for use.
    /// </summary>
    /// <value>
    /// Always <see langword="false"/> for the null implementation because no API key or provider is configured.
    /// </value>
    public bool IsConfigured => false;

    /// <summary>
    /// Validates the API key by making a test request.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>
    /// A task that completes with <see langword="false"/> for the null implementation.
    /// </returns>
    public Task<bool> ValidateApiKeyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    /// <summary>
    /// Generates Mermaid diagram code from a natural language description.
    /// </summary>
    /// <param name="prompt">Natural language description of the desired diagram.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task that would return generated Mermaid syntax when configured.</returns>
    /// <exception cref="InvalidOperationException">Thrown always by the null implementation to indicate the AI service is not configured.</exception>
    public Task<string> GenerateDiagramFromPromptAsync(string prompt, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("AI service is not configured. Please configure your API key in Settings.");
    }

    /// <summary>
    /// Explains what a Mermaid diagram represents in natural language.
    /// </summary>
    /// <param name="mermaidCode">The Mermaid diagram code to explain.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task that would return a human-readable explanation when configured.</returns>
    /// <exception cref="InvalidOperationException">Thrown always by the null implementation to indicate the AI service is not configured.</exception>
    public Task<string> ExplainDiagramAsync(string mermaidCode, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("AI service is not configured. Please configure your API key in Settings.");
    }

    /// <summary>
    /// Suggests improvements or optimizations for a diagram.
    /// </summary>
    /// <param name="mermaidCode">The Mermaid diagram code to analyze.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task that would return suggestions for improving the diagram when configured.</returns>
    /// <exception cref="InvalidOperationException">Thrown always by the null implementation to indicate the AI service is not configured.</exception>
    public Task<string> SuggestImprovementsAsync(string mermaidCode, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("AI service is not configured. Please configure your API key in Settings.");
    }
}
