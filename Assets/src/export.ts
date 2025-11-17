// MIT License
// Copyright (c) 2025 Dave Black

/**
 * PNG export functionality for MermaidPad
 */

import type { ExportOptions, ExportResult, ExportStatus } from './types.js';

// Helper to set status and notify host (if available)
function setExportStatus(statusObj: ExportStatus): void {
  try {
    const json = JSON.stringify(statusObj);
    window.__pngExportStatus__ = json;

    // Prefer WebView2 style postMessage
    const webviewPostMessage = window.chrome?.webview?.postMessage;
    if (typeof webviewPostMessage === 'function') {
      try {
        webviewPostMessage({ type: 'png-export-progress', status: statusObj });
      } catch (postError) {
        console.warn('chrome.webview.postMessage failed', postError);
      }
      return;
    }

    // Fallback for other hosts
    const externalNotify = window.external?.notify;
    if (typeof externalNotify === 'function') {
      try {
        externalNotify(JSON.stringify({ type: 'png-export-progress', status: statusObj }));
      } catch (exportProgressError) {
        console.warn('external.notify failed', exportProgressError);
      }
    }
  } catch (statusError) {
    // Ignore status delivery errors - keep working locally
    console.warn('Failed to set or notify export status', statusError);
  }
}

/**
 * Browser-based PNG export function
 * Renders the current diagram to PNG using canvas
 */
