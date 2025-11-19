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

using Anthropic;
using Anthropic.Core;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace MermaidPad.Services.AI;

/// <summary>
/// Anthropic Claude implementation of the AI service using the official Anthropic SDK.
/// </summary>
/// <remarks>
/// This implementation of <see cref="IAIService"/> uses an <see cref="AnthropicClient"/> to communicate
/// with Anthropic's Claude models. It provides methods to validate API credentials, generate Mermaid diagram
/// syntax from natural language, explain existing Mermaid diagrams, and suggest improvements. Consumers should
/// provide a valid API key when constructing this service; otherwise <see cref="IsConfigured"/> will be <see langword="false"/>
/// and calls will fail with the exceptions thrown by the underlying SDK or argument validation.
/// </remarks>
public sealed class AnthropicAIService : IAIService
{
    internal const int ApiTimeoutInSeconds = 120;
    private const int MaxTokens = 2_048;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly AnthropicClient _client;
    private readonly ILogger<AnthropicAIService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnthropicAIService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for logging service operations. Cannot be null.</param>
    /// <param name="httpClient">
    /// An optional <see cref="HttpClient"/> instance to use for requests. Providing an <see cref="HttpClient"/>
    /// allows callers to control connection lifetime and reuse. If <see langword="null"/>, the SDK will create its own client.
    /// </param>
    /// <param name="apiKey">The Anthropic API key to authenticate requests. This value must not be <see langword="null"/> or whitespace.</param>
    /// <param name="model">
    /// The Anthropic model identifier to use for requests. If <see cref="string.IsNullOrWhiteSpace(string)"/> the default
    /// model <c>claude-sonnet-4</c> will be used.
    /// </param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="apiKey"/> is <see langword="null"/> or whitespace.</exception>
    public AnthropicAIService(ILogger<AnthropicAIService> logger, HttpClient httpClient, string apiKey, string model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        _apiKey = apiKey;
        _logger = logger;
        _model = string.IsNullOrWhiteSpace(model) ? "claude-sonnet-4" : model;

        // Create client with optional HttpClient for better resource management
        ClientOptions clientOptions = new ClientOptions
        {
            APIKey = apiKey,
            HttpClient = httpClient,
            Timeout = TimeSpan.FromSeconds(ApiTimeoutInSeconds)
        };

        _client = new AnthropicClient(clientOptions);
    }

    /// <summary>
    /// Gets a value indicating whether the service was configured with a non-empty API key.
    /// </summary>
    /// <value>
    /// <see langword="true"/> when an API key was provided; otherwise <see langword="false"/>.
    /// </value>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    /// <summary>
    /// Validates the configured API key by performing a lightweight test request to the Anthropic service.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>
    /// A <see cref="Task{Boolean}"/> that completes with <see langword="true"/> if the API responded with at least one
    /// <c>TextBlock</c>-typed content element; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// This method swallows exceptions from the underlying SDK and returns <see langword="false"/> when any error occurs.
    /// It issues a small request (10 tokens max) and should be suitable for quick validation checks.
    /// </remarks>
    public async Task<bool> ValidateApiKeyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            MessageCreateParams parameters = new MessageCreateParams
            {
                Model = _model,
                MaxTokens = 10,
                Messages =
                [
                    new MessageParam
                    {
                        Role = Role.User,
                        Content = "Hello, please respond with 'OK'."
                    }
                ]
            };

