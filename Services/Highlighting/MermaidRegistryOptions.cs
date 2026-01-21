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

using System.Reflection;
using System.Text;
using TextMateSharp.Grammars;
using TextMateSharp.Internal.Grammars.Reader;
using TextMateSharp.Internal.Themes.Reader;
using TextMateSharp.Internal.Types;
using TextMateSharp.Registry;
using TextMateSharp.Themes;

namespace MermaidPad.Services.Highlighting;

/// <summary>
/// Custom TextMate registry options for loading the Mermaid grammar.
/// Implements IRegistryOptions to provide grammar and theme loading for TextMate syntax highlighting.
/// </summary>
/// <remarks>
/// This class loads the Mermaid grammar from embedded resources and provides theme support
/// for both light and dark modes. The grammar is loaded lazily to improve startup performance.
/// </remarks>
public sealed class MermaidRegistryOptions : IRegistryOptions
{
    internal const string MermaidScopeName = "source.mermaid";
    internal const string GrammarResourceName = "MermaidPad.Resources.Grammars.mermaid.tmLanguage.json";

    private readonly IRawTheme _theme;
    private readonly Lazy<IRawGrammar> _mermaidGrammar;

    /// <summary>
    /// Initializes a new instance of the <see cref="MermaidRegistryOptions"/> class.
    /// </summary>
    /// <param name="themeName">The TextMate theme to use for syntax highlighting.</param>
    public MermaidRegistryOptions(ThemeName themeName)
    {
        _theme = LoadTheme(themeName);
        _mermaidGrammar = new Lazy<IRawGrammar>(LoadMermaidGrammar);
    }

    /// <summary>
    /// Gets the grammar for the specified scope name.
    /// </summary>
    /// <param name="scopeName">The scope name to get the grammar for.</param>
    /// <returns>The raw grammar if the scope is "source.mermaid", otherwise null.</returns>
    /// <remarks><see cref="IRegistryOptions"/> defines this method to return non-nullable, so we must do the same.
    /// See: https://github.com/danipen/TextMateSharp/blob/1287b30befcda4750229697677ee0c049c6f1b9c/src/TextMateSharp/Registry/IRegistryOptions.cs#L10</remarks>
    public IRawGrammar GetGrammar(string scopeName)
    {
#pragma warning disable CS8603 // Possible null reference return.
        return scopeName == MermaidScopeName ? _mermaidGrammar.Value : null;
#pragma warning restore CS8603 // Possible null reference return.
    }

    /// <summary>
    /// Gets the theme for syntax highlighting.
    /// </summary>
    /// <param name="scopeName">The scope name (unused, same theme for all scopes).</param>
    /// <returns>The configured theme.</returns>
    public IRawTheme GetTheme(string scopeName) => _theme;

    /// <summary>
    /// Gets grammar injections for the specified scope.
    /// </summary>
    /// <param name="scopeName">The scope name.</param>
    /// <returns>Always returns an empty collection, as Mermaid doesn't use injections.</returns>
    public ICollection<string> GetInjections(string scopeName) => Array.Empty<string>();

    /// <summary>
    /// Gets the default theme name based on Avalonia's current theme variant.
    /// </summary>
    /// <param name="isDarkTheme">Whether the current theme is dark.</param>
    /// <returns>The appropriate theme name for the current theme variant.</returns>
    public static ThemeName GetDefaultThemeName(bool isDarkTheme)
    {
        return isDarkTheme ? ThemeName.DarkPlus : ThemeName.Light;
    }

    /// <summary>
    /// Returns the default theme used by the application.
    /// </summary>
    /// <returns>An <see cref="IRawTheme"/> instance representing the application's default theme.</returns>
    public IRawTheme GetDefaultTheme() => _theme;

    /// <summary>
    /// Loads the Mermaid grammar from embedded resources.
    /// </summary>
    /// <returns>The loaded Mermaid grammar.</returns>
    /// <exception cref="FileNotFoundException">Thrown if the grammar resource cannot be found.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the grammar cannot be parsed.</exception>
    private static IRawGrammar LoadMermaidGrammar()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();

