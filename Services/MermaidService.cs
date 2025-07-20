using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace MermaidPad.Services;
public class MermaidService
{
    private readonly string _tempDirectory;
    private readonly string _htmlTemplateFile;

    public MermaidService()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "MermaidPad");
        _htmlTemplateFile = Path.Combine(_tempDirectory, "mermaid-preview.html");

        // Ensure temp directory exists
        Directory.CreateDirectory(_tempDirectory);
    }

    public async Task<string> GeneratePreviewAsync(string mermaidCode)
    {
        try
        {
            string htmlContent = GenerateHtmlTemplate(mermaidCode);
            await File.WriteAllTextAsync(_htmlTemplateFile, htmlContent);

            // Return file URL for WebView
            return $"file:///{_htmlTemplateFile.Replace('\\', '/')}";
        }
        catch (Exception ex)
        {
            // Return error HTML
            string errorHtml = GenerateErrorHtml(ex.Message);
            await File.WriteAllTextAsync(_htmlTemplateFile, errorHtml);
            return $"file:///{_htmlTemplateFile.Replace('\\', '/')}";
        }
    }

    private static string GenerateHtmlTemplate(string mermaidCode)
    {
        // Escape any potential HTML in the mermaid code
        string escapedCode = mermaidCode.Replace("<", "&lt;").Replace(">", "&gt;");

        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Mermaid Preview</title>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            margin: 0;
            padding: 20px;
            background: #f8f9fa;
            min-height: 100vh;
            display: flex;
            flex-direction: column;
        }}
        .container {{
            display: flex;
            justify-content: center;
            align-items: center;
            flex-grow: 1;
            min-height: 400px;
        }}
        .mermaid {{
            background: white;
            border-radius: 8px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
            padding: 20px;
            max-width: 100%;
            overflow: auto;
        }}
        .error {{
            color: #dc3545;
            background: #f8d7da;
            border: 1px solid #f5c6cb;
            padding: 12px;
            border-radius: 4px;
            margin: 20px 0;
            font-family: 'Consolas', 'Monaco', monospace;
            white-space: pre-wrap;
        }}
        .status {{
            position: fixed;
            bottom: 10px;
            right: 10px;
            background: #007bff;
            color: white;
            padding: 8px 12px;
            border-radius: 4px;
            font-size: 12px;
            display: none;
        }}
        .loading {{
            text-align: center;
            color: #6c757d;
            font-style: italic;
        }}
    </style>
    <script src='https://cdn.jsdelivr.net/npm/mermaid@10.6.1/dist/mermaid.min.js'></script>
</head>
<body>
    <div class='container'>
        <div class='mermaid'>
            {escapedCode}
        </div>
    </div>
    <div class='status' id='status'>Rendering...</div>
    
    <script>
        // Show loading status
        document.getElementById('status').style.display = 'block';
        
        // Configure mermaid
        mermaid.initialize({{
            startOnLoad: true,
            theme: 'default',
            securityLevel: 'loose',
            fontFamily: 'Segoe UI, Tahoma, Geneva, Verdana, sans-serif',
            flowchart: {{
                htmlLabels: true,
                curve: 'basis'
            }},
            sequence: {{
                diagramMarginX: 50,
                diagramMarginY: 10,
                actorMargin: 50,
                width: 150,
                height: 65,
                boxMargin: 10,
                boxTextMargin: 5,
                noteMargin: 10,
                messageMargin: 35
            }},
            gantt: {{
                titleTopMargin: 25,
                barHeight: 20,
                fontSize: 11,
                fontFamily: 'Segoe UI'
            }}
        }});
        
        // Handle successful rendering
        mermaid.parseError = function(err, hash) {{
            console.error('Mermaid parse error:', err);
            document.querySelector('.container').innerHTML = 
                '<div class=""error"">Mermaid Syntax Error:<br><br>' + 
                err.message + '<br><br>' + 
                'Please check your diagram syntax and try again.</div>';
            document.getElementById('status').style.display = 'none';
        }};
        
        // Hide status after rendering
        setTimeout(() => {{
            document.getElementById('status').style.display = 'none';
        }}, 2000);
        
        // Global error handling
        window.addEventListener('error', function(e) {{
            console.error('Global error:', e);
            document.querySelector('.container').innerHTML = 
                '<div class=""error"">Rendering Error:<br><br>' + 
                e.message + '<br><br>' + 
                'Please try refreshing the preview.</div>';
            document.getElementById('status').style.display = 'none';
        }});
        
        // Handle mermaid rendering completion
        window.addEventListener('load', function() {{
            setTimeout(() => {{
                document.getElementById('status').style.display = 'none';
            }}, 1000);
        }});
    </script>
</body>
</html>";
    }

    private static string GenerateErrorHtml(string errorMessage)
    {
        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Preview Error</title>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            margin: 0;
            padding: 20px;
            background: #f8f9fa;
        }}
        .error {{
            color: #dc3545;
            background: #f8d7da;
            border: 1px solid #f5c6cb;
            padding: 20px;
            border-radius: 8px;
            margin: 20px 0;
            font-family: 'Consolas', 'Monaco', monospace;
            white-space: pre-wrap;
        }}
    </style>
</head>
<body>
    <div class='error'>
        <h3>Preview Generation Error</h3>
        {errorMessage}
    </div>
</body>
</html>";
    }

    public void Cleanup()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error cleaning up temporary files: " + ex.Message);
        }
    }
}
