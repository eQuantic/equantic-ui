import { describe, expect, it } from 'vitest';
import { lowerVisualNode } from './lowering';
import { effectiveStyle } from './style-atomizer';
import type { ColorTokenValue, VisualNodeValue } from './nodes';

/**
 * The window-relative cap on the twin (C# cross-pin: WindowRelativeSizeTests).
 *
 * The web writes it as a `calc` the browser resolves per window; Photon subtracts from the window
 * it was handed. Neither side has a viewport UNIT in the vocabulary, because `vh` is the web's
 * word and means nothing to a Photon window.
 */
const textPrimary: ColorTokenValue = {
  light: { r: 0x17, g: 0x1b, b: 0x21, a: 255 },
  dark: { r: 0xf2, g: 0xf4, b: 0xf7, a: 255 },
};
const ctx = { textPrimary };

const styleOf = (node: unknown): string =>
  effectiveStyle(node as { attributes: Record<string, string | undefined> });

const box = (style: Record<string, unknown>) =>
  ({
    nodeKind: 'box',
    style,
    child: { nodeKind: 'text', content: 'x', role: 'bodyL', maxLines: 0 },
  }) as unknown as VisualNodeValue;

describe('a cap the window decides', () => {
  it('is the window less the inset', () => {
    const lowered = lowerVisualNode(box({ maxHeight: { kind: 'windowMinus', value: 88 } }), ctx);
    expect(styleOf(lowered)).toContain('max-height: calc(100vh - 88px)');
  });

  it('takes its unit from the AXIS', () => {
    expect(styleOf(lowerVisualNode(box({ width: { kind: 'windowMinus', value: 24 } }), ctx)))
      .toContain('width: calc(100vw - 24px)');
    expect(styleOf(lowerVisualNode(box({ height: { kind: 'windowMinus', value: 24 } }), ctx)))
      .toContain('height: calc(100vh - 24px)');
  });

  it('with no inset is the whole window', () => {
    expect(styleOf(lowerVisualNode(box({ maxHeight: { kind: 'windowMinus', value: 0 } }), ctx)))
      .toContain('max-height: 100vh');
  });

  it('still reads a BARE NUMBER, which is what old payloads carry', () => {
    // The C# cap became a SizeValue; a tree serialised before that says `620`, and hydration must
    // not fall over on it — the same tolerance the heading level needed.
    expect(styleOf(lowerVisualNode(box({ maxWidth: 980 }), ctx))).toContain('max-width: 980px');
  });

  it('no cap is still no cap', () => {
    const plain = styleOf(lowerVisualNode(box({}), ctx));
    expect(plain).not.toContain('max-width');
    expect(plain).not.toContain('max-height');
  });
});
