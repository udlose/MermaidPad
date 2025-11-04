// eslint.config.mjs
import js from '@eslint/js';
import html from 'eslint-plugin-html';
import globals from 'globals';

export default [
  // 1) GLOBAL IGNORES (keep these first)
  {
    ignores: [
      '**/node_modules/**',
      '**/bin/**',
      '**/obj/**',
      '**/dist/**',
      '**/build/**',
      '**/*.min.js',
      'Assets/mermaid-elk-layout/**/*.mjs',
      // WebView2/Edge runtime profiles & extensions
      '**/MermaidPad.exe.WebView2/**',
      '**/EBWebView/**',
      '**/Default/Extensions/**',
      // don’t lint the config itself
      'eslint.config.*',
    ],
  },

  // 2) Your sources only
  { files: ['Assets/**/*.{js,ts,html}'] },

  // 3) Base rules + environment
  js.configs.recommended,
  {
    languageOptions: {
      ecmaVersion: 'latest',
      sourceType: 'script',
      globals: { ...globals.browser, ...globals.es2021 },
    },
    plugins: { html },
  },

  // 4) If you ever use <script type="module"> in HTML
  { files: ['Assets/**/*.html'], languageOptions: { sourceType: 'module' } },
];
