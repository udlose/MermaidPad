using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using MermaidPad.ViewModels.MainWindow;
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using TextMateSharp.Grammars;
using TextMateSharp.Internal.Types;

namespace MermaidPad.Views;
public partial class MainWindow : Window
{
    private TextEditor? _textEditor;
    private readonly MainWindowViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        // Set up the view model
        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;
        _viewModel.SetMainWindow(this);

        // Initialize after the window is loaded
        Loaded += OnWindowLoaded;
    }

    private void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        SetupTextEditor();
        SetupSyntaxHighlighting();
    }

    private void SetupTextEditor()
    {
        _textEditor = this.FindControl<TextEditor>("MermaidEditor");

        if (_textEditor is null) return;

        // Configure editor options
        _textEditor.Options.ShowSpaces = false;
        _textEditor.Options.ShowTabs = false;
        _textEditor.Options.ShowEndOfLine = false;
        _textEditor.Options.ShowBoxForControlCharacters = false;
        _textEditor.Options.IndentationSize = 2;
        _textEditor.Options.ConvertTabsToSpaces = true;
        _textEditor.Options.EnableRectangularSelection = true;
        _textEditor.Options.EnableTextDragDrop = true;
        _textEditor.Options.EnableVirtualSpace = false;
        _textEditor.Options.CutCopyWholeLine = true;
        _textEditor.Options.AllowScrollBelowDocument = true;
    }

    private void SetupSyntaxHighlighting()
    {
        if (_textEditor is null) return;

        try
        {
            // Create registry options with dark theme
            RegistryOptions registryOptions = new RegistryOptions(ThemeName.DarkPlus);

            // Install TextMate
            TextMate.Installation textMateInstallation = _textEditor.InstallTextMate(registryOptions);

            // Try to load custom Mermaid grammar
            IRawGrammar? grammar = LoadMermaidGrammar(registryOptions);

            if (grammar is not null)
            {
                textMateInstallation.SetGrammar(grammar.GetScopeName());
            }
            else
            {
                // Fallback to YAML grammar (similar structure)
                string yamlGrammar = registryOptions.GetScopeByLanguageId("yaml");
                if (yamlGrammar is not null)
                {
                    textMateInstallation.SetGrammar(yamlGrammar);
                }
            }
        }
        catch (Exception ex)
        {
            // If syntax highlighting fails, continue without it
            Console.WriteLine($"Syntax highlighting setup failed: {ex.Message}");
        }
    }

    private static IRawGrammar? LoadMermaidGrammar(RegistryOptions registryOptions)
    {
        try
        {
            // Try to load embedded Mermaid grammar
            Assembly assembly = Assembly.GetExecutingAssembly();
            const string resourceName = "MermaidPad.Resources.mermaid.tmLanguage.json";

            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is not null)
            {
                using StreamReader reader = new StreamReader(stream);
                string grammarJson = reader.ReadToEnd();
                return registryOptions.GetGrammar(grammarJson);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load Mermaid grammar: {ex.Message}");
        }

        return null;
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel?.Close();
        base.OnClosed(e);
    }
}

// Helper converter for conditional CSS classes
public class BoolToClassConverter : IValueConverter
{
    public static readonly BoolToClassConverter Instance = new BoolToClassConverter();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue && parameter is string classNames)
        {
            string[] classes = classNames.Split('|');
            return boolValue ? classes[0] : classes[1];
        }
        return "";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
