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

namespace MermaidPad.Models.AI;

/// <summary>
/// Configuration settings for AI features in MermaidPad.
/// </summary>
public sealed class AISettings
{
    /// <summary>
    /// Selected AI provider. Default is None (AI disabled).
    /// </summary>
    public AIProvider Provider { get; set; } = AIProvider.None;

    /// <summary>
    /// Encrypted API key for the selected provider.
    /// This is encrypted using platform-specific secure storage.
    /// </summary>
    public string EncryptedApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Model identifier for the AI provider (e.g., "claude-sonnet-4", "gpt-4").
    /// </summary>
    public string Model { get; set; } = "claude-sonnet-4";

    /// <summary>
    /// Whether AI features are enabled.
    /// Requires both this flag and a valid configured provider.
    /// </summary>
    public bool EnableAIFeatures { get; set; } = false;

    /// <summary>
    /// Enable natural language to diagram generation.
    /// </summary>
    public bool EnableNaturalLanguageGeneration { get; set; } = true;

    /// <summary>
    /// Enable diagram explanation feature.
    /// </summary>
    public bool EnableDiagramExplanation { get; set; } = true;

    /// <summary>
    /// Enable AI-powered suggestions and improvements.
    /// </summary>
    public bool EnableSmartSuggestions { get; set; } = true;
}