            Message response = await _client.Messages.Create(parameters, cancellationToken);
            return response.Content.Any(static c => c.Value is TextBlock);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error validating API key.");
            return false;
        }
    }

    /// <summary>
    /// Generates Mermaid diagram code from a natural language description.
    /// </summary>
    /// <param name="prompt">A natural language description of the desired diagram. Must not be <see langword="null"/> or whitespace.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>
    /// A <see cref="Task{String}"/> that completes with the raw Mermaid syntax produced by the model.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="prompt"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="Exception">Exceptions thrown by the underlying Anthropic SDK may propagate (network errors, timeouts, etc.).</exception>
    public async Task<string> GenerateDiagramFromPromptAsync(string prompt, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        const string systemPrompt = @"You are an expert in Mermaid diagram syntax. When given a description, generate ONLY the Mermaid code without any markdown code fences, explanations, or additional text.

Rules:
- Return ONLY the raw Mermaid syntax
- Do NOT wrap in ```mermaid code blocks
- Do NOT include any explanatory text
- Ensure the syntax is valid and will render correctly
- Use appropriate diagram types (flowchart, sequence, class, state, gantt, pie, etc.)

Example response format:
flowchart TD
    A[Start] --> B[Process]
    B --> C[End]";

        MessageCreateParams parameters = new MessageCreateParams
        {
            Model = _model,
            MaxTokens = MaxTokens,
            System = systemPrompt,
            Messages =
            [
                new MessageParam
                {
                    Role = Role.User,
                    Content = prompt
                }
            ]
        };

        try
        {
            Message response = await _client.Messages.Create(parameters, cancellationToken);
            string diagramCode = AggregateAiResponseContent(response);
            return UnwrapMermaidCode(diagramCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating diagram from prompt.");
            throw;
        }
    }

    /// <summary>
    /// Produces a natural-language explanation of the provided Mermaid diagram code.
    /// </summary>
    /// <param name="mermaidCode">The Mermaid diagram code to explain. Must not be <see langword="null"/> or whitespace.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>
    /// A <see cref="Task{String}"/> that completes with a human-readable explanation of the diagram. If the model
    /// returns no text content, the method returns the string "Unable to generate explanation".
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="mermaidCode"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="Exception">Exceptions thrown by the underlying Anthropic SDK may propagate (network errors, timeouts, etc.).</exception>
    public async Task<string> ExplainDiagramAsync(string mermaidCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mermaidCode);

        const string systemPrompt = "You are an expert at reading and explaining Mermaid diagrams. Provide clear, concise explanations of what the diagram represents, its structure, and key relationships. Format your response in a readable way.";
        MessageCreateParams parameters = new MessageCreateParams
        {
            Model = _model,
            MaxTokens = MaxTokens,
            System = systemPrompt,
            Messages =
            [
                new MessageParam()
                {
                    Role = Role.User,
                    Content = $"Please explain this Mermaid diagram:{Environment.NewLine}{Environment.NewLine}{mermaidCode}"
                }
            ]
        };

        try
        {
            Message response = await _client.Messages.Create(parameters, cancellationToken);
            string explanation = AggregateAiResponseContent(response);
            return string.IsNullOrWhiteSpace(explanation) ? "Unable to generate explanation." : explanation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error explaining diagram.");
            throw;
        }
    }

    /// <summary>
    /// Analyzes the provided Mermaid diagram code and suggests concrete improvements for clarity and best practices.
    /// </summary>
    /// <param name="mermaidCode">The Mermaid diagram code to analyze. Must not be <see langword="null"/> or whitespace.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>
    /// A <see cref="Task{String}"/> that completes with suggested improvements. If the model returns no text content,
    /// the method returns the string "Unable to generate suggestions".
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="mermaidCode"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="Exception">Exceptions thrown by the underlying Anthropic SDK may propagate (network errors, timeouts, etc.).</exception>
    public async Task<string> SuggestImprovementsAsync(string mermaidCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mermaidCode);

        const string systemPrompt = "You are an expert at analyzing Mermaid diagrams. Suggest improvements for clarity, structure, and best practices. Be specific and actionable.";
        MessageCreateParams parameters = new MessageCreateParams
        {
            Model = _model,
            MaxTokens = MaxTokens,
            System = systemPrompt,
            Messages =
            [
                new MessageParam()
                {
                    Role = Role.User,
                    Content = $"Please analyze this Mermaid diagram and suggest improvements:{Environment.NewLine}{Environment.NewLine}{mermaidCode}"
                }
            ]
        };

        try
        {
            Message response = await _client.Messages.Create(parameters, cancellationToken);
            string suggestions = AggregateAiResponseContent(response);
            return string.IsNullOrWhiteSpace(suggestions) ? "Unable to generate suggestions." : suggestions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error suggesting improvements.");
            throw;
        }
    }

    /// <summary>
    /// Aggregates all non-null text content from the specified AI response message into a single string.
    /// </summary>
    /// <param name="response">The AI response message containing content blocks to be aggregated. Must not be null.</param>
    /// <returns>A string containing the concatenated text from all non-null text blocks in the response. Returns an empty string
    /// if no text blocks are present.</returns>
    [SuppressMessage("ReSharper", "MergeIntoPattern", Justification = "Not a fan of the null-object pattern.")]
    private static string AggregateAiResponseContent(Message response)
    {
        StringBuilder sb = new StringBuilder(capacity: 1_024);
        foreach (ContentBlock contentBlock in response.Content)
        {
            if (contentBlock.Value is TextBlock textBlock && textBlock.Text is not null)
            {
                sb.Append(textBlock.Text);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Removes Markdown code block markers from a Mermaid diagram string, returning only the diagram code.
    /// </summary>
    /// <remarks>This method trims whitespace and removes both the "```mermaid" and generic "```" code block
    /// markers if present. It is useful for extracting raw Mermaid code from Markdown documents or user
    /// input.</remarks>
    /// <param name="diagramCode">The input string containing a Mermaid diagram, optionally wrapped in Markdown code block markers such as
    /// "```mermaid" and "```".</param>
    /// <returns>A string containing the Mermaid diagram code with any leading or trailing Markdown code block markers removed.</returns>
    private static string UnwrapMermaidCode(string diagramCode)
    {
        ReadOnlySpan<char> span = diagramCode.AsSpan().Trim();

        // Check for "```mermaid" prefix
        if (span.StartsWith("```mermaid", StringComparison.Ordinal))
        {
            // Slice off "```mermaid" prefix and trim leading whitespace
            span = span["```mermaid".Length..].TrimStart();
        }
        // Check for generic "```" prefix (only if "```mermaid" wasn't found)
        else if (span.StartsWith("```", StringComparison.Ordinal))
        {
            // Slice off "```" prefix and trim leading whitespace
            span = span[3..].TrimStart();
        }

        // Remove trailing "```" if present
        if (span.EndsWith("```", StringComparison.Ordinal))
        {
            // Slice off "```" suffix and trim trailing whitespace
            span = span[..^3].TrimEnd();
        }

        return span.ToString();
    }
}
