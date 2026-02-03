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
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
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
[SuppressMessage("ReSharper", "GCSuppressFinalizeForTypeWithoutDestructor")]
public sealed class SyntaxHighlightingService : IDisposable, IAsyncDisposable
{
    private readonly ILogger<SyntaxHighlightingService> _logger;
    private readonly Lock _sync = new Lock();
    private TextMate.Installation? _textMateInstallation;
    private TextEditor? _currentEditor;
    private ThemeName _currentTheme;
    private volatile bool _isInitialized;
    private volatile bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyntaxHighlightingService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for structured logging.</param>
    public SyntaxHighlightingService(ILogger<SyntaxHighlightingService> logger)
    {
        _logger = logger;
    }

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
            _logger.LogDebug("Syntax highlighting service already initialized");
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
                    _logger.LogDebug("Syntax highlighting service already initialized (post-verify)");
                    return;
                }

                _isInitialized = true;
                _logger.LogInformation("Syntax highlighting service initialized successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize syntax highlighting service");
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
    /// <param name="cancellationToken">A cancellation token used while dispatching work to the Avalonia UI thread.</param>
    /// <exception cref="ArgumentNullException">Thrown if editor is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the service has not been initialized. Call Initialize before using this method.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the service has been disposed.</exception>
    public async Task ApplyToAsync(TextEditor editor, ThemeName? themeName = null, CancellationToken cancellationToken = default)
    {
        // This is a volatile read
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(editor);

        if (Dispatcher.UIThread.CheckAccess())
        {
            ApplyToOnUIThread(editor, themeName);
            return;
        }

        Task applyTask = Dispatcher.UIThread.InvokeAsync(() => ApplyToOnUIThread(editor, themeName),
            DispatcherPriority.Normal,
            cancellationToken).GetTask();

        await applyTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Updates the syntax highlighting theme based on Avalonia's current theme variant.
    /// </summary>
    /// <param name="isDarkTheme">True if the current theme is dark, false if light.</param>
    /// <param name="cancellationToken">A cancellation token used while dispatching work to the Avalonia UI thread.</param>
    /// <remarks>
    /// This method is typically called when the application theme changes.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Thrown if the service has been disposed.</exception>
    public async Task UpdateThemeForVariantAsync(bool isDarkTheme, CancellationToken cancellationToken = default)
    {
        // This is a volatile read
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (Dispatcher.UIThread.CheckAccess())
        {
            UpdateThemeForVariantOnUIThread(isDarkTheme);
            return;
        }

        Task updateThemeTask = Dispatcher.UIThread.InvokeAsync(
            () => UpdateThemeForVariantOnUIThread(isDarkTheme),
            DispatcherPriority.Normal,
            cancellationToken).GetTask();

        await updateThemeTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Applies Mermaid syntax highlighting to the specified text editor on the UI thread, using the given theme or the
    /// current application theme if none is specified.
    /// </summary>
    /// <remarks>This method must be called on the UI thread. Any existing syntax highlighting installation on
    /// the editor will be replaced. If the service is disposed concurrently during execution, the method ensures proper
    /// cleanup and signals disposal by throwing an ObjectDisposedException.</remarks>
    /// <param name="editor">The text editor control to which syntax highlighting will be applied. Cannot be null.</param>
    /// <param name="themeName">The theme to use for syntax highlighting. If null, the current application theme is used.</param>
    /// <exception cref="InvalidOperationException">Thrown if the service has not been initialized before calling this method.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the service has been disposed before or during the operation.</exception>
    private void ApplyToOnUIThread(TextEditor editor, ThemeName? themeName)
    {
        Dispatcher.UIThread.VerifyAccess();

        // This is a volatile read
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(editor);

        // Determine theme based on Avalonia's current theme if not specified
        ThemeName effectiveTheme = themeName ?? GetThemeForCurrentVariant();

        TextMate.Installation? currentInstallationSnapshot = null;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            if (!_isInitialized)
            {
                throw new InvalidOperationException($"{nameof(SyntaxHighlightingService)} must be initialized before use. Call {nameof(Initialize)} first.");
            }

            // If we already have an installation for this exact editor, DO NOT reinstall.
            // Reinstalling + disposing the previous installation can break highlighting because AvaloniaEdit.TextMate
            // may share underlying editor hooks/resources across installation instances for the same editor.
            //
            // Instead, update the theme on the existing installation.
            if (_textMateInstallation is not null && ReferenceEquals(_currentEditor, editor))
            {
                currentInstallationSnapshot = _textMateInstallation;
                _currentTheme = effectiveTheme;
            }
        }

        // If we have an existing installation, update its theme outside the lock
        if (currentInstallationSnapshot is not null)
        {
            try
            {
                MermaidRegistryOptions registryOptions = new MermaidRegistryOptions(effectiveTheme);

                currentInstallationSnapshot.SetTheme(registryOptions.GetTheme(MermaidRegistryOptions.MermaidScopeName));
                currentInstallationSnapshot.SetGrammar(MermaidRegistryOptions.MermaidScopeName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating syntax highlighting theme");
                throw;
            }
        }
        else
        {
            // Otherwise, this is the first time we're applying to this editor instance: install TextMate once
            CreateNewTextMateInstallation(editor, effectiveTheme);
        }
    }

    /// <summary>
    /// Initializes and attaches a new TextMate syntax highlighting installation to the specified text editor using the given theme.
    /// </summary>
    /// <remarks>This method is intended for use in single-document interface (SDI) scenarios. Attempting to
    /// attach the service to multiple editors concurrently is not supported and will result in an exception. For
    /// multi-document interface (MDI) support, the service must be redesigned to track multiple editor
    /// instances.</remarks>
    /// <param name="editor">The text editor instance to which the TextMate installation will be applied. Must not already have an active
    /// installation from this service.</param>
    /// <param name="effectiveTheme">The theme to use for configuring syntax highlighting in the new TextMate installation.</param>
    /// <exception cref="InvalidOperationException">Thrown if the service is already attached to a different text editor instance.
    /// This service supports only a single editor (SDI) configuration.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the service has been disposed before or during the installation process.</exception>
    private void CreateNewTextMateInstallation(TextEditor editor, ThemeName effectiveTheme)
    {
        TextMate.Installation? newInstallation = null;

        try
        {
            MermaidRegistryOptions registryOptions = new MermaidRegistryOptions(effectiveTheme);

            // Install TextMate on the editor (may interact with UI; do it outside locks)
            newInstallation = editor.InstallTextMate(registryOptions);

            // Set the Mermaid grammar
            newInstallation.SetGrammar(MermaidRegistryOptions.MermaidScopeName);

            lock (_sync)
            {
                // When the TextEditor is re-created from a Docking operation (pin/unpin), we will be creating a new installation
                if (_currentEditor is not null && !ReferenceEquals(_currentEditor, editor) && _textMateInstallation is not null)
                {
                    // Dispose the _textMateInstallation
                    DisposeInstallationSafely(_textMateInstallation);
                }

                if (_isDisposed)
                {
                    // If disposed concurrently, ensure we clean up what we just created
                    // Any exceptions thrown while disposing the new installation are swallowed so they do not mask
                    // the concurrent disposal path. After cleanup, we signal the caller that the service has been
                    // disposed by throwing ObjectDisposedException.
                    DisposeInstallationSafely(newInstallation, "Error disposing concurrent installation during ApplyTo cleanup");

                    throw new ObjectDisposedException(nameof(SyntaxHighlightingService));
                }

                _textMateInstallation = newInstallation;
                _currentEditor = editor;
                _currentTheme = effectiveTheme;

                // Prevent finally from disposing the committed installation
                newInstallation = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying syntax highlighting");
            throw;
        }
        finally
        {
            if (newInstallation is not null)
            {
                DisposeInstallationSafely(newInstallation);
            }
        }
    }

    /// <summary>
    /// Updates the editor's theme to match the specified light or dark variant on the UI thread.
    /// </summary>
    /// <remarks>This method must be called on the UI thread. If the editor is not available or the requested
    /// theme is already applied, no action is taken.</remarks>
    /// <param name="isDarkTheme">A value indicating whether to apply the dark theme variant.
    /// Pass <see langword="true"/> to apply the dark theme; otherwise, the light theme is applied.</param>
    private void UpdateThemeForVariantOnUIThread(bool isDarkTheme)
    {
        Dispatcher.UIThread.VerifyAccess();

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
        ChangeThemeOnUIThread(newTheme);
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
    private void ChangeThemeOnUIThread(ThemeName themeName)
    {
        Dispatcher.UIThread.VerifyAccess();

        TextEditor? editorSnapshot;

        // Capture required state under lock and validate
        lock (_sync)
        {
            editorSnapshot = _currentEditor ??
                throw new InvalidOperationException($"No editor has been initialized with syntax highlighting. Call {nameof(ApplyToAsync)} first.");

            ThemeName currentThemeSnapshot = _currentTheme;
            if (currentThemeSnapshot == themeName)
            {
                _logger.LogDebug("Theme is already set to {ThemeName}, skipping theme change", themeName);
                return;
            }

            _logger.LogInformation("Changing syntax highlighting theme from {CurrentTheme} to {NewTheme}", _currentTheme, themeName);
        }

        try
        {
            // Re-apply syntax highlighting with new theme using the captured editor.
            // ApplyToOnUIThread is thread-safe and does its own synchronization.
            ApplyToOnUIThread(editorSnapshot, themeName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing theme");
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
    private ThemeName GetThemeForCurrentVariant()
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
        _logger.LogWarning("Could not determine theme variant, defaulting to {DefaultTheme}", nameof(ThemeName.DarkPlus));
        return ThemeName.DarkPlus;
    }

    /// <summary>
    /// Verifies that the Mermaid grammar resource exists in the assembly.
    /// </summary>
    /// <exception cref="FileNotFoundException">Thrown if the grammar resource cannot be found.</exception>
    private static void VerifyGrammarResource()
    {
        Assembly assembly = typeof(SyntaxHighlightingService).Assembly;

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
    /// <remarks>
    /// <para>
    /// This method should NOT be called manually; the lifetime of SyntaxHighlightingService is controlled
    /// by dependency injection which will call this method automatically when appropriate.
    /// After calling this method, the instance is in an unusable state and should not be accessed.
    /// </para>
    /// <para>
    /// Implemented according to: https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-disposeasync#implement-both-dispose-and-async-dispose-patterns
    /// </para>
    /// </remarks>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Asynchronously releases all resources used by the current instance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Call this method to clean up resources when the instance is no longer needed. This method is
    /// thread-safe and can be called multiple times; subsequent calls after the first have no effect.
    /// After calling this method, the instance is in an unusable state and should not be accessed.
    /// </para>
    /// <para>
    /// Implemented according to: https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-disposeasync#implement-both-dispose-and-async-dispose-patterns
    /// </para>
    /// </remarks>
    /// <returns>A task that represents the asynchronous dispose operation.</returns>
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);

        Dispose(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the object and, optionally, releases the managed resources.
    /// </summary>
    /// <remarks>
    /// <para>This method is typically called by the public Dispose() method and a finalizer. When
    /// disposing is true, this method disposes all resources held by managed objects.
    /// When disposing is false, only unmanaged resources are released. Override this method to
    /// provide custom disposal logic for derived classes.
    /// </para>
    /// <para>
    /// Implemented according to: https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-disposeasync#implement-both-dispose-and-async-dispose-patterns
    /// </para>
    /// </remarks>
    /// <param name="disposing">true to release both managed and unmanaged resources;
    /// false to release only unmanaged resources.</param>
    private void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        TextMate.Installation? installationToDispose = TryBeginDispose();
        if (installationToDispose is null)
        {
            return;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            DisposeInstallationSafely(installationToDispose);
            return;
        }

        // Dispose() is a synchronous API; we do not block threads here
        // We schedule disposal on the UI thread as a best-effort fallback
        Dispatcher.UIThread.Post(state =>
            {
                TextMate.Installation installation = (TextMate.Installation)state!;
                DisposeInstallationSafely(installation);
            },
            installationToDispose,
            DispatcherPriority.Normal);
    }

    /// <summary>
    /// Performs the core asynchronous logic for releasing resources used by the installation, ensuring disposal occurs
    /// on the UI thread if required.
    /// </summary>
    /// <remarks>
    /// <para>This method should be called as part of the asynchronous dispose pattern to ensure that
    /// resources are released safely, especially when UI thread access is necessary. It is intended to be invoked by
    /// DisposeAsync implementations and not called directly by user code.
    /// </para>
    /// <para>
    /// Implemented according to: https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-disposeasync#implement-both-dispose-and-async-dispose-patterns
    /// </para>
    /// </remarks>
    /// <returns>A ValueTask that represents the asynchronous dispose operation.</returns>
    private async ValueTask DisposeAsyncCore()
    {
        TextMate.Installation? installationToDispose = TryBeginDispose();
        if (installationToDispose is null)
        {
            return;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            DisposeInstallationSafely(installationToDispose);
            return;
        }

        Task disposeTask = Dispatcher.UIThread.InvokeAsync(
            () => DisposeInstallationSafely(installationToDispose),
            DispatcherPriority.Normal,
            CancellationToken.None).GetTask();

        await disposeTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Attempts to initiate the disposal process and returns the associated TextMate installation if disposal has not
    /// already begun.
    /// </summary>
    /// <returns>The TextMate installation to be disposed if disposal is initiated; otherwise,
    /// null if disposal has already started.</returns>
    private TextMate.Installation? TryBeginDispose()
    {
        lock (_sync)
        {
            if (_isDisposed)
            {
                return null;
            }

            _isDisposed = true;

            TextMate.Installation? installationToDispose = _textMateInstallation;
            _textMateInstallation = null;
            _currentEditor = null;

            return installationToDispose;
        }
    }

    /// <summary>
    /// Disposes the specified syntax highlighting installation, suppressing any exceptions that occur during disposal.
    /// </summary>
    /// <remarks>Any exceptions thrown during disposal are caught and logged. This method is intended to
    /// ensure that disposal does not interrupt the calling workflow.</remarks>
    /// <param name="installationToDispose">The installation instance to dispose. Cannot be null.</param>
    /// <param name="messageOnError">The error message to log in case of disposal failure.</param>
    private void DisposeInstallationSafely(TextMate.Installation installationToDispose,
        string messageOnError = "Error disposing syntax highlighting installation")
    {
        try
        {
            installationToDispose.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Message}", messageOnError);
        }
    }
}
