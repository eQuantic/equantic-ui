import { describe, expect, it } from 'vitest';
import { lowerVisualNode } from './lowering';
import { effectiveStyle } from './style-atomizer';
import type { ColorTokenValue, VisualNodeValue } from './nodes';

/**
 * A host that CARRIES A ROLE has the bounds of the thing it names (C# cross-pin:
 * RoleBearingHostBoundsTests).
 *
 * The role-bearing element's box is what a screen reader outlines and a browser draws a focus ring
 * from, so it is part of the announcement. Both of these lower to a block `div`: with a child that
 * hugs, the host stretched to the container while the control stayed its own width. Photon never
 * had it — `MeasureWrapper` gives a layout-transparent wrapper exactly the child's bounds — so this
 * was a web-only divergence in the one place the two targets must agree.
 *
 * Written on both sides at once, because this lowering is its OWN realizer and the C# suite never
 * reaches it.
 */
const textPrimary: ColorTokenValue = {
  light: { r: 0x17, g: 0x1b, b: 0x21, a: 255 },
  dark: { r: 0xf2, g: 0xf4, b: 0xf7, a: 255 },
};
const ctx = { textPrimary };

const hugTrack = () => ({
  nodeKind: 'box',
  style: { width: { kind: 'fixed', value: 120 }, height: { kind: 'fixed', value: 8 } },
  child: { nodeKind: 'text', content: 'bar', role: 'bodyL', maxLines: 0 },
});

const fillTrack = () => ({
  nodeKind: 'box',
  style: { width: { kind: 'fill' }, height: { kind: 'fixed', value: 8 } },
  child: { nodeKind: 'text', content: 'bar', role: 'bodyL', maxLines: 0 },
});

const styleOf = (node: unknown): string =>
  effectiveStyle(node as { attributes: Record<string, string | undefined> });

// A model stands in for the real one: the lowering reads where the text door and the carets stand and
// closes over the rest (a press, a key), so a sheet at rest with its door at its corner is enough to lower
// the host and read its box.
const sheetController = () =>
  ({
    carets: [],
    inputBounds: { x: 0, y: 0, width: 10, height: 10 },
    focusVersion: 0,
  }) as unknown as never;

describe('a role-bearing host has the bounds of what it names', () => {
  for (const [name, wrap] of [
    ['progress', (child: unknown) => ({ nodeKind: 'progress', child, label: 'Uploading' })],
    ['adjustable', (child: unknown) => ({ nodeKind: 'adjustable', child, onAdjust: () => {} })],
    // A live region's box is what a reader outlines when it announces the change inside it, so the
    // same rule applies for the same reason.
    ['liveRegion', (child: unknown) => ({ nodeKind: 'liveRegion', child, label: 'Upload status' })],
    // A grid's host is what a reader outlines (#241); its stop of the Tab order is its text door.
    ['sheetSurface', (child: unknown) =>
      ({ nodeKind: 'sheetSurface', child, model: sheetController(), label: 'Budget' })],
  ] as const) {
    it(`${name} hugs a hug child and fills a fill child`, () => {
      const hug = lowerVisualNode(wrap(hugTrack()) as unknown as VisualNodeValue, ctx);
      const fill = lowerVisualNode(wrap(fillTrack()) as unknown as VisualNodeValue, ctx);

      expect((hug as { attributes: Record<string, string> }).attributes['role']).toBeTruthy();
      expect(styleOf(hug)).toContain('width: fit-content');

      // The fill branch is untouched: a filling track still makes the host fill, or the child's own
      // 100% resolves against a shrink-to-fit box and collapses.
      expect(styleOf(fill)).toContain('width: 100%');
      expect(styleOf(fill)).not.toContain('fit-content');
    });
  }
});
