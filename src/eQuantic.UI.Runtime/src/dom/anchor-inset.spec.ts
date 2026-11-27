import { afterEach, describe, expect, it } from 'vitest';
import { ANCHOR_VIEWPORT_INSET, commitAnchoredPanels, insetShift, keepInside } from './anchor-inset';

/** A panel measured where its placement put it: happy-dom lays nothing out, so the box is the one a browser measured. */
function panelAt(left: number, width: number): HTMLElement {
  const host = document.createElement('div');
  host.className = 'eq-anchorhost';
  const panel = document.createElement('div');
  panel.className = 'eq-anchor-panel';
  host.appendChild(panel);
  document.body.appendChild(host);
  panel.getBoundingClientRect = () => {
    const shift = Number.parseFloat(panel.style.translate || '0');
    return { left: left + shift, right: left + shift + width, width, height: 300, top: 0, bottom: 300, x: left, y: 0, toJSON: () => ({}) } as DOMRect;
  };
  return panel;
}

function viewport(width: number): void {
  Object.defineProperty(document.documentElement, 'clientWidth', { configurable: true, value: width });
}

describe('an anchored panel keeps inside the viewport (#1933)', () => {
  afterEach(() => {
    document.body.innerHTML = '';
  });

  it('is the C# Anchored.ViewportInset', () => {
    expect(ANCHOR_VIEWPORT_INSET).toBe(8);
  });

  // The issue's measurements: a 334px calendar from the opener's start, at 390, 768 and 1280.
  it.each([
    [306, 640, 390, 48 - 306],
    [669, 1003, 768, 426 - 669],
    [955, 1289, 1280, 938 - 955],
  ])('moves a panel past the end edge back to end at the inset: %d..%d in %d', (left, right, width, shift) => {
    expect(insetShift(left, right, width)).toBe(shift);
  });

  it('starts a panel past the start edge, or wider than the room, at the start inset', () => {
    expect(insetShift(-8, 326, 390)).toBe(16);
    expect(insetShift(-100, 400, 360)).toBe(108);
  });

  it('leaves a panel inside the viewport where it is', () => {
    expect(insetShift(100, 300, 1280)).toBe(0);
  });

  it('moves the panel by its translate, and measures it again from where its placement put it', () => {
    viewport(390);
    const panel = panelAt(306, 334);
    keepInside(panel);
    expect(panel.style.translate).toBe('-258px 0');
    keepInside(panel);
    expect(panel.style.translate).toBe('-258px 0');
  });

  it('moves the open panels once a pass is written, and leaves a hover-revealed one to the moment it is reached', () => {
    viewport(390);
    const open = panelAt(306, 334);
    const tip = panelAt(306, 334);
    tip.parentElement!.classList.add('eq-hoverreveal');
    commitAnchoredPanels();
    expect(open.style.translate).toBe('-258px 0');
    expect(tip.style.translate).toBe('');
    tip.parentElement!.dispatchEvent(new Event('pointerover', { bubbles: true }));
    expect(tip.style.translate).toBe('-258px 0');
  });

  it('moves an open panel inside a hover-revealed one, whose own host says it is open', () => {
    viewport(390);
    const tip = panelAt(10, 200);
    tip.parentElement!.classList.add('eq-hoverreveal');
    const nested = panelAt(306, 334);
    tip.appendChild(nested.parentElement!);
    commitAnchoredPanels();
    expect(nested.style.translate).toBe('-258px 0');
  });
});
