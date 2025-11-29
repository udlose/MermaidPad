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

using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using AvaloniaEdit;
using MermaidPad.Models;
using MermaidPad.Services.Highlighting;
using Microsoft.Extensions.Logging;
using TextMateSharp.Grammars;

namespace MermaidPad.Services.Theming;

/// <summary>
/// Provides functionality for managing and applying application and editor themes, including retrieving available
/// themes, applying selected themes, and obtaining display names for themes.
/// </summary>
/// <remarks>The IThemeService interface enables consistent theme management across the application, allowing both
/// UI and editor appearance to be customized. Implementations may persist theme selections and ensure that changes are
/// reflected throughout the user interface. Thread safety and persistence behavior depend on the specific
/// implementation.</remarks>
public interface IThemeService
{
    /// <summary>
    /// Gets the current application theme.
    /// </summary>
    ApplicationTheme CurrentApplicationTheme { get; }

    /// <summary>
    /// Gets the current editor theme.
    /// </summary>
    ThemeName CurrentEditorTheme { get; }

    /// <summary>
    /// Initializes the theme service and applies the default or saved theme.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Applies an application theme (UI colors).
    /// </summary>
    /// <param name="theme">The application theme to apply.</param>
    void ApplyApplicationTheme(ApplicationTheme theme);

    /// <summary>
    /// Applies an editor theme (syntax highlighting).
    /// </summary>
    /// <param name="editor">The text editor to apply the theme to.</param>
    /// <param name="theme">The editor theme to apply.</param>
    void ApplyEditorTheme(TextEditor editor, ThemeName theme);

    /// <summary>
    /// Gets all available application themes.
    /// </summary>
    /// <returns>An array of all application themes.</returns>
    ApplicationTheme[] GetAvailableApplicationThemes();

    /// <summary>
    /// Gets all available editor themes from TextMateSharp.
    /// </summary>
    /// <returns>An array of all editor themes.</returns>
    ThemeName[] GetAvailableEditorThemes();

    /// <summary>
    /// Gets a friendly display name for an application theme.
    /// </summary>
    /// <param name="theme">The theme to get a display name for.</param>
    /// <returns>A user-friendly display name.</returns>
    string GetApplicationThemeDisplayName(ApplicationTheme theme);

    /// <summary>
    /// Gets a friendly display name for an editor theme.
    /// </summary>
    /// <param name="theme">The theme to get a display name for.</param>
    /// <returns>A user-friendly display name.</returns>
    string GetEditorThemeDisplayName(ThemeName theme);

    /// <summary>
    /// Determines whether an application theme is a dark theme.
    /// </summary>
    /// <param name="theme">The theme to check.</param>
    /// <returns>True if the theme is dark; otherwise, false.</returns>
    bool IsDarkTheme(ApplicationTheme theme);
}

/// <summary>
/// Provides functionality for managing and applying application and editor themes, including initialization, theme
/// selection, and retrieval of available themes and display names.
/// </summary>
/// <remarks>ThemeService coordinates theme settings for both the application's user interface and the code
/// editor, persisting user preferences and applying changes at runtime. It exposes methods to initialize themes based
/// on saved settings or system defaults, apply new themes, and retrieve available options. This service is intended to
/// be used as a singleton and is not thread-safe; concurrent calls may result in inconsistent state.</remarks>
public sealed class ThemeService : IThemeService
{
    private readonly ILogger<ThemeService> _logger;
    private readonly SettingsService _settingsService;
    private readonly SyntaxHighlightingService _syntaxHighlightingService;

    public ApplicationTheme CurrentApplicationTheme { get; private set; }
    public ThemeName CurrentEditorTheme { get; private set; }

