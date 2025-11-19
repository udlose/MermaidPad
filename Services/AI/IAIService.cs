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
/// Defines the contract for an AI-powered service that generates, explains, and improves Mermaid diagrams based on
/// natural language input or existing diagram code.
/// </summary>
/// <remarks>Implementations of this interface provide methods for validating API credentials, generating Mermaid
/// diagram syntax from user prompts, explaining diagrams in plain language, and suggesting optimizations. These
/// capabilities are intended to support applications that integrate AI-driven diagramming and analysis workflows. All
/// methods are asynchronous and support cancellation via a <see cref="CancellationToken"/>.</remarks>
public interface IAIService
{
    /// <summary>
    /// Gets a value indicating whether the component has been configured with the required settings and is ready for use.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Validates the API key by making a test request.
    /// </summary>
    Task<bool> ValidateApiKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates Mermaid diagram code from a natural language description.
    /// </summary>
    /// <param name="prompt">Natural language description of the desired diagram.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated Mermaid syntax.</returns>
    Task<string> GenerateDiagramFromPromptAsync(string prompt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Explains what a Mermaid diagram represents in natural language.
    /// </summary>
    /// <param name="mermaidCode">The Mermaid diagram code to explain.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Human-readable explanation of the diagram.</returns>
    Task<string> ExplainDiagramAsync(string mermaidCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Suggests improvements or optimizations for a diagram.
    /// </summary>
    /// <param name="mermaidCode">The Mermaid diagram code to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Suggestions for improving the diagram.</returns>
    Task<string> SuggestImprovementsAsync(string mermaidCode, CancellationToken cancellationToken = default);
}
