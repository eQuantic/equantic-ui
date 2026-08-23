import { describe, it, expect } from 'vitest';
import { lowerVisualNode } from './lowering';
import { effectiveStyle } from './style-atomizer';
import { photonTheme } from './design-system.generated';
import type { LoweringContext } from './lowering';
import type { VisualNodeValue } from './nodes';

const ctx: LoweringContext = { textPrimary: photonTheme.textPrimary };

/** A paragraph built from RUNS: its `content` is empty and the text lives in `spans`. */
const styled = (content: string): VisualNodeValue =>
  ({
    nodeKind: 'text',
    content: '',
    role: 'heading',
    spans: [{ content }],
  }) as unknown as VisualNodeValue;

/**
 * Cross-pinned with the C# RichTextPlainContentTests. Anything that reads a Text's `content` FIELD
 * decides as if a styled paragraph were empty, because the paragraph is in `spans`.
 *
 * The parity fixture could never have caught this: the C# twin read the same wrong field, so the
 * two sides agreed. Agreement is not correctness, and this is what that costs.
 */
describe('a paragraph built from runs (C# cross-pin)', () => {
  it('keeps a hard break inside a run', () => {
    expect(effectiveStyle(lowerVisualNode(styled('first\nsecond'), ctx))).toContain('pre-line');
  });

  it('asks for no white-space rule when no run has a break', () => {
    const style = effectiveStyle(lowerVisualNode(styled('one line'), ctx));
    expect(style).not.toContain('pre-line');
    expect(style).not.toContain('pre-wrap');
  });

  it('still reads the plain content when there are no runs at all', () => {
    const plain = { nodeKind: 'text', content: 'a\nb', role: 'heading' } as unknown as VisualNodeValue;
    expect(effectiveStyle(lowerVisualNode(plain, ctx))).toContain('pre-line');
  });
});
