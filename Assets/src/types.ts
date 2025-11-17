// MIT License
// Copyright (c) 2025 Dave Black

/**
 * Type definitions for global objects and interfaces used in MermaidPad
 */

export interface RenderOptions {
  suppressHideLoadingIndicator?: boolean;
}

export interface RenderResult {
  success: boolean;
  error?: string;
}

export interface ExportOptions {
  scale?: number;
  backgroundColor?: string;
  dpi?: number;
}

export interface ExportStatus {
  step: string;
  percent: number;
  message: string;
}

export interface ExportResult {
  success: boolean;
  dataLength?: number;
  dimensions?: { width: number; height: number };
  error?: string;
}

// Declare global extensions
declare global {
  interface Window {
    // Mermaid
    mermaid: any;
    jsyaml: any;

    // Theme management
    currentTheme: string;
    getSystemTheme: () => string;
    applyTheme: (theme: string) => void;

    // Rendering
    mermaidInitialized: boolean;
    mermaidInitializationFailed?: boolean;
    elkLayoutAvailable: boolean;
    lastMermaidSource?: string;
    pendingMermaidRender?: string;
    renderRetryCount?: number;

    // Functions
    renderMermaid: (source: string, options?: RenderOptions) => Promise<RenderResult>;
    clearOutput: () => void;
    updateLoadingStatus: (message: string) => void;
    hideLoadingIndicator: () => void;
    exportToPNG: (options?: ExportOptions) => Promise<ExportResult>;
    enhanceHoverability: () => void;

    // Export flags
    __renderingComplete__: boolean;
    __pngExportStatus__?: string;
    __pngExportResult__?: string | null;

    // Legacy export
    exportDiagram: {
      getSVG: () => string | null;
    };

    // WebView APIs
    chrome?: {
      webview?: {
        postMessage: (message: any) => void;
      };
    };
    webkit?: {
      messageHandlers?: {
        renderingComplete?: {
          postMessage: (message: string) => void;
        };
      };
    };
    external?: {
      notify?: (message: string) => void;
    };
  }
}
