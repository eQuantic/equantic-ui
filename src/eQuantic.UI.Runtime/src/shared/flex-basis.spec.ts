import { describe, it, expect } from 'vitest';
import { lowerVisualNode } from './lowering';
import { effectiveStyle } from './style-atomizer';
import { photonTheme } from './design-system.generated';
import type { LoweringContext } from './lowering';
import type { VisualNodeValue } from './nodes';

const ctx: LoweringContext = { textPrimary: photonTheme.textPrimary };

const flexible = (extra: Record<string, unknown>): VisualNodeValue =>
  ({
    nodeKind: 'flexible',
    flex: 1,
    child: { nodeKind: 'box', style: {}, children: [] },
    ...extra,
  }) as unknown as VisualNodeValue;

/**
 * `flex: grow shrink basis` — the three numbers, where the realizer used to emit two and a
 * hardcoded `0%`. The basis is what a WRAPPING container measures against, so without it a
 * responsive two-pane layout could not be expressed at all.
 */
describe('flex-basis lowering (C# cross-pin)', () => {
  it('no basis keeps the historical shape', () => {
    // 0% rather than 0px on purpose: it is the output every existing layout was built against.
    expect(effectiveStyle(lowerVisualNode(flexible({ flex: 2 }), ctx))).toContain('flex: 2 1 0%');
  });

  it('a basis is emitted in px', () => {
    expect(effectiveStyle(lowerVisualNode(flexible({ flex: 1, basis: 440 }), ctx)))
      .toContain('flex: 1 1 440px');
  });

  it('shrink 0 refuses to give space back', () => {
    expect(effectiveStyle(lowerVisualNode(flexible({ flex: 1, basis: 320, shrink: 0 }), ctx)))
      .toContain('flex: 1 0 320px');
  });

  it('a zero basis is still 0%, not 0px', () => {
    // Explicitly passing 0 must not produce a different declaration from omitting it — otherwise
    // the atomizer mints a second class for a style that is identical.
    const omitted = effectiveStyle(lowerVisualNode(flexible({ flex: 1 }), ctx));
    const explicit = effectiveStyle(lowerVisualNode(flexible({ flex: 1, basis: 0 }), ctx));
    expect(explicit).toBe(omitted);
  });
});
