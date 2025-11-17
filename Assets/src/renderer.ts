// MIT License
// Copyright (c) 2025 Dave Black

/**
 * Mermaid diagram rendering functions
 */

import type { RenderOptions, RenderResult } from './types.js';
import { hideLoadingIndicator } from './loading.js';

// Render function with proper error handling
// signature: renderMermaid(source, options = { suppressHideLoadingIndicator: false })
export async function renderMermaid(source: string, options: RenderOptions = {}): Promise<RenderResult> {
  const suppressHide = !!options.suppressHideLoadingIndicator;
  window.lastMermaidSource = source; // Save for re-rendering
  const output = document.getElementById('output');

  // Helper functions
  const createErrorElement = (message: string): HTMLElement => {
    const errorDiv = document.createElement('div');
    errorDiv.className = 'error';
    errorDiv.textContent = message; // XSS-safe
    return errorDiv;
  };

  const showError = (message: string): void => {
    // Render error into output; hide loader only if caller did not suppress it.
    if (output) {
      output.innerHTML = '';
      output.appendChild(createErrorElement(message));
    }
    if (!suppressHide) {
      try {
        hideLoadingIndicator();
      } catch (e) {
        console.error('Error hiding loading indicator:', e);
      }
    }
  };

  const setSafeSVG = (htmlContent: string): void => {
    const trimmed = htmlContent.trim();
    if (trimmed.startsWith('<svg') && trimmed.includes('</svg>')) {
      if (output) {
        output.innerHTML = htmlContent;
      }
      // Enhance hover interactivity after SVG is rendered
      if (typeof window.enhanceHoverability === 'function') {
        const hoverActivationDelay = window.elkLayoutAvailable ? 200 : 50; // ms
        setTimeout(() => window.enhanceHoverability(), hoverActivationDelay);
      }
    } else {
      showError('Invalid diagram content received');
    }
  };

  // Check if initialization failed
  if (window.mermaidInitializationFailed) {
    showError('Mermaid initialization failed. Please refresh the page.');
    return { success: false, error: 'Initialization failed' };
  }

  // Check if mermaid is initialized
  if (!window.mermaidInitialized) {
    // Store for later rendering
    window.pendingMermaidRender = source;

    // Retry logic (same behavior, but return a result)
    if (!window.renderRetryCount) {
      window.renderRetryCount = 0;
    }

    if (window.renderRetryCount < 10) {
      window.renderRetryCount++;
      setTimeout(() => renderMermaid(source, options), 200);
      return { success: false, error: 'Mermaid is initializing; retry scheduled' };
    } else {
      try {
        showError('Mermaid is still loading. Please try again.');
        return { success: false, error: 'Mermaid loading timeout' };
      } finally {
        // Ensure retry count is reset even if showError throws
        window.renderRetryCount = 0;
      }
    }
  }

  // Reset retry count on successful initialization path
  window.renderRetryCount = 0;

  // Parse front-matter config if present
  const fmRegex = /^---\s*([\s\S]*?)---\s*/;
  let diagramSource = source;
  const match = source.match(fmRegex);

  if (match) {
    try {
      const parsed = window.jsyaml.load(match[1]);
      if (parsed && parsed.config) {
        const customConfig = parsed.config;

        // Re-initialize with new config
        const configWithTheme = {
          ...customConfig,
          theme: customConfig.theme || window.currentTheme,
          startOnLoad: false
        };

        // Log config for debugging if using ELK
        if (window.elkLayoutAvailable && customConfig.flowchart?.defaultRenderer === 'elk') {
          console.log('Using ELK layout with config:', configWithTheme);
        }

        window.mermaid.initialize(configWithTheme);
      }
      // Remove front-matter from diagram source
      diagramSource = source.replace(fmRegex, '');
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : String(err);
      showError(`YAML parse error: ${errorMessage}`);
      return { success: false, error: `YAML parse error: ${errorMessage}` };
    }
  }

  // Render the diagram
  const id = `mermaid-svg-${Date.now()}`;
  try {
    const result = await window.mermaid.render(id, diagramSource);
    setSafeSVG(result.svg);
    if (typeof result.bindFunctions === 'function') {
      result.bindFunctions(output);
    }
    // Hide loader for normal renders (unless caller suppressed)
    if (!suppressHide) {
      try {
        hideLoadingIndicator();
      } catch (e) {
        console.error('Error hiding loading indicator', e);
      }
    }
    return { success: true };
  } catch (e) {
    const errorMessage = e instanceof Error ? e.message : String(e);
    showError(errorMessage);
    // Do NOT rethrow; return a failure result to avoid leaking into init failure path
    return { success: false, error: errorMessage };
  }
}

export function clearOutput(): void {
  const output = document.getElementById('output');
  if (output) {
    output.innerHTML = '';
  }
}
