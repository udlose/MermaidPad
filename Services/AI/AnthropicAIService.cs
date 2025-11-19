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
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Anthropic;
using Anthropic.Models.Messages;
using Anthropic.Models.Content;

namespace MermaidPad.Services.AI;

/// <summary>
/// Anthropic Claude implementation of the AI service using the official Anthropic SDK.
/// </summary>
public sealed class AnthropicAIService : IAIService
{
    private readonly string _apiKey;
    private readonly string _model;
    private readonly AnthropicClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnthropicAIService"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for API requests.</param>
    /// <param name="apiKey">The Anthropic API key.</param>
    /// <param name="model">The model to use for requests.</param>
    public AnthropicAIService(HttpClient? httpClient, string apiKey, string model)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));

        _apiKey = apiKey;
        _model = string.IsNullOrWhiteSpace(model) ? "claude-sonnet-4" : model;

        // Create client with optional HttpClient for better resource management
        // Note: If the SDK doesn't support custom HttpClient, this will fall back to default
        _client = httpClient != null
            ? new AnthropicClient(httpClient) { APIKey = apiKey }
            : new AnthropicClient { APIKey = apiKey };
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<bool> ValidateApiKeyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new MessageCreateParams
            {
                Model = _model,
                MaxTokens = 10,
                Messages =
                [
                    new()
                    {
                        Role = Role.User,
                        Content = "Hello, please respond with 'OK'."
                    }
                ]
            };

            var response = await _client.Messages.Create(parameters, cancellationToken);
            return response?.Content?.Any(c => c.Value is TextBlock) == true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<string> GenerateDiagramFromPromptAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt cannot be null or empty", nameof(prompt));

        var systemPrompt = @"You are an expert in Mermaid diagram syntax. When given a description, generate ONLY the Mermaid code without any markdown code fences, explanations, or additional text.

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

        var parameters = new MessageCreateParams
        {
            Model = _model,
            MaxTokens = 2048,
            System = systemPrompt,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = prompt
                }
            ]
        };

        var response = await _client.Messages.Create(parameters, cancellationToken);
        var content = string.Join(
            "",
            response?.Content?
                .Where(c => c.Value is TextBlock)
                .Select(c => c.Value as TextBlock)
                .Select(tb => tb?.Text ?? string.Empty) ?? Enumerable.Empty<string>());

        // Clean up response in case AI included markdown fences
        content = content.Trim();
        if (content.StartsWith("```mermaid"))
        {
            content = content["```mermaid".Length..];
        }
        if (content.StartsWith("```"))
        {
            content = content[3..];
        }
        if (content.EndsWith("```"))
        {
            content = content[..^3];
        }

        return content.Trim();
    }

    public async Task<string> ExplainDiagramAsync(string mermaidCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mermaidCode))
            throw new ArgumentException("Mermaid code cannot be null or empty", nameof(mermaidCode));

        var systemPrompt = @"You are an expert at reading and explaining Mermaid diagrams. Provide clear, concise explanations of what the diagram represents, its structure, and key relationships. Format your response in a readable way.";

        var parameters = new MessageCreateParams
        {
            Model = _model,
            MaxTokens = 1024,
            System = systemPrompt,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = $"Please explain this Mermaid diagram:\n\n{mermaidCode}"
                }
            ]
        };

        var response = await _client.Messages.Create(parameters, cancellationToken);
        var explanation = string.Join(
            "",
            response?.Content?
                .Where(c => c.Value is TextBlock)
                .Select(c => c.Value as TextBlock)
                .Select(tb => tb?.Text ?? string.Empty) ?? Enumerable.Empty<string>());

        return string.IsNullOrWhiteSpace(explanation) ? "Unable to generate explanation." : explanation;
    }

    public async Task<string> SuggestImprovementsAsync(string mermaidCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mermaidCode))
            throw new ArgumentException("Mermaid code cannot be null or empty", nameof(mermaidCode));

        var systemPrompt = @"You are an expert at analyzing Mermaid diagrams. Suggest improvements for clarity, structure, and best practices. Be specific and actionable.";

        var parameters = new MessageCreateParams
        {
            Model = _model,
            MaxTokens = 1024,
            System = systemPrompt,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = $"Please analyze this Mermaid diagram and suggest improvements:\n\n{mermaidCode}"
                }
            ]
        };

        var response = await _client.Messages.Create(parameters, cancellationToken);
        var suggestions = string.Join(
            "",
            response?.Content?
                .Where(c => c.Value is TextBlock)
                .Select(c => c.Value as TextBlock)
                .Select(tb => tb?.Text ?? string.Empty) ?? Enumerable.Empty<string>());

        return string.IsNullOrWhiteSpace(suggestions) ? "Unable to generate suggestions." : suggestions;
    }
}
