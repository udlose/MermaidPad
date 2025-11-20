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
using MermaidPad.Services.AI;
using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.ViewModels;

/// <summary>
/// Represents the view model for the AI panel, providing properties and commands for generating, explaining, and
/// improving diagrams using an AI service.
/// </summary>
/// <remarks>This view model is designed for use in MVVM applications and exposes properties for data binding to
/// the view. It manages the state and interactions with the underlying AI service, including handling user prompts,
/// displaying results, and updating status messages. Commands are provided for generating diagrams, explaining
/// diagrams, suggesting improvements, and managing results. The view model raises the DiagramGenerated event when a new
/// diagram is ready to be inserted into the editor.</remarks>
[SuppressMessage("ReSharper", "MemberCanBeMadeStatic.Global", Justification = "ViewModel properties are instance-based for binding.")]
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global", Justification = "ViewModel properties are set during initialization by the MVVM framework.")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global", Justification = "ViewModel properties are accessed by the view for data binding.")]
[SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "ViewModel members are accessed by the view for data binding.")]
public sealed partial class AIPanelViewModel : ViewModelBase
{
    private IAIService _aiService;

    [ObservableProperty]
    public partial string PromptText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ResultText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsProcessing { get; set; }

    [ObservableProperty]
    public partial bool IsConfigured { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    public AIPanelViewModel(IAIService aiService)
    {
        _aiService = aiService;
        IsConfigured = _aiService.IsConfigured;

        if (!IsConfigured)
        {
            StatusMessage = "AI is not configured. Please configure your API key in Settings.";
        }
    }

    /// <summary>
    /// Updates the AI service (called when settings change).
    /// </summary>
    public void UpdateAIService(IAIService newService)
    {
        ArgumentNullException.ThrowIfNull(newService);

        _aiService = newService;
        IsConfigured = newService.IsConfigured;
        StatusMessage = !IsConfigured ? "AI is not configured. Please configure your API key in Settings." : string.Empty;
    }

    [RelayCommand]
    private async Task GenerateDiagramAsync()
    {
        if (!IsConfigured)
        {
            StatusMessage = "Please configure AI in Settings first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(PromptText))
        {
            StatusMessage = "Please enter a description of the diagram you want to generate.";
            return;
        }

        IsProcessing = true;
        StatusMessage = "Generating diagram...";
        ResultText = string.Empty;

        try
        {
            string result = await _aiService.GenerateDiagramFromPromptAsync(PromptText);
            ResultText = result;
            StatusMessage = "Diagram generated successfully! Copy the code to the editor.";

            // Raise event to insert into editor
            OnDiagramGenerated(result);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            ResultText = string.Empty;
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task ExplainDiagramAsync(string? mermaidCode)
    {
        if (!IsConfigured)
        {
            StatusMessage = "Please configure AI in Settings first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(mermaidCode))
        {
            StatusMessage = "No diagram to explain. Please create a diagram first.";
            return;
        }

        IsProcessing = true;
        StatusMessage = "Explaining diagram...";
        ResultText = string.Empty;

        try
        {
            ResultText = await _aiService.ExplainDiagramAsync(mermaidCode);
            StatusMessage = "Explanation generated successfully!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            ResultText = string.Empty;
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task SuggestImprovementsAsync(string? mermaidCode)
    {
        if (!IsConfigured)
        {
            StatusMessage = "Please configure AI in Settings first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(mermaidCode))
        {
            StatusMessage = "No diagram to analyze. Please create a diagram first.";
            return;
        }

        IsProcessing = true;
        StatusMessage = "Analyzing diagram...";
        ResultText = string.Empty;

        try
        {
            ResultText = await _aiService.SuggestImprovementsAsync(mermaidCode);
            StatusMessage = "Suggestions generated successfully!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            ResultText = string.Empty;
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private void InsertResultToEditor()
    {
        if (!string.IsNullOrWhiteSpace(ResultText))
        {
            OnDiagramGenerated(ResultText);
        }
    }

    [RelayCommand]
    private void ClearResult()
    {
        ResultText = string.Empty;
        PromptText = string.Empty;
        StatusMessage = string.Empty;
    }

    /// <summary>
    /// Event raised when a diagram is generated and should be inserted into the editor.
    /// </summary>
    public event EventHandler<string>? DiagramGenerated;

    private void OnDiagramGenerated(string diagram)
    {
        DiagramGenerated?.Invoke(this, diagram);
    }
}
