// MIT License
// Copyright (c) 2025 Dave Black

/**
 * Loading indicator management for MermaidPad
 */

// Get transition duration from CSS custom property
function getTransitionDuration(): number {
  const duration = getComputedStyle(document.documentElement)
    .getPropertyValue('--loading-transition-duration')
    .trim();
  // Remove 'ms' suffix and convert to number
  const numericValue = Number.parseFloat(duration.replace('ms', ''));
  return Number.isNaN(numericValue) ? 300 : numericValue; // Fallback to 300ms if parsing fails
}

export function updateLoadingStatus(message: string): void {
  const statusElement = document.getElementById('loading-status');
  if (statusElement) {
    statusElement.textContent = message;
  }
}

export function hideLoadingIndicator(): void {
  const loadingContainer = document.getElementById('loading-container');
  const output = document.getElementById('output');

  if (loadingContainer) {
    loadingContainer.classList.add('hidden');
    // Remove from DOM after transition completes (duration from CSS variable)
    setTimeout(() => {
      loadingContainer.style.display = 'none';
    }, getTransitionDuration());
  }

  if (output) {
    output.classList.add('visible');
  }

  try {
    // Set the flag for C# polling (AvaloniaWebView doesn't support postMessage events)
    window.__renderingComplete__ = true;
    console.log('Set __renderingComplete__ flag for C# polling');

    // Try platform-specific notifications

    // Try WebView2 (Windows)
    if (window.chrome?.webview?.postMessage) {
      try {
        window.chrome.webview.postMessage('renderingComplete');
        console.log('Sent postMessage via chrome.webview (may not be received by AvaloniaWebView)');
      } catch (chromeError) {
        // Silently ignore - AvaloniaWebView may not support this
        console.debug('chrome.webview.postMessage failed:', (chromeError as Error).message);
      }
    }

    // Try WebKit (macOS/Linux)
    if (window.webkit?.messageHandlers?.renderingComplete) {
      try {
        window.webkit.messageHandlers.renderingComplete.postMessage('');
        console.log('Sent postMessage via webkit.messageHandlers (may not be received by AvaloniaWebView)');
      } catch (webkitError) {
        // Silently ignore - AvaloniaWebView may not support this
        console.debug('webkit.messageHandlers failed:', (webkitError as Error).message);
      }
    }
  } catch (outerError) {
    // Even if postMessage fails, ensure the flag is set
    console.error('Error in render completion notification:', (outerError as Error).message);
    window.__renderingComplete__ = true;
  }
}