    /// <summary>
    /// Initializes a new instance of the ThemeService class with the specified logging, settings, and syntax
    /// highlighting services.
    /// </summary>
    /// <param name="logger">The logger used to record diagnostic and operational information for the ThemeService.</param>
    /// <param name="settingsService">The service that provides access to application settings relevant to theme management.</param>
    /// <param name="syntaxHighlightingService">The service responsible for applying syntax highlighting based on the current theme.</param>
    public ThemeService(ILogger<ThemeService> logger, SettingsService settingsService,
        SyntaxHighlightingService syntaxHighlightingService)
    {
        _logger = logger;
        _settingsService = settingsService;
        _syntaxHighlightingService = syntaxHighlightingService;
    }

    /// <summary>
    /// Initializes the theme service by applying the appropriate application and editor themes based on user settings
    /// or system defaults.
    /// </summary>
    /// <remarks>This method should be called during application startup to ensure that the visual appearance
    /// matches user preferences or system theme. If no user preferences are found, default themes are selected based on
    /// the operating system or application theme. Subsequent calls will reapply the current theme settings.</remarks>
    public void Initialize()
    {
        _logger.LogInformation("Initializing ThemeService...");

        AppSettings settings = _settingsService.Settings;

        // Determine application theme
        ApplicationTheme appTheme;
        if (!string.IsNullOrWhiteSpace(settings.SelectedApplicationTheme) &&
            Enum.TryParse<ApplicationTheme>(settings.SelectedApplicationTheme, out ApplicationTheme savedAppTheme))
        {
            appTheme = savedAppTheme;
            _logger.LogInformation("Loaded saved application theme: {AppTheme}", appTheme);
        }
        else
        {
            // Default based on OS theme
            ThemeVariant? osThemeVariant = Application.Current?.ActualThemeVariant;
            appTheme = osThemeVariant == ThemeVariant.Dark
                ? ApplicationTheme.VS2022Dark
                : ApplicationTheme.StudioLight;
            _logger.LogInformation("No saved theme found. Using default: {AppTheme}", appTheme);
        }

        // Determine editor theme
        ThemeName editorTheme;
        if (!string.IsNullOrWhiteSpace(settings.SelectedEditorTheme) &&
            Enum.TryParse<ThemeName>(settings.SelectedEditorTheme, out ThemeName savedEditorTheme))
        {
            editorTheme = savedEditorTheme;
            _logger.LogInformation("Loaded saved editor theme: {EditorTheme}", editorTheme);
        }
        else
        {
            // Default based on application theme
            editorTheme = IsDarkTheme(appTheme) ? ThemeName.DarkPlus : ThemeName.Light;
            _logger.LogInformation("No saved editor theme found. Using default: {EditorTheme}", editorTheme);
        }

        // Apply themes
        ApplyApplicationTheme(appTheme);
        //TODO DaveBlack: re-attach this from the MainWindowViewModel
        ApplyEditorTheme(editor, editorTheme);

        _logger.LogInformation("ThemeService initialized successfully.");
    }

    /// <summary>
    /// Applies the specified application theme and updates the current theme setting.
    /// </summary>
    /// <remarks>This method updates the application's visual appearance and persists the selected theme to
    /// user settings. If the theme is successfully applied, subsequent calls to retrieve the current theme will reflect
    /// the new value.</remarks>
    /// <param name="theme">The application theme to apply. Must be a valid value of the <see cref="ApplicationTheme"/> enumeration.</param>
    public void ApplyApplicationTheme(ApplicationTheme theme)
    {
        _logger.LogInformation("Applying application theme: {AppTheme}", theme);

        try
        {
            ApplyApplicationThemeInternal(theme);
            CurrentApplicationTheme = theme;

            // Save to settings
            AppSettings settings = _settingsService.Settings;
            settings.SelectedApplicationTheme = theme.ToString();
            _settingsService.Save();

            _logger.LogInformation("Application theme applied and saved: {AppTheme}", theme);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply application theme: {Theme}", theme);
            throw;
        }
    }

