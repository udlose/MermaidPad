// MIT License
// Copyright (c) 2025 Dave Black

/**
 * Hover enhancement for Mermaid diagrams
 * Adds invisible wider hit areas to paths/lines for easier hovering
 * Uses event delegation to avoid memory leaks
 */

/**
 * Enhances hoverability of diagram elements
 * Adds invisible wider hit areas to paths/lines for easier hovering
 */
export function enhanceHoverability(): void {
  const hitAreaWidth = 20; // Adjustable hover detection width in pixels
  const output = document.getElementById('output');
  if (!output) return;

  const svg = output.querySelector('svg');
  if (!svg) return;

  // Check if event delegation is already initialized on OUTPUT container
  // (OUTPUT persists across renders, unlike SVG which gets replaced)
  const isInitialized = !!(output.dataset.hoverDelegationInit);

  // Find all paths and lines that need hit areas
  const paths = svg.querySelectorAll<SVGPathElement>('path.flowchart-link, .edgePath path, path[class*="edge"], path[marker-end]');
  const lines = svg.querySelectorAll<SVGLineElement>('line.messageLine, line[class*="edge"], line[marker-end]');

  // Create hit areas for paths (these get recreated on each render since SVG is replaced)
  for (const path of paths) {
    if (path.dataset.hoverEnhanced) {
      console.debug('Skipping already-enhanced path', path);
      continue;
    }
    const hitArea = path.cloneNode(false) as SVGPathElement;
    hitArea.classList.add('hit-area');
    hitArea.dataset.hoverEnhanced = 'true';
    hitArea.style.strokeWidth = `${hitAreaWidth}px`;
    hitArea.style.stroke = 'transparent';
    hitArea.style.fill = 'none';
    hitArea.style.pointerEvents = 'stroke';
    hitArea.style.cursor = 'pointer';

    // Insert the hit area immediately after the original path
    path.parentNode?.insertBefore(hitArea, path.nextSibling);
    path.dataset.hoverEnhanced = 'true';
    path.dataset.hoverTarget = 'true';
  }

  // Create hit areas for lines
  for (const line of lines) {
    if (line.dataset.hoverEnhanced) {
      console.debug('Skipping already-enhanced line', line);
      continue;
    }

    const hitArea = line.cloneNode(false) as SVGLineElement;
    hitArea.classList.add('hit-area');
    hitArea.dataset.hoverEnhanced = 'true';
    hitArea.style.strokeWidth = `${hitAreaWidth}px`;
    hitArea.style.stroke = 'transparent';
    hitArea.style.pointerEvents = 'stroke';
    hitArea.style.cursor = 'pointer';

    // Insert the hit area immediately after the original line
    line.parentNode?.insertBefore(hitArea, line.nextSibling);
    line.dataset.hoverEnhanced = 'true';
    line.dataset.hoverTarget = 'true';
  }

  // Only attach event listeners ONCE to the OUTPUT container
  // These listeners persist across renders since OUTPUT doesn't get replaced
  if (!isInitialized) {
    // Use event delegation on OUTPUT container (not SVG which gets replaced)
    output.addEventListener('mouseover', (e) => {
      // Early exit: Skip if not hovering over SVG elements
      if (!(e.target as Element).closest('svg')) return;

      const hitArea = (e.target as Element).closest('.hit-area');
      if (!hitArea) return;

      // Get original element via previousElementSibling
      const original = hitArea.previousElementSibling as HTMLElement | null;
      if (original?.dataset?.hoverTarget) {
        original.classList.add('hover-active');
      }
    }, { capture: true });

    output.addEventListener('mouseout', (e) => {
      // Early exit: Skip if not hovering over SVG elements
      if (!(e.target as Element).closest('svg')) return;

      const hitArea = (e.target as Element).closest('.hit-area');
      if (!hitArea) return;

      // Check if we're actually leaving the hit-area
      if (hitArea.contains(e.relatedTarget as Node)) return;

      // Get original element via previousElementSibling
      const original = hitArea.previousElementSibling as HTMLElement | null;
      if (original?.dataset?.hoverTarget) {
        original.classList.remove('hover-active');
      }
    }, { capture: true });

    // Mark OUTPUT container as initialized
    output.dataset.hoverDelegationInit = 'true';

    console.log('Hover event delegation initialized (one-time setup)');
  }

  // Log hit area creation on first render only
  if (!isInitialized && paths.length + lines.length > 0) {
    console.log(`Created hit areas for ${paths.length} paths and ${lines.length} lines`);
  }
}
