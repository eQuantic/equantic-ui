import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { lowerVisualNode } from './lowering';
import {
  adaptiveGateOpen,
  adaptiveGateRules,
  effectiveStyle,
  resetAtomizerForTests,
} from './style-atomizer';
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

  it('a fractional threshold names a gate a selector can reach (#669)', () => {
    // A dot in a selector opens a second class: `.eq-vc703.7037` is `eq-vc703` followed by
    // `.7037`, which cannot be one, so the browser dropped every rule of the gate and each arm
    // showed at every width. The SAME literals S6AdaptiveRealizerTests pins on the server.
    const node = lowerVisualNode(
      {
        nodeKind: 'adaptive',
        compact: marker(),
        medium: marker(),
        expanded: marker(),
        expandedFrom: Math.fround(703.7037),
      } as unknown as VisualNodeValue,
      ctx,
    );
    const gates = node.children.map((gate) => gate.attributes['class']);
    expect(gates).toEqual(['eq-vc600', 'eq-vm600-703_7037', 'eq-vx703_7037']);
    for (const gate of gates) expect(gate).toMatch(/^-?[_a-zA-Z][_a-zA-Z0-9-]*$/);
    expect(adaptiveGateRules('eq-vm600-703_7037').join('')).toBe(
      '.eq-vm600-703_7037{display:none}@media (min-width: 600px) and (max-width: 703.6837px){.eq-vm600-703_7037{display:contents}}',
    );
    expect(adaptiveGateRules('eq-vx703_7037').join('')).toBe(
      '.eq-vx703_7037{display:none}@media (min-width: 703.7037px){.eq-vx703_7037{display:contents}}',
    );
  });

  it.each([
    [1066.6667, 'eq-vx1066_6667', '@media (min-width: 1066.6667px)'],
    [2133.3333, 'eq-vx2133_3333', '@media (min-width: 2133.3333px)'],
    [1279.9999, 'eq-vx1279_9999', '@media (min-width: 1279.9999px)'],
  ])('a threshold of %d keeps its four decimals, as the server spells it', (from, gate, media) => {
    // The value arrives as the C# float it was authored as (eqc writes a float constant through
    // Math.fround), and both producers spell that single's exact value.
    const node = lowerVisualNode(
      {
        nodeKind: 'adaptive',
        compact: marker(),
        expanded: marker(),
        expandedFrom: Math.fround(from),
      } as unknown as VisualNodeValue,
      ctx,
    );
    expect(node.children[1].attributes['class']).toBe(gate);
    expect(adaptiveGateRules(gate)[1]).toBe(`${media}{.${gate}{display:contents}}`);
  });

  it("a spacer arm keeps its space on the parent's axis (#670)", () => {
    // The gates are display:contents, so an arm is laid out by the adaptive node's PARENT, and a
    // Spacer lowered on no axis lowered to nothing. Same literals as S6AdaptiveRealizerTests.
    const fixed = (length: number) =>
      ({ nodeKind: 'spacer', flex: 0, fixedLength: length }) as unknown as VisualNodeValue;
    const arms = () =>
      ({
        nodeKind: 'adaptive',
        compact: fixed(24),
        expanded: fixed(64),
        expandedFrom: 980,
      }) as unknown as VisualNodeValue;
    const flex = (nodeKind: string, children: VisualNodeValue[]) =>
      ({
        nodeKind,
        gap: 0,
        main: 'start',
        cross: 'stretch',
        children,
      }) as unknown as VisualNodeValue;
    const text = (content: string) =>
      ({ nodeKind: 'text', content, role: 'bodyM' }) as unknown as VisualNodeValue;

    const down = lowerVisualNode(flex('column', [text('above'), arms(), text('below')]), ctx)
      .children[1].children;
    const across = lowerVisualNode(flex('row', [arms()]), ctx).children[0].children;

    expect(down).toHaveLength(2);
    expect(effectiveStyle(down[0].children[0])).toBe('flex-shrink: 0; height: 24px');
    expect(effectiveStyle(down[1].children[0])).toBe('flex-shrink: 0; height: 64px');
    expect(effectiveStyle(across[0].children[0])).toBe('flex-shrink: 0; width: 24px');
  });

  it('a positioned arm is anchored in its stack (#671)', () => {
    // An AdaptiveNode is no layer of its own: each ARM is, placed as a direct child would be.
    // Same literals as S6AdaptiveRealizerTests (the order is effectiveStyle's, alphabetical).
    const sized = (width: number, height: number) =>
      ({
        nodeKind: 'box',
        style: { width: { kind: 'fixed', value: width }, height: { kind: 'fixed', value: height } },
      }) as unknown as VisualNodeValue;
    const stack = {
      nodeKind: 'stack',
      align: 'topStart',
      children: [
        sized(400, 300),
        {
          nodeKind: 'adaptive',
          compact: { nodeKind: 'box', style: {} },
          expanded: { nodeKind: 'positioned', child: sized(32, 32), top: 0, end: 0 },
          expandedFrom: 980,
        },
      ],
    } as unknown as VisualNodeValue;

    const layer = lowerVisualNode(stack, ctx).children[1];

    expect(effectiveStyle(layer)).toBe('display: contents');
    expect(effectiveStyle(layer.children[0].children[0])).toContain('grid-area: 1 / 1');
    expect(effectiveStyle(layer.children[1].children[0])).toBe(
      'position: absolute; right: 0; top: 0; z-index: 2',
    );
  });

  it('an arm aligns itself in its line and spans its grid', () => {
    // The rest of what a parent reads off a direct child, placed on the arm. The node carries a
    // placement of its own that no arm asks for, and it is never read, so the compact arm stays
    // unaligned and unspanned. Same assertions as
    // S6AdaptiveRealizerTests.AnArm_AlignsItselfInItsLine_AndSpansItsGrid.
    const sized = (extra: Record<string, unknown>) =>
      ({
        nodeKind: 'box',
        style: { width: { kind: 'fixed', value: 10 }, height: { kind: 'fixed', value: 10 } },
        ...extra,
      }) as unknown as VisualNodeValue;
    const row = {
      nodeKind: 'row',
      gap: 0,
      main: 'start',
      cross: 'start',
      children: [
        {
          nodeKind: 'adaptive',
          alignSelf: 'center',
          compact: marker(),
          expanded: sized({ alignSelf: 'end' }),
        },
      ],
    } as unknown as VisualNodeValue;
    const grid = {
      nodeKind: 'grid',
      columns: [
        { kind: 'fill', value: 1 },
        { kind: 'fill', value: 1 },
      ],
      gap: 0,
      children: [
        { nodeKind: 'adaptive', gridSpan: 2, compact: marker(), expanded: sized({ gridSpan: 2 }) },
      ],
    } as unknown as VisualNodeValue;

    const line = lowerVisualNode(row, ctx).children[0].children;
    const cells = lowerVisualNode(grid, ctx).children[0].children;

    expect(effectiveStyle(line[1].children[0])).toContain('align-self: flex-end');
    expect(effectiveStyle(line[0].children[0])).not.toContain('align-self');
    expect(effectiveStyle(cells[1].children[0])).toContain('grid-column: span 2');
    expect(effectiveStyle(cells[0].children[0])).not.toContain('grid-column');
  });

  it('the rules are the C# AdaptiveGates.Css blobs, byte for byte', () => {
    // The SAME literals S6AdaptiveRealizerTests pins on the server. The registry's cssText cannot
    // say this — a stylesheet reformats what it is given — so the strings are read before insertion.
    expect(adaptiveGateRules('eq-vc600').join('')).toBe(
      '.eq-vc600{display:contents}@media (min-width: 600px){.eq-vc600{display:none}}',
    );
    expect(adaptiveGateRules('eq-vm600-840').join('')).toBe(
      '.eq-vm600-840{display:none}@media (min-width: 600px) and (max-width: 839.98px){.eq-vm600-840{display:contents}}',
    );
    expect(adaptiveGateRules('eq-vx840').join('')).toBe(
      '.eq-vx840{display:none}@media (min-width: 840px){.eq-vx840{display:contents}}',
    );
  });
});

