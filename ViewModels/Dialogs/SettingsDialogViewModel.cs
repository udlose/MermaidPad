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
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.ViewModels.Dialogs;

/// <summary>
/// Represents the view model for the settings dialog, providing properties and commands for configuring AI features,
/// providers, and related options.
/// </summary>
/// <remarks>
/// This view model exposes bindable properties for AI configuration, including provider selection, model
/// choice, API key management, and feature toggles. It supports testing API connectivity and securely handles API key
/// encryption and decryption. All properties are intended for data binding in the view. Thread safety is not
/// guaranteed; instances should be used on the UI thread.
/// </remarks>
[SuppressMessage("ReSharper", "MemberCanBeMadeStatic.Global", Justification = "ViewModel properties are instance-based for binding.")]
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global", Justification = "ViewModel properties are set during initialization by the MVVM framework.")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global", Justification = "ViewModel properties are accessed by the view for data binding.")]
[SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "ViewModel members are accessed by the view for data binding.")]
public sealed partial class SettingsDialogViewModel : ViewModelBase
{
    private readonly ISecureStorageService _secureStorage;
    private readonly AIServiceFactory _aiServiceFactory;

    /// <summary>
    /// Gets or sets a value indicating whether AI features are enabled in the application.
    /// </summary>
    /// <remarks>
    /// When <c>true</c>, AI features will be available provided a valid provider and API key are configured.
    /// This property is bound to the settings UI toggle.
    /// </remarks>
    [ObservableProperty]
    public partial bool EnableAIFeatures { get; set; }

    /// <summary>
    /// Gets or sets the selected AI provider.
    /// </summary>
    /// <remarks>
    /// Use <see cref="AIProvider.None"/> to disable provider-specific functionality. This value is used when
    /// creating temporary settings for connection tests or when persisting AI configuration.
    /// </remarks>
    [ObservableProperty]
    public partial AIProvider SelectedProvider { get; set; }

    /// <summary>
    /// Gets or sets the unencrypted API key entered by the user.
    /// </summary>
    /// <remarks>
    /// This value is stored temporarily in memory for editing. When settings are saved via <see cref="GetUpdatedSettings"/>,
    /// the API key will be encrypted using the configured <see cref="ISecureStorageService"/>.
    /// </remarks>
    [ObservableProperty]
    public partial string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the API key should be displayed in plain text in the UI.
    /// </summary>
    /// <remarks>
    /// Default is <c>false</c>. This property is toggled by the <see cref="ToggleApiKeyVisibility"/> command.
    /// </remarks>
    [ObservableProperty]
    public partial bool ShowApiKey { get; set; }

