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
using MermaidPad.Services.Platforms;
using Microsoft.Extensions.Logging;

namespace MermaidPad.Services.AI;

/// <summary>
/// Provides a factory for creating AI service instances based on specified settings and provider configuration.
/// </summary>
/// <remarks>Use this class to obtain an implementation of <see cref="IAIService"/> that is configured according
/// to the given <see cref="AISettings"/>. The factory handles provider selection, secure API key decryption, and
/// returns a non-operational service if AI features are disabled or configuration is incomplete. This class is
/// thread-safe and intended for use in application-level dependency injection scenarios.</remarks>
public sealed class AIServiceFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<AIServiceFactory> _logger;
    private readonly ISecureStorageService _secureStorage;
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the AIServiceFactory class using the specified secure storage and HTTP client factory services.
    /// </summary>
    /// <param name="loggerFactory">The factory for creating loggers. Cannot be null.</param>
    /// <param name="logger">The logger instance for logging factory operations. Cannot be null.</param>
    /// <param name="secureStorage">The secure storage service used to manage sensitive data required by AI service instances. Cannot be null.</param>
    /// <param name="httpClientFactory">The HTTP client factory used to create HTTP clients for AI service communication. Cannot be null.</param>
    public AIServiceFactory(ILoggerFactory loggerFactory, ILogger<AIServiceFactory> logger, ISecureStorageService secureStorage, IHttpClientFactory httpClientFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = logger;
        _secureStorage = secureStorage;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Creates and configures an AI service instance based on the specified settings.
    /// </summary>
    /// <remarks>If the API key decryption fails or the API key is not provided, a <see cref="NullAIService"/>
    /// is returned. This method does not support the OpenAI provider at this time.</remarks>
    /// <param name="settings">The AI settings used to determine the provider, model, and API credentials. Cannot be null.</param>
    /// <returns>An instance of <see cref="IAIService"/> corresponding to the configured provider and model. Returns a <see
    /// cref="NullAIService"/> if AI features are disabled, the provider is not set, or the API key is missing or
    /// invalid.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotImplementedException">Thrown if the specified provider is <see cref="AIProvider.OpenAI"/>, which is not yet supported.</exception>
    public IAIService CreateService(AISettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.EnableAIFeatures || settings.Provider == AIProvider.None)
        {
            // AI features are disabled or no provider is set, so return a null implementation
            return new NullAIService();
        }

        // Decrypt the API key
        string apiKey = string.Empty;
        if (!string.IsNullOrEmpty(settings.EncryptedApiKey))
        {
            try
            {
                apiKey = _secureStorage.Decrypt(settings.EncryptedApiKey);
            }
            catch (Exception ex)
            {
                // If decryption fails, treat as unconfigured
                _logger.LogWarning(ex, "Failed to decrypt AI API key; returning {AIServiceName}.", nameof(NullAIService));
                return new NullAIService();
            }
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            return new NullAIService();
        }

        return settings.Provider switch
        {
            AIProvider.Anthropic => new AnthropicAIService(
                _loggerFactory.CreateLogger<AnthropicAIService>(),
                _httpClientFactory.CreateClient("Anthropic"),
                apiKey,
                settings.Model),

            // TODO: Implement OpenAI service when ready
            AIProvider.OpenAI => throw new NotImplementedException("OpenAI support coming soon"),
            _ => new NullAIService()
        };
    }
}