    /// <summary>
    /// Applies the specified editor theme to the syntax highlighting service and updates the current editor theme
    /// setting.
    /// </summary>
    /// <remarks>The selected theme is persisted to application settings and will be used for subsequent
    /// editor sessions. If the theme cannot be applied, an exception is logged and rethrown.</remarks>
    /// <param name="editor">The text editor to which the theme will be applied.</param>
    /// <param name="theme">The theme to apply to the editor. Must be a valid value of <see cref="ThemeName"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown if editor is null.</exception>
    public void ApplyEditorTheme(TextEditor editor, ThemeName theme)
    {
        ArgumentNullException.ThrowIfNull(editor);
        _logger.LogInformation("Applying editor theme: {Theme}", theme);

        try
        {
            _syntaxHighlightingService.ApplyTo(editor, theme);
            CurrentEditorTheme = theme;

            // Save to settings
            AppSettings settings = _settingsService.Settings;
            settings.SelectedEditorTheme = theme.ToString();
            _settingsService.Save();

            _logger.LogInformation("Editor theme applied and saved: {EditorTheme}", theme);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply editor theme: {EditorTheme}", theme);
            throw;
        }
    }

    /// <summary>
    /// Retrieves all available application themes supported by the current version.
    /// </summary>
    /// <remarks>The returned array reflects the themes defined in the <see cref="ApplicationTheme"/>
    /// enumeration. This method is useful for populating theme selection controls or for enumerating supported themes
    /// at runtime.</remarks>
    /// <returns>An array of <see cref="ApplicationTheme"/> values representing the available themes. The array will contain all
    /// defined themes; it will be empty only if no themes are defined.</returns>
    public ApplicationTheme[] GetAvailableApplicationThemes()
    {
        return Enum.GetValues<ApplicationTheme>();
    }

    /// <summary>
    /// Retrieves all available editor themes supported by the application.
    /// </summary>
    /// <returns>An array of <see cref="ThemeName"/> values representing the available editor themes. The array will be empty if
    /// no themes are defined.</returns>
    public ThemeName[] GetAvailableEditorThemes()
    {
        return Enum.GetValues<ThemeName>();
    }

    /// <summary>
    /// Returns the display name associated with the specified application theme.
    /// </summary>
    /// <param name="theme">The application theme for which to retrieve the display name.</param>
    /// <returns>A string containing the display name of the specified theme. If the theme is not recognized, returns the string
    /// representation of the theme value.</returns>
    public string GetApplicationThemeDisplayName(ApplicationTheme theme)
    {
        return theme switch
        {
            ApplicationTheme.StudioLight => "Studio Light",
            ApplicationTheme.ProfessionalGray => "Professional Gray",
            ApplicationTheme.SoftContrast => "Soft Contrast",
            ApplicationTheme.VSDark => "VS Dark",
            ApplicationTheme.MidnightDeveloper => "Midnight Developer",
            ApplicationTheme.CharcoalPro => "Charcoal Pro",
            ApplicationTheme.VS2022Dark => "VS 2022 Dark",
            ApplicationTheme.StudioLight3D => "Studio Light 3D",
            ApplicationTheme.ProfessionalGray3D => "Professional Gray 3D",
            ApplicationTheme.SoftContrast3D => "Soft Contrast 3D",
            ApplicationTheme.VSDark3D => "VS Dark 3D",
            ApplicationTheme.MidnightDeveloper3D => "Midnight Developer 3D",
            ApplicationTheme.CharcoalPro3D => "Charcoal Pro 3D",
            ApplicationTheme.VS2022Dark3D => "VS 2022 Dark 3D",
            _ => theme.ToString()
        };
    }

