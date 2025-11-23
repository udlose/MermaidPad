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

using MermaidPad.Services;
using MermaidPad.Services.AI;
using MermaidPad.ViewModels.Panels;
using Microsoft.Extensions.Logging;

namespace MermaidPad.Factories;

/// <summary>
/// Factory for creating <see cref="AIPanelViewModel"/> instances with runtime-configured AI service.
/// </summary>
/// <remarks>
/// This factory creates AIPanelViewModel instances with the AI service configured from current user settings.
/// Since users can switch between different AI providers (Claude, OpenAI, Gemini, etc.) at runtime, the AI
/// service cannot be a static dependency. The factory reads the current provider selection from settings and
/// creates the appropriate AI service implementation.
/// </remarks>
public sealed class AIPanelViewModelFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly AIServiceFactory _aiServiceFactory;
    private readonly SettingsService _settingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIPanelViewModelFactory"/> class.
    /// </summary>
    /// <param name="loggerFactory">Factory for creating loggers.</param>
    /// <param name="aiServiceFactory">Factory for creating AI service implementations.</param>
    /// <param name="settingsService">Service providing access to current application settings.</param>
    public AIPanelViewModelFactory(ILoggerFactory loggerFactory, AIServiceFactory aiServiceFactory, SettingsService settingsService)
    {
        _loggerFactory = loggerFactory;
        _aiServiceFactory = aiServiceFactory;
        _settingsService = settingsService;
    }

    /// <summary>
    /// Creates a new <see cref="AIPanelViewModel"/> instance configured with the current AI service from settings.
    /// </summary>
    /// <returns>A fully initialized <see cref="AIPanelViewModel"/> instance.</returns>
    public AIPanelViewModel Create()
    {
        // Create AI service from current settings (provider, API key, model, etc.)
        IAIService aiService = _aiServiceFactory.CreateService(_settingsService.Settings.AI);

        // Create the Logger for the ViewModel
        ILogger<AIPanelViewModel> logger = _loggerFactory.CreateLogger<AIPanelViewModel>();

        return new AIPanelViewModel(logger, aiService);
    }
}