/**
 * Whether an arm is on screen, asked of the browser with the media condition its gate's CSS writes
 * — the question a keyboard shortcut declared inside the arm needs answered at every keypress.
 * Pinned at the thresholds themselves, where a range is off by one if it is off at all.
 */
describe('S6 which arm is on screen', () => {
  const happyDOM = (
    window as unknown as {
      happyDOM: { setViewport(viewport: { width: number; height: number }): void };
    }
  ).happyDOM;
  const at = (width: number) => happyDOM.setViewport({ width, height: 900 });

  afterEach(() => at(1024));

  it.each([
    [599, true, false, false],
    [600, false, true, false],
    [839, false, true, false],
    [840, false, false, true],
  ])('at %ipx: compact %s, medium %s, expanded %s', (width, compact, medium, expanded) => {
    at(width);
    expect(adaptiveGateOpen('eq-vc600')).toBe(compact);
    expect(adaptiveGateOpen('eq-vm600-840')).toBe(medium);
    expect(adaptiveGateOpen('eq-vx840')).toBe(expanded);
  });

  it('an open-ended middle arm stays shown past its threshold', () => {
    at(2000);
    expect(adaptiveGateOpen('eq-vm600')).toBe(true);
    at(599);
    expect(adaptiveGateOpen('eq-vm600')).toBe(false);
  });

  it('a fractional threshold is read back from the name at its decimals', () => {
    at(703);
    expect(adaptiveGateOpen('eq-vc703_7037')).toBe(true);
    expect(adaptiveGateOpen('eq-vx703_7037')).toBe(false);
    at(704);
    expect(adaptiveGateOpen('eq-vc703_7037')).toBe(false);
    expect(adaptiveGateOpen('eq-vx703_7037')).toBe(true);
  });

  it('a name that is not a gate hides nothing', () => {
    at(700);
    expect(adaptiveGateOpen('eq-something-else')).toBe(true);
  });
});
