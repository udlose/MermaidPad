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
/// <para>This service is thread-safe and can be used from multiple threads.</para>
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
    private readonly Lock _sync = new Lock();
    private TextMate.Installation? _textMateInstallation;
    private TextEditor? _currentEditor;
    private ThemeName _currentTheme;
    private volatile bool _isInitialized;
    private volatile bool _isDisposed;

    /// <summary>
    /// Initializes the syntax highlighting service, preparing it for use.
    /// </summary>
    /// <remarks>This method verifies that required grammar resources are available and ensures the service is
    /// only initialized once. If the service has already been initialized or has been disposed, the method has no
    /// effect or throws an exception, respectively. This method is thread-safe.</remarks>
    /// <exception cref="ObjectDisposedException">Thrown if the service has already been disposed.</exception>
    public void Initialize()
    {
        // This is a volatile read
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        // This is a volatile read
        if (_isInitialized)
        {
            SimpleLogger.Log("Syntax highlighting service already initialized");
            return;
        }

        try
        {
            // Verify that the grammar resource exists. Uses I/O so do it outside locks.
            VerifyGrammarResource();

            // Finalize initialization under lock to avoid races
            lock (_sync)
            {
                // Guard again for dispose/other initializers
                ObjectDisposedException.ThrowIf(_isDisposed, this);

                if (_isInitialized)
                {
                    SimpleLogger.Log("Syntax highlighting service already initialized (post-verify)");
                    return;
                }

                _isInitialized = true;
                SimpleLogger.Log("Syntax highlighting service initialized successfully");
            }
        }
        catch (Exception ex)
        {
            SimpleLogger.Log($"ERROR initializing syntax highlighting service: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Applies Mermaid syntax highlighting to the specified text editor, optionally using the provided theme.
    /// </summary>
    /// <remarks>This method replaces any existing syntax highlighting installation on the editor. If called
    /// multiple times, previous installations are disposed before applying the new highlighting. Thread safety is
    /// ensured during state changes. The method must be called after the service has been initialized and before it is
    /// disposed.</remarks>
    /// <param name="editor">The text editor to which syntax highlighting will be applied. Cannot be null.</param>
    /// <param name="themeName">The theme to use for syntax highlighting. If null, the current Avalonia theme variant is used.</param>
    /// <exception cref="ArgumentNullException">Thrown if editor is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the service has not been initialized. Call Initialize before using this method.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the service has been disposed.</exception>
    public void ApplyTo(TextEditor editor, ThemeName? themeName = null)
    {
        // This is a volatile read
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(editor);

        // Determine theme based on Avalonia's current theme if not specified
        ThemeName effectiveTheme = themeName ?? GetThemeForCurrentVariant();

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            if (!_isInitialized)
            {
                throw new InvalidOperationException($"{nameof(SyntaxHighlightingService)} must be initialized before use. Call {nameof(Initialize)} first.");
            }
        }

        try
        {
            TextMate.Installation? previousInstallation;

            // Create custom registry options with Mermaid grammar
            MermaidRegistryOptions registryOptions = new MermaidRegistryOptions(effectiveTheme);

            // Capture and clear current installation/editor under lock, but do not dispose while holding lock.
            lock (_sync)
            {
                previousInstallation = _textMateInstallation;
                _textMateInstallation = null;
                _currentEditor = null;
            }

            // Dispose previous installation outside the lock to avoid holding the lock during disposal work
            previousInstallation?.Dispose();

            // Confirm not disposed before proceeding
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            // Install TextMate on the editor (may interact with UI; do it outside locks)
            TextMate.Installation installation = editor.InstallTextMate(registryOptions);

            // Set the Mermaid grammar
            installation.SetGrammar(MermaidRegistryOptions.MermaidScopeName);

            // Store references for later theme switching under lock
            lock (_sync)
            {
                if (_isDisposed)
                {
                    // If disposed concurrently, ensure we clean up what we just created
                    // Any exceptions thrown while disposing the new installation are swallowed so they do not mask
                    // the concurrent disposal path. After cleanup, we signal the caller that the service has been
                    // disposed by throwing ObjectDisposedException.
                    try
                    {
                        installation.Dispose();
                    }
                    catch (Exception ex)
                    {
                        // Swallow to avoid throwing during concurrent disposal cleanup
                        SimpleLogger.LogError("Error disposing concurrent installation during ApplyTo cleanup", ex);
                    }

                    throw new ObjectDisposedException(nameof(SyntaxHighlightingService));
                }

                _textMateInstallation = installation;
                _currentEditor = editor;
                _currentTheme = effectiveTheme;
            }
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError("Error applying syntax highlighting", ex);
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
    /// <exception cref="ObjectDisposedException">Thrown if the service has been disposed.</exception>
    public void UpdateThemeForVariant(bool isDarkTheme)
    {
        // This is a volatile read
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        ThemeName newTheme = MermaidRegistryOptions.GetDefaultThemeName(isDarkTheme);

        // Capture editor and current theme under lock so we act on a stable snapshot.
        TextEditor? editorSnapshot;
        ThemeName currentThemeSnapshot;
        lock (_sync)
        {
            editorSnapshot = _currentEditor;
            currentThemeSnapshot = _currentTheme;
        }

        // If nothing to do or editor disappeared, exit.
        if (editorSnapshot is null || currentThemeSnapshot == newTheme)
        {
            return;
        }

        // Use existing ChangeTheme that re-validates state under lock and then performs the reapply safely.
        ChangeTheme(newTheme);
    }

    /// <summary>
    /// Changes the syntax highlighting theme.
    /// </summary>
    /// <param name="themeName">The new theme to apply.</param>
    /// <remarks>
    /// This method re-applies the syntax highlighting with the new theme.
    /// The editor's text and cursor position are preserved.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown if no editor has been initialized with syntax highlighting.</exception>
    private void ChangeTheme(ThemeName themeName)
    {
        TextEditor? editorSnapshot;

        // Capture required state under lock and validate
        lock (_sync)
        {
            editorSnapshot = _currentEditor ?? throw new InvalidOperationException($"No editor has been initialized with syntax highlighting. Call {nameof(ApplyTo)} first.");
            ThemeName currentThemeSnapshot = _currentTheme;

            if (currentThemeSnapshot == themeName)
            {
                SimpleLogger.Log($"Theme is already set to {themeName}, skipping theme change");
                return;
            }

            SimpleLogger.Log($"Changing syntax highlighting theme from {_currentTheme} to {themeName}");
        }

        try
        {
            // Re-apply syntax highlighting with new theme using the captured editor.
            // ApplyTo is thread-safe and does its own synchronization.
            ApplyTo(editorSnapshot, themeName);
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError("Error changing theme", ex);
            throw;
        }
    }

    /// <summary>
    /// Determines the appropriate theme name based on the application's current theme variant.
    /// </summary>
    /// <remarks>If the application's theme variant is not set or is unrecognized, the method defaults to the
    /// dark theme. This ensures consistent appearance even when theme information is unavailable.</remarks>
    /// <returns>A value of <see cref="ThemeName"/> corresponding to the current theme variant. Returns <see
    /// cref="ThemeName.DarkPlus"/> if the theme variant cannot be determined.</returns>
    private static ThemeName GetThemeForCurrentVariant()
    {
        // Try to get the current theme variant from the application
        ThemeVariant? actualThemeVariant = Avalonia.Application.Current?.ActualThemeVariant;
        if (actualThemeVariant == ThemeVariant.Dark)
        {
            return ThemeName.DarkPlus;
        }

        if (actualThemeVariant == ThemeVariant.Light)
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
    /// <exception cref="FileNotFoundException">Thrown if the grammar resource cannot be found.</exception>
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
    }

    /// <summary>
    /// Releases all resources used by the current instance of the class.
    /// </summary>
    /// <remarks>This method should NOT be called manually; the lifetime of SyntaxHighlightingService is controlled
    /// by dependency injection which will call this method automatically when appropriate.
    /// After calling this method, the instance is in an unusable state and should not be accessed.</remarks>
    public void Dispose()
    {
        TextMate.Installation? toDispose;

        lock (_sync)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            toDispose = _textMateInstallation;
            _textMateInstallation = null;
            _currentEditor = null;
        }

        // Dispose outside lock to avoid deadlocks or long blocking sections
        try
        {
            toDispose?.Dispose();
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError("Error disposing syntax highlighting installation", ex);
        }
    }
}