    /// <summary>
    /// Gets or sets the selected model identifier for the chosen AI provider.
    /// </summary>
    /// <remarks>
    /// Defaults to "claude-sonnet-4". The list of valid models for Anthropic is provided by <see cref="AnthropicModels"/>.
    /// </remarks>
    [ObservableProperty]
    public partial string Model { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether natural language generation features are enabled.
    /// </summary>
    [ObservableProperty]
    public partial bool EnableNaturalLanguageGeneration { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether diagram explanation features are enabled.
    /// </summary>
    [ObservableProperty]
    public partial bool EnableDiagramExplanation { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether smart suggestions are enabled.
    /// </summary>
    [ObservableProperty]
    [DefaultValue(true)]
    public partial bool EnableSmartSuggestions { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a connection test is currently running.
    /// </summary>
    [ObservableProperty]
    public partial bool IsTestingConnection { get; set; }

    /// <summary>
    /// Gets or sets the textual result of the most recent connection test.
    /// </summary>
    [ObservableProperty]
    public partial string TestConnectionResult { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the last connection test succeeded.
    /// </summary>
    [ObservableProperty]
    public partial bool TestConnectionSuccess { get; set; }

    /// <summary>
    /// Gets the result of the dialog interaction, indicating whether the user accepted or canceled the dialog.
    /// </summary>
    /// <remarks>A value of <see langword="true"/> indicates the user accepted or confirmed the dialog; <see
    /// langword="false"/> indicates the user canceled or declined. If the value is <see langword="null"/>, the dialog
    /// result has not been set or the dialog was closed without an explicit choice.</remarks>
    [ObservableProperty]
    public partial bool? DialogResult { get; private set; }

    /// <summary>
    /// Gets a collection of available AI providers that can be selected by the user in the settings UI.
    /// </summary>
    /// <remarks>
    /// This collection is read-only and is initialized with supported providers. Add new providers here when implemented.
    /// </remarks>
    public ObservableCollection<AIProvider> AvailableProviders { get; } = new ObservableCollection<AIProvider>
    {
        AIProvider.None,
        AIProvider.Anthropic
    };

    /// <summary>
    /// Gets a collection of model identifiers available for Anthropic provider selection.
    /// </summary>
    /// <remarks>
    /// The UI can bind to this collection to populate a model-selection dropdown when <see cref="SelectedProvider"/>
    /// is set to <see cref="AIProvider.Anthropic"/>.
    /// </remarks>
    public ObservableCollection<string> AnthropicModels { get; } = new ObservableCollection<string>
    {
        "claude-sonnet-4",
        "claude-3-5-sonnet-20241022",
        "claude-3-5-haiku-20241022",
        "claude-opus-4"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsDialogViewModel"/> class using the provided services.
    /// </summary>
    /// <param name="settingsService">The settings service used to read current persisted settings. Cannot be <c>null</c>.</param>
    /// <param name="secureStorage">Platform-specific secure storage service for encrypting/decrypting API keys. Cannot be <c>null</c>.</param>
    /// <param name="aiServiceFactory">Factory used to create AI service instances for connection testing. Cannot be <c>null</c>.</param>
    /// <exception cref="ArgumentNullException">Thrown when any of the parameters are <c>null</c>.</exception>
    public SettingsDialogViewModel(SettingsService settingsService, ISecureStorageService secureStorage, AIServiceFactory aiServiceFactory)
    {
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(secureStorage);
        ArgumentNullException.ThrowIfNull(aiServiceFactory);

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

    /// <summary>
    /// Toggles the visibility of the API key in the UI.
    /// </summary>
    /// <remarks>
    /// This command flips the <see cref="ShowApiKey"/> property between <c>true</c> and <c>false</c>. It is intended
    /// to be bound to a button in the settings dialog to allow users to reveal or hide their API key.
    /// </remarks>
    [RelayCommand]
    private void ToggleApiKeyVisibility()
    {
        ShowApiKey = !ShowApiKey;
    }

    /// <summary>
    /// Tests the currently entered API key against the selected provider by creating a temporary AI service and validating the key.
    /// </summary>
    /// <returns>A task that completes when the test has finished. On completion the properties <see cref="TestConnectionResult"/>,
    /// <see cref="TestConnectionSuccess"/>, and <see cref="IsTestingConnection"/> reflect the outcome and state.</returns>
    /// <remarks>
    /// If no provider is selected or the API key is empty, the method sets <see cref="TestConnectionResult"/> with an appropriate
    /// message and returns immediately. Exceptions during validation are caught and surfaced via <see cref="TestConnectionResult"/>.
    /// </remarks>
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
            AISettings testSettings = new AISettings
            {
                Provider = SelectedProvider,
                Model = Model,
                EnableAIFeatures = true,
                EncryptedApiKey = _secureStorage.Encrypt(ApiKey)
            };

            IAIService service = _aiServiceFactory.CreateService(testSettings);
            bool isValid = await service.ValidateApiKeyAsync();

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
        catch (Exception ex)
        {
            TestConnectionResult = $"Error: {ex.Message}";
            TestConnectionSuccess = false;
        }
        finally
        {
            IsTestingConnection = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResult = false;
    }

    [RelayCommand]
    private void Save()
    {
        DialogResult = true;
        //TODO is this where this should go?
        //_secureStorage.Encrypt(ApiKey);
    }

    /// <summary>
    /// Gets the updated AI settings representing the current state of this view model.
    /// </summary>
    /// <returns>
    /// An <see cref="AISettings"/> instance populated from the view model. If <see cref="ApiKey"/> contains text the API key
    /// is encrypted via the configured <see cref="ISecureStorageService"/> and placed into <see cref="AISettings.EncryptedApiKey"/>.
    /// If the <see cref="ApiKey"/> is empty or whitespace, <see cref="AISettings.EncryptedApiKey"/> will be an empty string.
    /// </returns>
    public AISettings GetUpdatedSettings()
    {
        string encryptedApiKey = string.Empty;
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
