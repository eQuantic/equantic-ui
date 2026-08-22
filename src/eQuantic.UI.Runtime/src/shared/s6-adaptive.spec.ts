import { describe, it, expect, beforeEach } from 'vitest';
import { lowerVisualNode } from './lowering';
import { resetAtomizerForTests } from './style-atomizer';
import { photonTheme } from './design-system.generated';
import type { LoweringContext } from './lowering';
import type { VisualNodeValue } from './nodes';

const ctx: LoweringContext = { textPrimary: photonTheme.textPrimary };
const marker = (): VisualNodeValue =>
  ({
    nodeKind: 'box',
    style: { width: { kind: 'fixed', value: 10 }, height: { kind: 'fixed', value: 10 } },
  }) as unknown as VisualNodeValue;

/** Spec S6 client lowering — gate classes and media blobs byte-identical to the C# realizer. */
describe('S6 adaptive lowering (C# cross-pin)', () => {
  beforeEach(() => resetAtomizerForTests());

  it('three variants gate as compact/medium/expanded, rules land in the registry', () => {
    const node = lowerVisualNode(
      {
        nodeKind: 'adaptive',
        compact: marker(),
        medium: marker(),
        expanded: marker(),
      } as unknown as VisualNodeValue,
      ctx,
    );
    expect(node.children).toHaveLength(3);
    // The range is IN the name (dp), so a design's own breakpoints need no shared registry.
    expect(node.children[0].attributes['class']).toBe('eq-vc600');
    expect(node.children[1].attributes['class']).toBe('eq-vm600-840');
    expect(node.children[2].attributes['class']).toBe('eq-vx840');

    const sheet = document.getElementById('eq-atomic') as HTMLStyleElement;
    const rules = [...(sheet.sheet?.cssRules ?? [])].map((r) => r.cssText).join('\n');
    expect(rules).toContain('min-width: 600px');
    expect(rules).toContain('min-width: 840px');
  });

  it('compact+expanded: compact serves the medium range (fallback parity with native)', () => {
    const node = lowerVisualNode(
      { nodeKind: 'adaptive', compact: marker(), expanded: marker() } as unknown as VisualNodeValue,
      ctx,
    );
    expect(node.children).toHaveLength(2);
    expect(node.children[0].attributes['class']).toBe('eq-vc840');
  });

  it("custom thresholds gate at the DESIGN's breakpoint, not the spec's", () => {
    // The handoff header swaps nav↔hamburger at 1024 — gating at 840 is what let the nav overlap
    // the logo on tablets.
    const node = lowerVisualNode(
      {
        nodeKind: 'adaptive',
        compact: marker(),
        expanded: marker(),
        expandedFrom: 1024,
      } as unknown as VisualNodeValue,
      ctx,
    );
    expect(node.children[0].attributes['class']).toBe('eq-vc1024');
    expect(node.children[1].attributes['class']).toBe('eq-vx1024');

    const sheet = document.getElementById('eq-atomic') as HTMLStyleElement;
    const rules = [...(sheet.sheet?.cssRules ?? [])].map((r) => r.cssText).join('\n');
    expect(rules).toContain('min-width: 1024px');
  });

  it('a lone compact is not gated', () => {
    const node = lowerVisualNode(
      { nodeKind: 'adaptive', compact: marker() } as unknown as VisualNodeValue,
      ctx,
    );
    expect(node.attributes['class'] ?? '').not.toContain('eq-v');
  });
});
