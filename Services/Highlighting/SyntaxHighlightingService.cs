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

using Avalonia.Styling;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using System.Reflection;
using TextMateSharp.Grammars;

namespace MermaidPad.Services.Highlighting;

/// <summary>
/// Service for managing TextMate-based syntax highlighting in the text editor.
/// </summary>
/// <remarks>
/// <para>
/// This service provides Mermaid diagram syntax highlighting using TextMate grammars.
/// It supports automatic theme switching between light and dark modes based on the
/// Avalonia application theme.
/// </para>
/// <para>
/// See:
/// <list type="bullet">
///     <item><a href="https://github.com/danipen/TextMateSharp">TextMateSharp GitHub Repository</a></item>
///     <item><a href="https://github.com/AvaloniaUI/AvaloniaEdit?tab=readme-ov-file#how-to-set-up-textmate-theme-and-syntax-highlighting-for-my-project">AvaloniaEdit Readme</a></item>
/// </list>
/// </para>
/// </remarks>
public sealed class SyntaxHighlightingService : IDisposable
{
    private TextMate.Installation? _textMateInstallation;
    private TextEditor? _currentEditor;
    private bool _isInitialized;
    private ThemeName _currentTheme;
    private bool _isDisposed;

    /// <summary>
    /// Initializes the syntax highlighting service.
    /// This method should be called once during application startup to verify
    /// that all required resources are available.
    /// </summary>
    /// <exception cref="FileNotFoundException">
    /// Thrown if the Mermaid grammar resource cannot be found.
    /// </exception>
    public void Initialize()
    {
        // This class is a singleton controlled by dependency injection, so there is no need for synchronization here
        if (_isInitialized)
        {
            SimpleLogger.Log("Syntax highlighting service already initialized");
            return;
        }

        try
        {
            // Verify that the Mermaid grammar resource exists
            VerifyGrammarResource();

            _isInitialized = true;
            SimpleLogger.Log("Syntax highlighting service initialized successfully");
        }
        catch (Exception ex)
        {
            SimpleLogger.Log($"ERROR initializing syntax highlighting service: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Applies Mermaid syntax highlighting to the specified TextEditor.
    /// </summary>
    /// <param name="editor">The TextEditor control to apply syntax highlighting to.</param>
    /// <param name="themeName">
    /// The TextMate theme to use. If not specified, uses a theme appropriate
    /// for the current Avalonia theme variant.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown if editor is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the service has not been initialized.
    /// </exception>
    public void ApplyTo(TextEditor editor, ThemeName? themeName = null)
    {
        ArgumentNullException.ThrowIfNull(editor);

        if (!_isInitialized)
        {
            throw new InvalidOperationException($"{nameof(SyntaxHighlightingService)} must be initialized before use. Call {nameof(Initialize)} first.");
        }

        // Determine theme based on Avalonia's current theme if not specified
        ThemeName effectiveTheme = themeName ?? GetThemeForCurrentVariant();

        try
        {
            // Create custom registry options with Mermaid grammar
            MermaidRegistryOptions registryOptions = new MermaidRegistryOptions(effectiveTheme);

            // Install TextMate on the editor
            _textMateInstallation = editor.InstallTextMate(registryOptions);

            // Set the Mermaid grammar
            _textMateInstallation.SetGrammar(MermaidRegistryOptions.MermaidScopeName);

            // Store references for later theme switching
            _currentEditor = editor;
            _currentTheme = effectiveTheme;

            SimpleLogger.Log($"Syntax Highlighting applied for theme: {effectiveTheme}");
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError("Error applying syntax highlighting", ex);
            throw;
        }
    }

    /// <summary>
    /// Changes the syntax highlighting theme.
    /// </summary>
    /// <param name="themeName">The new theme to apply.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if no editor has been initialized with syntax highlighting.
    /// </exception>
    /// <remarks>
    /// This method re-applies the syntax highlighting with the new theme.
    /// The editor's text and cursor position are preserved.
    /// </remarks>
    private void ChangeTheme(ThemeName themeName)
    {
        if (_currentEditor is null)
        {
            throw new InvalidOperationException($"No editor has been initialized with syntax highlighting. Call {nameof(ApplyTo)} first.");
        }

        if (_currentTheme == themeName)
        {
            SimpleLogger.Log($"Theme is already set to {themeName}, skipping theme change");
            return;
        }

        SimpleLogger.Log($"Changing syntax highlighting theme from {_currentTheme} to {themeName}");

        try
        {
            // Re-apply syntax highlighting with new theme
            // Note: TextMate.Installation doesn't support direct theme switching,
            // so we need to reinstall with the new theme
            ApplyTo(_currentEditor, themeName);
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError("Error changing theme", ex);
            throw;
        }
    }

    /// <summary>
    /// Updates the syntax highlighting theme based on Avalonia's current theme variant.
    /// </summary>
    /// <param name="isDarkTheme">True if the current theme is dark, false if light.</param>
    /// <remarks>
    /// This method is typically called when the application theme changes.
    /// </remarks>
    public void UpdateThemeForVariant(bool isDarkTheme)
    {
        ThemeName newTheme = MermaidRegistryOptions.GetDefaultThemeName(isDarkTheme);
        if (_currentEditor is not null && _currentTheme != newTheme)
        {
            ChangeTheme(newTheme);
        }
    }

    /// <summary>
    /// Gets the appropriate TextMate theme based on Avalonia's current theme variant.
    /// </summary>
    /// <returns>
    /// ThemeName.DarkPlus for dark themes, ThemeName.Light for light themes,
    /// or ThemeName.DarkPlus as a fallback if theme variant cannot be determined.
    /// </returns>
    private static ThemeName GetThemeForCurrentVariant()
    {
        // Try to get the current theme variant from the application
        ThemeVariant? actualThemeVariant = Avalonia.Application.Current?.ActualThemeVariant;
        if (actualThemeVariant == ThemeVariant.Dark)
        {
            return ThemeName.DarkPlus;
        }
        else if (actualThemeVariant == ThemeVariant.Light)
        {
            return ThemeName.Light;
        }

        // Default to dark theme if we can't determine
        SimpleLogger.Log($"Could not determine theme variant, defaulting to {nameof(ThemeName.DarkPlus)}");
        return ThemeName.DarkPlus;
    }

    /// <summary>
    /// Verifies that the Mermaid grammar resource exists in the assembly.
    /// </summary>
    /// <exception cref="FileNotFoundException">
    /// Thrown if the grammar resource cannot be found.
    /// </exception>
    private static void VerifyGrammarResource()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();

        const string grammarResourceName = MermaidRegistryOptions.GrammarResourceName;
        using Stream? stream = assembly.GetManifestResourceStream(grammarResourceName);
        if (stream is null)
        {
            string[] allResources = assembly.GetManifestResourceNames();
            string resourceList = string.Join(", ", allResources);

            throw new FileNotFoundException($"Mermaid grammar resource not found. Expected: {grammarResourceName}. " +
                $"Available resources: {resourceList}. Ensure the grammar file is placed in Resources/Grammars/ and marked as EmbeddedResource.");
        }

        SimpleLogger.Log($"Verified Mermaid grammar resource exists: {grammarResourceName}");
    }

    /// <summary>
    /// Releases all resources used by the current instance of the class.
    /// </summary>
    /// <remarks>This method should NOT be called manually; the lifetime of SyntaxHighlightingService is controlled
    /// by dependency injection which will call this method automatically when appropriate.
    /// After calling this method, the instance is in an unusable state and should not be accessed.</remarks>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _textMateInstallation?.Dispose();
        _textMateInstallation = null;
        _currentEditor = null;
        _isInitialized = false;
        _isDisposed = true;
    }
}
