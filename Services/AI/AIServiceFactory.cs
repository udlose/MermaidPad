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

using MermaidPad.Models.AI;

namespace MermaidPad.Services.AI;

/// <summary>
/// Factory for creating AI service instances based on configuration.
/// </summary>
public sealed class AIServiceFactory
{
    private readonly ISecureStorageService _secureStorage;

    public AIServiceFactory(ISecureStorageService secureStorage)
    {
        _secureStorage = secureStorage;
    }

    /// <summary>
    /// Creates an AI service instance based on the provided settings.
    /// </summary>
    public IAIService CreateService(AISettings settings)
    {
        if (!settings.EnableAIFeatures || settings.Provider == AIProvider.None)
        {
            return new NullAIService();
        }

        // Decrypt the API key
        var apiKey = string.Empty;
        if (!string.IsNullOrEmpty(settings.EncryptedApiKey))
        {
            try
            {
                apiKey = _secureStorage.Decrypt(settings.EncryptedApiKey);
            }
            catch
            {
                // If decryption fails, treat as unconfigured
                return new NullAIService();
            }
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            return new NullAIService();
        }

        return settings.Provider switch
        {
            AIProvider.Anthropic => new AnthropicAIService(apiKey, settings.Model),
            AIProvider.OpenAI => throw new System.NotImplementedException("OpenAI support coming soon"),
            _ => new NullAIService()
        };
    }
}