    /// <summary>
    /// Returns the display name associated with the specified editor theme.
    /// </summary>
    /// <param name="theme">The theme for which to retrieve the display name.</param>
    /// <returns>A string containing the display name of the specified theme. If the theme is not recognized, returns the string
    /// representation of the theme value.</returns>
    public string GetEditorThemeDisplayName(ThemeName theme)
    {
        return theme switch
        {
            ThemeName.DarkPlus => "Dark+",
            ThemeName.KimbieDark => "Kimbie Dark",
            ThemeName.Light => "Light",
            ThemeName.LightPlus => "Light+",
            ThemeName.Monokai => "Monokai",
            ThemeName.DimmedMonokai => "Monokai Dimmed",
            ThemeName.OneDark => "One Dark",
            ThemeName.QuietLight => "Quiet Light",
            ThemeName.SolarizedDark => "Solarized Dark",
            ThemeName.SolarizedLight => "Solarized Light",
            ThemeName.TomorrowNightBlue => "Tomorrow Night Blue",
            ThemeName.HighContrastLight => "High Contrast Light",
            ThemeName.HighContrastDark => "High Contrast Dark",
            ThemeName.AtomOneLight => "Atom One Light",
            ThemeName.AtomOneDark => "Atom One Dark",
            ThemeName.VisualStudioLight => "Visual Studio Light",
            ThemeName.VisualStudioDark => "Visual Studio Dark",
            _ => theme.ToString()
        };
    }

    /// <summary>
    /// Determines whether the specified application theme is considered a dark theme.
    /// </summary>
    /// <remarks>Use this method to check if a given theme should be treated as dark for purposes such as UI
    /// styling or accessibility. The classification is based on predefined theme values and may not reflect custom or
    /// user-defined themes.</remarks>
    /// <param name="theme">The application theme to evaluate.</param>
    /// <returns>true if the specified theme is classified as a dark theme; otherwise, false.</returns>
    public bool IsDarkTheme(ApplicationTheme theme)
    {
        return theme switch
        {
            ApplicationTheme.VSDark => true,
            ApplicationTheme.MidnightDeveloper => true,
            ApplicationTheme.CharcoalPro => true,
            ApplicationTheme.VS2022Dark => true,
            ApplicationTheme.VSDark3D => true,
            ApplicationTheme.MidnightDeveloper3D => true,
            ApplicationTheme.CharcoalPro3D => true,
            ApplicationTheme.VS2022Dark3D => true,
            _ => false
        };
    }

    /// <summary>
    /// Applies the specified application theme by updating the merged resource dictionaries for the current application
    /// instance.
    /// </summary>
    /// <remarks>If <see cref="Application.Current"/> is <see langword="null"/>, the theme will not be applied
    /// and no changes will be made. Existing application theme resources are replaced with the new theme. This method
    /// logs informational and error messages during the theme application process.</remarks>
    /// <param name="theme">The theme to apply to the application. Determines which resource dictionary is loaded. Must be a valid value of
    /// the <see cref="ApplicationTheme"/> enumeration.</param>
    private void ApplyApplicationThemeInternal(ApplicationTheme theme)
    {
        if (Application.Current is null)
        {
            _logger.LogInformation("Cannot apply theme: Application.Current is null");
            return;
        }

        try
        {
            // Determine the theme category (Light or Dark)
            string themeCategory = IsDarkTheme(theme) ? "Dark" : "Light";

            // Construct the resource URI for the theme
            string themePath = $"avares://MermaidPad/Resources/Themes/ApplicationThemes/{themeCategory}/{theme}.axaml";

            _logger.LogInformation("Loading application theme from: {ThemePath}", themePath);

            // Create a new ResourceInclude for the theme
            ResourceInclude themeResource = new ResourceInclude(new Uri(themePath))
            {
                Source = new Uri(themePath)
            };

            // Remove any existing application theme resources
            for (int i = Application.Current.Resources.MergedDictionaries.Count - 1; i >= 0; i--)
            {
                if (Application.Current.Resources.MergedDictionaries[i] is ResourceInclude ri &&
                    ri.Source?.ToString().Contains("/Resources/Themes/ApplicationThemes/") == true)
                {
                    Application.Current.Resources.MergedDictionaries.RemoveAt(i);
                }
            }

            // Add the new theme resource
            Application.Current.Resources.MergedDictionaries.Add(themeResource);

            _logger.LogInformation("Successfully applied application theme: {Theme}", theme);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading application theme {Theme}", theme);
            throw;
        }
    }
}
