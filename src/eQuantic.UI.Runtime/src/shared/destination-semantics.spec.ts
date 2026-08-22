import { describe, expect, it } from 'vitest';
import type { ColorTokenValue, VisualNodeValue } from './nodes';
import { lowerVisualNode } from './lowering';

/**
 * WHERE YOU ARE, on the CLIENT half.
 *
 * The server writes `aria-current="page"` on the active destination; if this twin wrote anything
 * else — the attribute on every destination, or on none — the first hydration would silently
 * rewrite the navigation's semantics on every page in the app. Cross-pinned with
 * DestinationSemanticsTests on the C# side.
 */
describe('a navigation destination says where you are', () => {
  const token = (v: number): ColorTokenValue => ({
    light: { r: v, g: v, b: v, a: 255 },
    dark: { r: v, g: v, b: v, a: 255 },
  });
  const ctx = { textPrimary: token(0x17) };

  const destination = (selected: boolean | null): VisualNodeValue =>
    ({
      nodeKind: 'pressable',
      role: 'destination',
      label: 'Activity',
      selected,
      child: { nodeKind: 'box', style: {} },
      onPressed: () => {},
    }) as unknown as VisualNodeValue;

  it('marks the current one, and ONLY the current one', () => {
    expect(lowerVisualNode(destination(true), ctx).attributes['aria-current']).toBe('page');
    // `aria-current="false"` is legal and pure noise: every destination announcing "not current"
    // is worse than silence.
    expect(lowerVisualNode(destination(false), ctx).attributes['aria-current']).toBeUndefined();
  });

  it("never borrows another role's word for it", () => {
    const current = lowerVisualNode(destination(true), ctx).attributes;

    expect(current['aria-pressed']).toBeUndefined();
    expect(current['aria-selected']).toBeUndefined();
    expect(current['aria-checked']).toBeUndefined();
    // A navigation is a list of links, not a composite with one entry point — it keeps its Tab stop.
    expect(current['tabindex']).not.toBe('-1');
  });
});
