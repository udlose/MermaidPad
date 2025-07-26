using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using MermaidPad.Services;
using MermaidPad.Services.Platforms;
using MermaidPad.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Diagnostics;

namespace MermaidPad.Views;
public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly MermaidRenderer _renderer;

    public MainWindow()
    {
        InitializeComponent();

        Editor.PointerPressed += (_, __) => Debug.WriteLine("PointerPressed");
        Editor.GotFocus += (_, __) => Debug.WriteLine("GotFocus");

        IServiceProvider sp = App.Services;

        _renderer = sp.GetRequiredService<MermaidRenderer>();
        _vm = sp.GetRequiredService<MainViewModel>();
        DataContext = _vm;

        SettingsService settingsService = sp.GetRequiredService<SettingsService>();
        _vm.DiagramText = settingsService.Settings.LastDiagram ?? SampleText();

        this.Opened += async (_, _) =>
        {
            string assets = PlatformServiceFactory.Instance.GetAssetsDirectory();
            await InitializeWebViewAsync(assets);
            await _vm.CheckForMermaidUpdatesAsync();
        };

        this.Closing += OnClosing;

        Editor.Text = _vm.DiagramText ?? "";

        Editor.TextChanged += (_, __) =>
        {
            if (_vm.DiagramText != Editor.Text)
            {
                _vm.DiagramText = Editor.Text;
            }
        };

        _vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_vm.DiagramText) && Editor.Text != _vm.DiagramText)
            {
                Editor.Text = _vm.DiagramText ?? "";
            }
        };

        // Make sure caret is visible:
        Editor.TextArea.Caret.CaretBrush = new SolidColorBrush(Colors.Red);

        // Optional: ensure selection is visible too
        Editor.TextArea.SelectionBrush = new SolidColorBrush(Colors.SteelBlue);
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _vm.Persist();
    }

    private async Task InitializeWebViewAsync(string assets)
    {
        try
        {
            string indexPath = Path.Combine(assets, "index.html");
            if (!File.Exists(indexPath))
            {
                await File.WriteAllTextAsync(indexPath, "<html><body>Missing index.html</body></html>");
            }

            Preview.Url = new Uri(indexPath);
            _renderer.Attach(Preview);
            await Task.Delay(500); // allow initial load
            await _renderer.RenderAsync(_vm.DiagramText);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WebView init failed: {ex}");
        }
    }

    private static string SampleText() => """
graph TD
  A[Start] --> B{Decision}
  B -->|Yes| C[Render Diagram]
  B -->|No| D[Edit Text]
  C --> E[Done]
  D --> B
""";

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
