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

describe('a role-bearing host has the bounds of what it names', () => {
  for (const [name, wrap] of [
    ['progress', (child: unknown) => ({ nodeKind: 'progress', child, label: 'Uploading' })],
    ['adjustable', (child: unknown) => ({ nodeKind: 'adjustable', child, onAdjust: () => {} })],
    // A live region's box is what a reader outlines when it announces the change inside it, so the
    // same rule applies for the same reason.
    ['liveRegion', (child: unknown) => ({ nodeKind: 'liveRegion', child, label: 'Upload status' })],
    // A labelled group's box is what a reader outlines when it names the group (#677).
    ['group', (child: unknown) => ({ nodeKind: 'group', child, label: 'Shipping' })],
    // A sheet surface was the fourth until it stopped bearing a role (#1081): the table it holds is the
    // grid a reader walks.
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

const cappedTrack = () => ({
  ...hugTrack(),
  style: {
    width: { kind: 'fixed', value: 120 },
    maxWidth: { kind: 'fixed', value: 200 },
    height: { kind: 'fixed', value: 8 },
  },
});

const fillCappedTrack = () => ({
  ...hugTrack(),
  style: {
    width: { kind: 'fill' },
    maxWidth: { kind: 'fixed', value: 200 },
    height: { kind: 'fixed', value: 8 },
  },
});

const fillDownTrack = () => ({
  ...hugTrack(),
  style: { width: { kind: 'fixed', value: 120 }, height: { kind: 'fill' } },
});

/**
 * The host takes the child's WHOLE contract (C# cross-pin: RoleBearingHostBoundsTests): the width, the
 * cap through as `max-width` and a filling height as `height: 100%`, on every wrapper the one host
 * writes. A host that kept the width and dropped the others passes the suite above (#1517).
 */
describe('the semantics host takes the child whole contract', () => {
  for (const [name, wrap] of [
    ['progress', (child: unknown) => ({ nodeKind: 'progress', child, label: 'Uploading' })],
    ['adjustable', (child: unknown) => ({ nodeKind: 'adjustable', child, onAdjust: () => {} })],
    ['liveRegion', (child: unknown) => ({ nodeKind: 'liveRegion', child, label: 'Upload status' })],
    ['group', (child: unknown) => ({ nodeKind: 'group', child, label: 'Shipping' })],
    // These two bear no role: the width contract is all their host is.
    ['mergeSemantics', (child: unknown) => ({ nodeKind: 'mergeSemantics', child })],
    ['excludeSemantics', (child: unknown) => ({ nodeKind: 'excludeSemantics', child })],
  ] as const) {
    it(`${name} passes the cap and the height through`, () => {
      const lower = (child: unknown) =>
        styleOf(lowerVisualNode(wrap(child) as unknown as VisualNodeValue, ctx));
      const plain = lower(hugTrack());

      expect(plain).toContain('width: fit-content');
      expect(plain).not.toContain('max-width');
      expect(plain).not.toContain('height');
      expect(lower(fillTrack())).toContain('width: 100%');
      expect(lower(cappedTrack())).toContain('max-width: 200px');
      expect(lower(fillDownTrack())).toContain('height: 100%');
      // The cap goes through on the fill branch too, never one without the other.
      const fillCapped = lower(fillCappedTrack());
      expect(fillCapped).toContain('width: 100%');
      expect(fillCapped).toContain('max-width: 200px');
      // The pointer is taken by the one wrapper that is a tab stop and operated, and by none of the others.
      if (name === 'adjustable') expect(plain).toContain('pointer-events: auto');
      else expect(plain).not.toContain('pointer-events');
    });
  }
});

/**
 * The host writes the caller's attributes (the role's) and THEN the accessible name, as the tree wrote
 * them before the helper: the delivery's claim is byte equality, and a host that wrote the name first
 * passes every other test here (#1517). C# cross-pin: RoleBearingHostBoundsTests.
 */
describe('the semantics host writes the role attributes, then the accessible name', () => {
  for (const [name, node, order] of [
    ['progress', { nodeKind: 'progress', label: 'Uploading' }, ['role', 'aria-label']],
    ['adjustable', { nodeKind: 'adjustable', label: 'Volume', onAdjust: () => {} }, ['role', 'tabindex', 'aria-label']],
    ['liveRegion', { nodeKind: 'liveRegion', label: 'Upload status' }, ['role', 'aria-live', 'aria-atomic', 'aria-label']],
    ['group', { nodeKind: 'group', label: 'Shipping' }, ['role', 'aria-label']],
  ] as const) {
    it(`${name} writes ${order.join(', ')}`, () => {
      const host = lowerVisualNode({ ...node, child: hugTrack() } as unknown as VisualNodeValue, ctx);
      const keys = Object.keys((host as { attributes: Record<string, string> }).attributes).filter((k) => k !== 'style' && k !== 'class');
      expect(keys.slice(0, order.length)).toEqual([...order]);
    });
  }
});
