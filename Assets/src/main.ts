// MIT License
// Copyright (c) 2025 Dave Black

/**
 * Main initialization for MermaidPad
 */

import { initializeTheme } from './theme.js';
import { updateLoadingStatus, hideLoadingIndicator } from './loading.js';
import { renderMermaid, clearOutput } from './renderer.js';
import { exportToPNG, getSVG } from './export.js';
import { enhanceHoverability } from './hover.js';

// Initialize theme immediately
initializeTheme();

// Global flags
window.elkLayoutAvailable = false;
window.mermaidInitialized = false;

// Export functions to global scope for C# interop
window.renderMermaid = renderMermaid;
window.clearOutput = clearOutput;
window.updateLoadingStatus = updateLoadingStatus;
window.hideLoadingIndicator = hideLoadingIndicator;
window.exportToPNG = exportToPNG;
window.enhanceHoverability = enhanceHoverability;

// Legacy export API
window.exportDiagram = {
  getSVG
};

// Promise-based script loading with timeout
function waitForScriptLoad(scriptId: string, timeout = 5000): Promise<void> {
  return new Promise((resolve, reject) => {
    const script = document.getElementById(scriptId);

    if (!script) {
      reject(new Error(`Script element with id '${scriptId}' not found`));
      return;
    }

    // Check if already loaded
    if (scriptId === 'mermaid-script' && window.mermaid !== undefined) {
      resolve();
      return;
    }
    if (scriptId === 'jsyaml-script' && window.jsyaml !== undefined) {
      resolve();
      return;
    }

    // Set up timeout
    const timeoutId = setTimeout(() => {
      reject(new Error(`Script '${scriptId}' load timeout after ${timeout}ms`));
    }, timeout);

    // Set up load event listener
    const handleLoad = (): void => {
      clearTimeout(timeoutId);
      resolve();
    };

    const handleError = (error: Event): void => {
      clearTimeout(timeoutId);
      reject(new Error(`Script '${scriptId}' failed to load: ${error.type}`));
    };

    script.addEventListener('load', handleLoad, { once: true });
    script.addEventListener('error', handleError, { once: true });
  });
}

// Load ELK layout module
async function loadElkLayout(): Promise<boolean> {
  try {
    const elkModule = await import('./mermaid-elk-layout/mermaid-layout-elk.esm.min.mjs');
    const elkLayouts = elkModule.default;

    // Register ELK layouts with Mermaid
    window.mermaid.registerLayoutLoaders(elkLayouts);
    window.elkLayoutAvailable = true;
    console.log('ELK layout registered successfully');
    return true;
  } catch (err) {
    console.error('Failed to load ELK layout:', err);
    console.log('Continuing without ELK layout support');
    return false;
  }
}

// Main initialization function
async function initializeApplication(): Promise<void> {
  try {
    // Wait for both scripts to load
    console.log('Waiting for scripts to load...');
    await Promise.all([
      waitForScriptLoad('jsyaml-script'),
      waitForScriptLoad('mermaid-script')
    ]);

    console.log('Scripts loaded, initializing Mermaid...');

    // Update loading status
    updateLoadingStatus('Loading rendering engine...');

    // Try to load ELK layout
    await loadElkLayout();

    // Initialize mermaid (with or without ELK)
    window.mermaid.initialize({
      startOnLoad: false,
      theme: window.currentTheme
    });

    // Set initialization flag
    window.mermaidInitialized = true;
    console.log('Mermaid initialized successfully');

    // Process any pending render
    if (window.pendingMermaidRender) {
      console.log('Processing pending render...');
      try {
        const renderResult = await renderMermaid(window.pendingMermaidRender, { suppressHideLoadingIndicator: true });
        if (!renderResult?.success) {
          console.warn('Pending render failed.');
        }
      } catch (renderError) {
        console.error('Failed to render Mermaid diagram:', renderError);
      } finally {
        window.pendingMermaidRender = undefined;
      }
    }
  } catch (error) {
    console.error('Failed to initialize application:', error);
    window.mermaidInitializationFailed = true;

    const output = document.getElementById('output');
    if (output) {
      output.classList.add('visible');
      const errorMessage = error instanceof Error ? error.message : String(error);
      output.innerHTML = `<div class="error">Failed to initialize: ${errorMessage}</div>`;
    }
  } finally {
    // Always hide the loading indicator
    console.log('Initialization complete, hiding loading indicator.');
    hideLoadingIndicator();
  }
}

// Start initialization
initializeApplication().catch((err) => {
  console.error('initializeApplication failed:', err);
});

// Setup theme change listener
document.addEventListener('DOMContentLoaded', () => {
  // Theme is already applied, but listen for changes
  if (window.matchMedia) {
    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', (e) => {
      window.currentTheme = e.matches ? 'dark' : 'default';
      window.applyTheme(window.currentTheme);

      // Re-render if we have content and Mermaid is initialized
      if (window.lastMermaidSource && window.mermaidInitialized) {
        // Re-initialize Mermaid with new theme
        window.mermaid.initialize({
          startOnLoad: false,
          theme: window.currentTheme
        });
        renderMermaid(window.lastMermaidSource);
      }
    });
  }
});

console.log('MermaidPad initialized');
