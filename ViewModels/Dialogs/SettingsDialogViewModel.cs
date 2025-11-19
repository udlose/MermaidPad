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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MermaidPad.Models.AI;
using MermaidPad.Services;
using MermaidPad.Services.AI;
using MermaidPad.Services.Platforms;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace MermaidPad.ViewModels.Dialogs;

/// <summary>
/// ViewModel for the Settings dialog.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
public sealed partial class SettingsDialogViewModel : ViewModelBase
{
    private readonly ISecureStorageService _secureStorage;
    private readonly AIServiceFactory _aiServiceFactory;

    [ObservableProperty]
    private bool _enableAIFeatures;

    [ObservableProperty]
    private AIProvider _selectedProvider;

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private bool _showApiKey = false;

    [ObservableProperty]
    private string _model = "claude-sonnet-4";

    [ObservableProperty]
    private bool _enableNaturalLanguageGeneration = true;

    [ObservableProperty]
    private bool _enableDiagramExplanation = true;

    [ObservableProperty]
    private bool _enableSmartSuggestions = true;

    [ObservableProperty]
    private bool _isTestingConnection = false;

    [ObservableProperty]
    private string _testConnectionResult = string.Empty;

    [ObservableProperty]
    private bool _testConnectionSuccess = false;

    public ObservableCollection<AIProvider> AvailableProviders { get; } = new()
    {
        AIProvider.None,
        AIProvider.Anthropic
    };

    public ObservableCollection<string> AnthropicModels { get; } = new()
    {
        "claude-sonnet-4",
        "claude-3-5-sonnet-20241022",
        "claude-3-5-haiku-20241022",
        "claude-opus-4"
    };

    public SettingsDialogViewModel(
        SettingsService settingsService,
        ISecureStorageService secureStorage,
        AIServiceFactory aiServiceFactory)
    {
        _secureStorage = secureStorage;
        _aiServiceFactory = aiServiceFactory;

        // Load current settings
        AISettings currentSettings = settingsService.Settings.AI;
        EnableAIFeatures = currentSettings.EnableAIFeatures;
        SelectedProvider = currentSettings.Provider;
        Model = currentSettings.Model;
        EnableNaturalLanguageGeneration = currentSettings.EnableNaturalLanguageGeneration;
        EnableDiagramExplanation = currentSettings.EnableDiagramExplanation;
        EnableSmartSuggestions = currentSettings.EnableSmartSuggestions;

        // Decrypt API key if present
        if (!string.IsNullOrEmpty(currentSettings.EncryptedApiKey))
        {
            try
            {
                ApiKey = _secureStorage.Decrypt(currentSettings.EncryptedApiKey);
            }
            catch
            {
                ApiKey = string.Empty;
            }
        }
    }

    [RelayCommand]
    private void ToggleApiKeyVisibility()
    {
        ShowApiKey = !ShowApiKey;
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (SelectedProvider == AIProvider.None || string.IsNullOrWhiteSpace(ApiKey))
        {
            TestConnectionResult = "Please select a provider and enter an API key.";
            TestConnectionSuccess = false;
            return;
        }

        IsTestingConnection = true;
        TestConnectionResult = "Testing connection...";
        TestConnectionSuccess = false;

        try
        {
            // Create a temporary settings object for testing
            var testSettings = new AISettings
            {
                Provider = SelectedProvider,
                Model = Model,
                EnableAIFeatures = true,
                EncryptedApiKey = _secureStorage.Encrypt(ApiKey)
            };

            var service = _aiServiceFactory.CreateService(testSettings);
            var isValid = await service.ValidateApiKeyAsync();

            if (isValid)
            {
                TestConnectionResult = "Connection successful! API key is valid.";
                TestConnectionSuccess = true;
            }
            else
            {
                TestConnectionResult = "Connection failed. Please check your API key.";
                TestConnectionSuccess = false;
            }
        }
        catch (System.Exception ex)
        {
            TestConnectionResult = $"Error: {ex.Message}";
            TestConnectionSuccess = false;
        }
        finally
        {
            IsTestingConnection = false;
        }
    }

    /// <summary>
    /// Gets the updated AI settings with encrypted API key.
    /// </summary>
    public AISettings GetUpdatedSettings()
    {
        var encryptedApiKey = string.Empty;
        if (!string.IsNullOrWhiteSpace(ApiKey))
        {
            encryptedApiKey = _secureStorage.Encrypt(ApiKey);
        }

        return new AISettings
        {
            Provider = SelectedProvider,
            EncryptedApiKey = encryptedApiKey,
            Model = Model,
            EnableAIFeatures = EnableAIFeatures,
            EnableNaturalLanguageGeneration = EnableNaturalLanguageGeneration,
            EnableDiagramExplanation = EnableDiagramExplanation,
            EnableSmartSuggestions = EnableSmartSuggestions
        };
    }
}
