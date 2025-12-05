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

  // 2) Enable HTML processor so <script> and <script type='module'> are linted
  {
    files: ['**/*.html'],
    plugins: { html },
  },

  // 3) Base recommended rules
  js.configs.recommended,

  // 4) Global language settings
  {
    languageOptions: {
      ecmaVersion: 'latest',
      sourceType: 'module', // <--- correct choice for both script types
      globals: { ...globals.browser, ...globals.es2025 },
    },
    plugins: {
      html,
    },
  },
];
