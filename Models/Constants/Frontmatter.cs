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

namespace MermaidPad.Models.Constants;

internal static class Frontmatter
{
    internal const string Delimiter = "---";
    internal const string Comment = "%%";
    internal const string Mermaid = "mermaid";
    internal const string DefaultRenderer = "defaultRenderer";
    internal const string Title = "title:";
    internal const string Config = "config:";
    internal const string Layout = "layout:";
    internal const string Dagre = "dagre";
    internal const string Elk = "elk";
    internal const string Theme = "theme:";
    internal const string LogLevel = "logLevel:";
    internal const string SecurityLevel = "securityLevel:";
    internal const string StartOnLoad = "startOnLoad:";
    internal const string Secure = "secure:";
    internal const string PrimaryColor = "primaryColor:";
    internal const string SignalColor = "signalColor:";
    internal const string SignalTextColor = "signalTextColor:";
    internal const string True = "true";
    internal const string False = "false";
    // Theme options: https://mermaid.js.org/config/theming.html#base-diagram-config
    internal const string Base = "base";
    internal const string Forest = "forest";
    internal const string Dark = "dark";
    internal const string Neutral = "neutral";
    internal const string ThemeVariables = "themeVariables:";
    // Base diagram Config: https://mermaid.js.org/config/theming.html#base-diagram-config
    internal const string UseWidth = "useWidth:";
    internal const string UseMaxWidth = "useMaxWidth:";
}