export async function exportToPNG(options?: ExportOptions): Promise<ExportResult> {
  console.log('Browser-based PNG export started');
  console.log('Export options:', options);

  try {
    // Set status for polling
    setExportStatus({
      step: 'initializing',
      percent: 0,
      message: 'Starting PNG export...'
    });

    // Validate we have diagram source
    if (!window.lastMermaidSource) {
      throw new Error('No diagram source available for export');
    }

    // Parse options with defaults
    const scale = options?.scale || 2;
    const backgroundColor = options?.backgroundColor || 'white';
    const dpi = options?.dpi || 96;

    // Calculate DPI scale (96 is the baseline DPI)
    const dpiScale = dpi / 96;
    const effectiveScale = scale * dpiScale;

    console.log(`Using scale: ${scale}, DPI: ${dpi}, effective scale: ${effectiveScale}`);

    // Update progress: Rendering
    setExportStatus({
      step: 'rendering',
      percent: 20,
      message: 'Rendering diagram...'
    });

    // Render fresh SVG with current config
    const id = `png-export-${Date.now()}`;
    const renderResult = await window.mermaid.render(id, window.lastMermaidSource);

    if (!renderResult?.svg) {
      throw new Error('Failed to render diagram for PNG export');
    }

    // Update progress: Creating canvas
    setExportStatus({
      step: 'creating-canvas',
      percent: 40,
      message: 'Creating canvas...'
    });

    // Create temporary container for SVG
    const container = document.createElement('div');
    container.style.position = 'absolute';
    container.style.left = '-99999px';
    container.style.top = '-99999px';
    container.innerHTML = renderResult.svg;
    document.body.appendChild(container);

    const svgElement = container.querySelector('svg');
    if (!svgElement) {
      container.remove();
      throw new Error('Could not find SVG element in rendered output');
    }

    // Get dimensions from viewBox or bounding box
    const viewBox = svgElement.getAttribute('viewBox');
    let width: number, height: number;

    if (viewBox) {
      const parts = viewBox.split(' ');
      width = Number.parseFloat(parts[2]);
      height = Number.parseFloat(parts[3]);
    } else {
      const bbox = svgElement.getBoundingClientRect();
      width = bbox.width || 800;
      height = bbox.height || 600;
    }

    const scaledWidth = Math.round(width * effectiveScale);
    const scaledHeight = Math.round(height * effectiveScale);
    const totalPixels = scaledWidth * scaledHeight;
    const estimatedMemoryMB = (totalPixels * 4) / (1024 * 1024); // 4 bytes per pixel

    console.log(`SVG dimensions: ${width}x${height}, scaled: ${scaledWidth}x${scaledHeight}`);
    console.log(`Total pixels: ${totalPixels.toLocaleString()}, estimated memory: ${estimatedMemoryMB.toFixed(0)} MB`);

    // Validate canvas size limits
    const maxDimension = 32767; // Browser spec limit per dimension
    const maxPixels = 16384 * 16384; // 16384 x 16384 = 256M pixels (practical limit)
    const maxMemoryMB = 1024; // 1 GB practical memory limit

    if (scaledWidth > maxDimension || scaledHeight > maxDimension) {
      container.remove();
      throw new Error(`Canvas dimension exceeds browser limit of ${maxDimension} pixels. Current: ${scaledWidth}x${scaledHeight}. Try reducing scale or DPI.`);
    }

    if (totalPixels > maxPixels) {
      container.remove();
      const oneMillion = 1000000;
      throw new Error(`Canvas size too large (${(totalPixels / oneMillion).toFixed(1)}M pixels). Maximum recommended: ${(maxPixels / oneMillion).toFixed(0)}M pixels. Try reducing scale or DPI.`);
    }

    if (estimatedMemoryMB > maxMemoryMB) {
      container.remove();
      throw new Error(`Estimated memory usage (${estimatedMemoryMB.toFixed(0)} MB) exceeds safe limit of ${maxMemoryMB} MB. Try reducing scale or DPI.`);
    }

    // Update progress: Converting to bitmap
    setExportStatus({
      step: 'converting',
      percent: 60,
      message: 'Converting to bitmap...'
    });

    // Create canvas with scaled dimensions
    const canvas = document.createElement('canvas');
    canvas.width = scaledWidth;
    canvas.height = scaledHeight;

    const ctx = canvas.getContext('2d');
    if (!ctx) {
      container.remove();
      throw new Error('Failed to get canvas 2D context');
    }

    // Scale context for high-DPI rendering
    ctx.scale(effectiveScale, effectiveScale);

    // Draw background if not transparent
    if (backgroundColor && backgroundColor.toLowerCase() !== 'transparent') {
      ctx.fillStyle = backgroundColor;
      ctx.fillRect(0, 0, width, height);
    }

    // Serialize SVG to string
    const svgString = new XMLSerializer().serializeToString(svgElement);

    // Clean up DOM
    container.remove();

    // Convert SVG to data URI
    const encodedSvg = encodeURIComponent(svgString)
      .replaceAll('\'', '%27')
      .replaceAll('"', '%22');

    const dataUri = `data:image/svg+xml;charset=utf-8,${encodedSvg}`;

    // Load SVG into image
    const img = new Image();

    // Create promise for image loading
    const imageLoadPromise = new Promise<void>((resolve, reject) => {
      img.onload = () => {
        resolve();
      };

      img.onerror = (error) => {
        reject(new Error(`Failed to load SVG as image: ${error}`));
      };

      // Set timeout for image loading (30 seconds)
      setTimeout(() => {
        reject(new Error('Image loading timeout'));
      }, 30000);
    });

    img.src = dataUri;
    await imageLoadPromise;

    // Update progress: Drawing to canvas
    setExportStatus({
      step: 'drawing',
      percent: 80,
      message: 'Drawing to canvas...'
    });

    // Draw image to canvas
    ctx.drawImage(img, 0, 0, width, height);

    // Update progress: Encoding
    setExportStatus({
      step: 'encoding',
      percent: 90,
      message: 'Encoding PNG...'
    });

    // Convert canvas to PNG data URL
    const dataUrl = canvas.toDataURL('image/png');

    // Validate that toDataURL() succeeded
    if (!dataUrl || dataUrl === 'data:,' || !dataUrl.includes(',')) {
      throw new Error('Failed to encode canvas to PNG. The canvas size may be too large for this browser. Try reducing scale or DPI.');
    }

    // Extract base64 data
    const base64Data = dataUrl.split(',')[1];

    // Validate base64 data
    if (!base64Data || base64Data.length === 0) {
      throw new Error('PNG encoding produced empty data. Try reducing scale or DPI.');
    }

    // Update progress: Complete
    setExportStatus({
      step: 'complete',
      percent: 100,
      message: 'Export complete!'
    });

    console.log('PNG export successful, data length:', base64Data.length);

    // Store result for retrieval
    window.__pngExportResult__ = base64Data;

    // Notify host that result is available
    try {
      const webviewPostMessage = window.chrome?.webview?.postMessage;
      if (typeof webviewPostMessage === 'function') {
        webviewPostMessage({ type: 'png-export-complete', length: base64Data.length });
      } else {
        const externalNotify = window.external?.notify;
        if (typeof externalNotify === 'function') {
          externalNotify(JSON.stringify({ type: 'png-export-complete', length: base64Data.length }));
        }
      }
    } catch (e) {
      console.error('Error notifying host that result is available', e);
    }

    return {
      success: true,
      dataLength: base64Data.length,
      dimensions: { width: canvas.width, height: canvas.height }
    };

  } catch (error) {
    console.error('PNG export failed:', error);
    const errorMessage = error instanceof Error ? error.message : String(error);

    setExportStatus({
      step: 'error',
      percent: 0,
      message: errorMessage
    });

    window.__pngExportResult__ = null;

    return {
      success: false,
      error: errorMessage
    };
  }
}

/**
 * Legacy export function for SVG
 */
export function getSVG(): string | null {
  try {
    const svg = document.querySelector('#output svg');
    if (!svg) {
      console.error('No SVG found in output');
      return null;
    }

    // Clone and ensure proper attributes
    const clone = svg.cloneNode(true) as SVGElement;
    clone.setAttribute('xmlns', 'http://www.w3.org/2000/svg');
    clone.setAttribute('xmlns:xlink', 'http://www.w3.org/1999/xlink');

    // Mermaid already includes styles, just serialize
    return new XMLSerializer().serializeToString(clone);
  } catch (error) {
    console.error('Error getting SVG:', error);
    return null;
  }
}
