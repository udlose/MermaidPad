// MIT License
// Copyright (c) 2025 Dave Black

/**
 * Theme management for MermaidPad
 * Handles light/dark theme detection and application
 */

export function getSystemTheme(): string {
  if (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').media !== 'not all') {
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'default';
  }
  return 'default';
}

export function applyTheme(theme: string): void {
  document.body.classList.toggle('dark-theme', theme === 'dark');
  document.body.classList.toggle('light-theme', theme === 'default');
}

/**
 * Initialize theme immediately to prevent flash
 * This runs as an IIFE in the HTML head
 */
export function initializeTheme(): void {
  // Store functions globally for reuse
  window.getSystemTheme = getSystemTheme;
  window.applyTheme = applyTheme;
  window.currentTheme = getSystemTheme();

  // Apply theme immediately if body exists
  if (document.body) {
    applyTheme(window.currentTheme);
  } else {
    // If body doesn't exist yet, apply on DOMContentLoaded
    document.addEventListener('DOMContentLoaded', () => {
      applyTheme(window.currentTheme);
    }, { once: true });
  }
}