        using Stream stream = assembly.GetManifestResourceStream(GrammarResourceName)
            ?? throw new FileNotFoundException(
                $"Mermaid grammar resource not found. Expected resource: {GrammarResourceName}. Ensure the file is marked as EmbeddedResource in the project file.");

        using StreamReader reader = new StreamReader(stream);
        string grammarJson = reader.ReadToEnd();

        try
        {
            byte[] grammarAsBytes = Encoding.UTF8.GetBytes(grammarJson);
            using MemoryStream themeStream = new MemoryStream(grammarAsBytes);
            using StreamReader themeReader = new StreamReader(themeStream);
            return GrammarReader.ReadGrammarSync(themeReader);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse Mermaid grammar. The grammar file may be corrupted or invalid. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Loads a TextMate theme based on the theme name.
    /// </summary>
    /// <param name="themeName">The name of the theme to load.</param>
    /// <returns>The loaded theme.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the theme cannot be loaded.</exception>
    /// <exception cref="FileNotFoundException">Thrown if the theme resource cannot be found.</exception>
    private static IRawTheme LoadTheme(ThemeName themeName)
    {
        try
        {
            // Use the built-in theme loader from TextMateSharp.Grammars
            string themeFileName = GetThemeFile(themeName);

            // Load theme from TextMateSharp.Grammars embedded resources
            Assembly assembly = typeof(RegistryOptions).Assembly;
            string resourceName = $"TextMateSharp.Grammars.Resources.Themes.{themeFileName}";

            using Stream stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new FileNotFoundException($"Theme resource not found: {resourceName}");

            using StreamReader reader = new StreamReader(stream);
            string themeJson = reader.ReadToEnd();

            using MemoryStream themeStream = new MemoryStream(Encoding.UTF8.GetBytes(themeJson));
            using StreamReader themeReader = new StreamReader(themeStream);
            return ThemeReader.ReadThemeSync(themeReader);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load TextMate theme '{themeName}'. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Retrieves the file name of the JSON theme file corresponding to the specified theme.
    /// </summary>
    /// <remarks>This method maps each <see cref="ThemeName"/> value to a predefined JSON file name. The
    /// returned file name can be used to load the corresponding theme configuration. If the provided theme name does
    /// not match any predefined value, the method defaults to returning the "dark_plus.json" theme file.</remarks>
    /// <param name="name">The <see cref="ThemeName"/> enumeration value representing the desired theme.</param>
    /// <returns>The file name of the JSON theme file associated with the specified theme. If the theme is not recognized, the
    /// default theme file <c>"dark_plus.json"</c> is returned.</returns>
    private static string GetThemeFile(ThemeName name)
    {
        const string darkPlusThemeFile = "dark_plus.json";
        return name switch
        {
            ThemeName.Abbys => "abyss-color-theme.json",
            ThemeName.Dark => "dark_vs.json",
            ThemeName.DarkPlus => darkPlusThemeFile,
            ThemeName.DimmedMonokai => "dimmed-monokai-color-theme.json",
            ThemeName.KimbieDark => "kimbie-dark-color-theme.json",
            ThemeName.Light => "light_vs.json",
            ThemeName.LightPlus => "light_plus.json",
            ThemeName.Monokai => "monokai-color-theme.json",
            ThemeName.OneDark => "onedark-color-theme.json",
            ThemeName.QuietLight => "quietlight-color-theme.json",
            ThemeName.Red => "Red-color-theme.json",
            ThemeName.SolarizedDark => "solarized-dark-color-theme.json",
            ThemeName.SolarizedLight => "solarized-light-color-theme.json",
            ThemeName.TomorrowNightBlue => "tomorrow-night-blue-color-theme.json",
            ThemeName.HighContrastLight => "hc_light.json",
            ThemeName.HighContrastDark => "hc_black.json",
            ThemeName.Dracula => "dracula-color-theme.json",
            ThemeName.AtomOneLight => "atom-one-light-color-theme.json",
            ThemeName.AtomOneDark => "atom-one-dark-color-theme.json",
            ThemeName.VisualStudioLight => "visual-studio-light-theme.json",
            ThemeName.VisualStudioDark => "visual-studio-dark-theme.json",
            _ => darkPlusThemeFile,
        };
    }
}
