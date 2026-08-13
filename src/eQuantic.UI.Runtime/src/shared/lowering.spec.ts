import { effectiveStyle } from './style-atomizer';
import { describe, expect, it } from 'vitest';
import type { ColorTokenValue, VisualNodeValue } from './nodes';
import { lowerVisualNode, px, tokenValue } from './lowering';

/**
 * Hydration-parity tests: these expectations are CROSS-PINNED with the C# WebRealizerTests — the
 * literal strings below also appear there, asserted against the SSR output. If either side drifts,
 * one of the two suites fails; hydration correctness is exactly this equality.
 */

const rgb = (r: number, g: number, b: number, a = 255) => ({ r, g, b, a });
const token = (light: [number, number, number], dark: [number, number, number]): ColorTokenValue => ({
  light: rgb(...light),
  dark: rgb(...dark),
});

// PhotonTheme values (pinned by the C# DesignTokenTests).
const primaryBase = token([0x00, 0x50, 0xa0], [0x5c, 0xa2, 0xe8]);
const borderStrong = token([0xc9, 0xce, 0xd6], [0x3d, 0x47, 0x54]);
const textPrimary = token([0x17, 0x1b, 0x21], [0xf2, 0xf4, 0xf7]);
const textMuted = token([0x5f, 0x6b, 0x7a], [0x8b, 0x95, 0xa3]);

const ctx = { textPrimary };

const box = (style: Record<string, unknown>, child?: VisualNodeValue): VisualNodeValue =>
  ({ nodeKind: 'box', style, child }) as VisualNodeValue;

describe('token → CSS formatting (mirrors C# TokenCss)', () => {
  it('formats light-dark pairs and collapses equal modes', () => {
    expect(tokenValue(primaryBase)).toBe('light-dark(#0050a0, #5ca2e8)');
    expect(tokenValue(token([255, 255, 255], [255, 255, 255]))).toBe('#ffffff');
  });

  it('carries alpha as 8-digit hex', () => {
    // The scrim token: 40% light / 56% dark (0x66 / 0x8f) — pinned by the C# CSS generator tests too.
    expect(tokenValue({ light: rgb(0x0b, 0x0e, 0x12, 102), dark: rgb(0, 0, 0, 143) })).toBe(
      'light-dark(#0b0e1266, #0000008f)',
    );
  });

  it('formats px like C# "0.##"', () => {
    expect(px(0)).toBe('0');
    expect(px(16)).toBe('16px');
    expect(px(3.5)).toBe('3.5px');
    expect(px(0.1)).toBe('0.1px');
  });
});

describe('lowering — cross-pinned with the C# WebRealizer', () => {
  it('Box lowers to div with the EXACT pinned style string', () => {
    const node = lowerVisualNode(
      box({
        width: { kind: 'fixed', value: 120 },
        height: { kind: 'fixed', value: 40 },
        padding: { start: 16, top: 0, end: 16, bottom: 0 },
        background: primaryBase,
        cornerRadius: { topLeft: 10, topRight: 10, bottomRight: 10, bottomLeft: 10 },
        borderWidth: 1,
        borderColor: borderStrong,
      }),
      ctx,
    );

    expect(node.tag).toBe('div');
    // CROSS-PIN: this literal is asserted verbatim by WebRealizerTests.Box_ExactStyleString_CrossPin.
    expect(effectiveStyle(node)).toBe(
      'background-color: light-dark(#0050a0, #5ca2e8); border-radius: 10px; border: 1px solid light-dark(#c9ced6, #3d4754); box-sizing: border-box; flex-shrink: 0; height: 40px; padding: 0 16px 0 16px; width: 120px',
    );
  });

  it('Row lowers to flex with spec defaults (cross center) and gap', () => {
    const row: VisualNodeValue = {
      nodeKind: 'row',
      gap: 8,
      main: 'spaceBetween',
      cross: 'center',
      children: [box({ width: { kind: 'fixed', value: 20 }, height: { kind: 'fixed', value: 20 } })],
    } as VisualNodeValue;

    const node = lowerVisualNode(row, ctx);
    expect(effectiveStyle(node)).toContain('display: flex');
    expect(effectiveStyle(node)).toContain('flex-direction: row');
    expect(effectiveStyle(node)).toContain('justify-content: space-between');
    expect(effectiveStyle(node)).toContain('align-items: center');
    expect(effectiveStyle(node)).toContain('gap: 8px');
  });

  it('Column defaults to stretch', () => {
    const column: VisualNodeValue = {
      nodeKind: 'column',
      gap: 0,
      main: 'start',
      cross: 'stretch',
      children: [],
    } as VisualNodeValue;
    expect(effectiveStyle(lowerVisualNode(column, ctx))).toContain('align-items: stretch');
  });

  it('Text lowers to a role-classed span with the ellipsis contract', () => {
    const text: VisualNodeValue = {
      nodeKind: 'text',
      content: 'Saldo disponível',
      role: 'caption',
      color: textMuted,
      maxLines: 1,
    } as VisualNodeValue;

    const node = lowerVisualNode(text, ctx);
    expect(node.tag).toBe('span');
    expect(node.attributes['class']).toMatch(/^eq-type-caption(?: |$)/);
    expect(effectiveStyle(node)).toContain('color: light-dark(#5f6b7a, #8b95a3)');
    expect(effectiveStyle(node)).toContain('white-space: nowrap');
    expect(effectiveStyle(node)).toContain('text-overflow: ellipsis');
    expect(node.children[0].textContent).toBe('Saldo disponível');
  });

  it('Text defaults to the theme TextPrimary and honors StyleOverride', () => {
    const text: VisualNodeValue = {
      nodeKind: 'text',
      content: 'Continuar',
      role: 'label',
      maxLines: 1,
      styleOverride: { size: 15, lineHeight: 15, weight: 'semiBold', tracking: 0.1 },
    } as VisualNodeValue;

    const style = effectiveStyle(lowerVisualNode(text, ctx));
    expect(style).toContain(`color: ${tokenValue(textPrimary)}`);
    expect(style).toContain('font-size: 15px');
    expect(style).toContain('font-weight: 600');
    expect(style).toContain('letter-spacing: 0.1px');
  });

  it('Text slants when the node asks for it', () => {
    const text: VisualNodeValue = {
      nodeKind: 'text',
      content: 'ementa',
      role: 'bodyM',
      maxLines: 0,
      italic: true,
    } as VisualNodeValue;

    expect(effectiveStyle(lowerVisualNode(text, ctx))).toContain('font-style: italic');
  });

  /**
   * The two things a RUN carries that the paragraph around it does not — and both were missing
   * from this side until 0.2.0-preview.28. The size one is the sharper story: the server sized a
   * markdown code span at 13.5 and hydration re-sized it to the paragraph's own size, so every
   * inline `code` on a documentation page grew the moment the page booted.
   */
  it('a rich run carries its own slant and its own size', () => {
    const text: VisualNodeValue = {
      nodeKind: 'text',
      content: '',
      role: 'bodyM',
      maxLines: 0,
      spans: [
        { content: 'plain ' },
        { content: 'emphasis', italic: true },
        { content: ' and ' },
        {
          content: 'code',
          mono: true,
          styleOverride: { size: 13.5, lineHeight: 20, weight: 'regular', tracking: 0 },
        },
      ],
    } as VisualNodeValue;

    const runs = lowerVisualNode(text, ctx).children;
    expect(effectiveStyle(runs[1])).toContain('font-style: italic');
    expect(effectiveStyle(runs[3])).toContain('font-size: 13.5px');
    // An unemphasised run states nothing: a declaration nobody asked for is a class nobody shares.
    expect(effectiveStyle(runs[0])).not.toContain('font-style');
    expect(effectiveStyle(runs[0])).not.toContain('font-size');
  });

  it('Pressable lowers to a neutralized button wiring onPressed to click', () => {
    let fired = false;
    const pressable: VisualNodeValue = {
      nodeKind: 'pressable',
      child: { nodeKind: 'text', content: 'Go', role: 'label', maxLines: 1 },
      onPressed: () => {
        fired = true;
      },
      label: 'Go',
    } as VisualNodeValue;

    const node = lowerVisualNode(pressable, ctx);
    expect(node.tag).toBe('button');
    expect(node.attributes['aria-label']).toBe('Go');
    expect(effectiveStyle(node)).toContain('background: none');
    expect(effectiveStyle(node)).toContain('border: none');
    expect(effectiveStyle(node)).toContain('cursor: pointer');

    (node.events['click'] as () => void)();
    expect(fired).toBe(true);
  });

  it('Pressable disabled swallows the handler and sets the attribute', () => {
    const pressable: VisualNodeValue = {
      nodeKind: 'pressable',
      child: { nodeKind: 'text', content: 'X', role: 'label', maxLines: 1 },
      onPressed: () => {},
      disabled: true,
    } as VisualNodeValue;

    const node = lowerVisualNode(pressable, ctx);
    expect(node.attributes['disabled']).toBe('');
    expect(node.events['click']).toBeUndefined();
    expect(effectiveStyle(node)).not.toContain('cursor');
  });

  it('Flexible lowers to basis-zero grow with min-width 0 (truncation contract)', () => {
    const row: VisualNodeValue = {
      nodeKind: 'row',
      gap: 0,
      main: 'start',
      cross: 'center',
      children: [
        {
          nodeKind: 'flexible',
          flex: 2,
          child: { nodeKind: 'text', content: 't', role: 'bodyM', maxLines: 0 },
        } as VisualNodeValue,
      ],
    } as VisualNodeValue;

    const wrapper = lowerVisualNode(row, ctx).children[0];
    expect(effectiveStyle(wrapper)).toContain('flex: 2 1 0%');
    expect(effectiveStyle(wrapper)).toContain('min-width: 0');
  });

  // The C# twin asserts the same two shapes (WebRealizerTests): a positive basis is what lets a
  // wrapping row break instead of squeezing, and the pair has to hash to the same class on both
  // sides or hydration repaints what SSR already drew.
  it('Flexible carries its own basis and shrink', () => {
    const row: VisualNodeValue = {
      nodeKind: 'row',
      gap: 0,
      main: 'start',
      cross: 'center',
      wrap: true,
      children: [
        {
          nodeKind: 'flexible',
          flex: 1,
          basis: 220,
          child: { nodeKind: 'text', content: 't', role: 'bodyM', maxLines: 0 },
        } as VisualNodeValue,
        {
          nodeKind: 'flexible',
          flex: 1,
          basis: 180,
          shrink: 0,
          child: { nodeKind: 'text', content: 't', role: 'bodyM', maxLines: 0 },
        } as VisualNodeValue,
      ],
    } as VisualNodeValue;

    const children = lowerVisualNode(row, ctx).children;
    expect(effectiveStyle(children[0])).toContain('flex: 1 1 220px');
    expect(effectiveStyle(children[1])).toContain('flex: 1 0 180px');
  });

  // `overflow` and `text-overflow` are inert on a non-replaced inline box, and a Text lowers to a
  // span — so without a display the ellipsis contract did nothing and a squeezed text painted out
  // of its parent.
  it('a single-line Text is a block, so the ellipsis contract applies', () => {
    const text: VisualNodeValue = {
      nodeKind: 'text',
      content: 'Saldo disponível',
      role: 'caption',
      maxLines: 1,
    } as VisualNodeValue;

    const style = effectiveStyle(lowerVisualNode(text, ctx));
    expect(style).toContain('display: block');
    expect(style).toContain('overflow: hidden');
    expect(style).toContain('text-overflow: ellipsis');
  });

  it('Spacer follows the flex axis (flexible and fixed)', () => {
    const row: VisualNodeValue = {
      nodeKind: 'row',
      gap: 0,
      main: 'start',
      cross: 'center',
      children: [
        { nodeKind: 'spacer', flex: 1, fixedLength: 0 } as VisualNodeValue,
        { nodeKind: 'spacer', flex: 0, fixedLength: 24 } as VisualNodeValue,
      ],
    } as VisualNodeValue;

    const node = lowerVisualNode(row, ctx);
    expect(effectiveStyle(node.children[0])).toBe('flex: 1 1 0%');
    expect(node.children[0].attributes['aria-hidden']).toBe('true');
    // flex-shrink precedes width — the C# HtmlStyle.ToCssString canonical order.
    expect(effectiveStyle(node.children[1])).toBe('flex-shrink: 0; width: 24px');

    const column = { ...row, nodeKind: 'column', cross: 'stretch' } as unknown as VisualNodeValue;
    expect(effectiveStyle(lowerVisualNode(column, ctx).children[1])).toBe(
      'flex-shrink: 0; height: 24px',
    );
  });

  it('WebFrame lowers to a sandboxed iframe — srcdoc wins, sandbox always present', () => {
    const frame: VisualNodeValue = {
      nodeKind: 'webFrame',
      document: '<!doctype html><p>hello</p>',
      source: '/ignored',
      sandbox: 3, // Scripts | SameOrigin — the [Flags] number as it crosses the bridge
      title: 'Live preview',
      width: { kind: 'fill', value: 0 },
      height: { kind: 'fixed', value: 360 },
    } as unknown as VisualNodeValue;

    const node = lowerVisualNode(frame, ctx);
    expect(node.tag).toBe('iframe');
    expect(node.attributes['srcdoc']).toBe('<!doctype html><p>hello</p>');
    expect(node.attributes['src']).toBeUndefined();
    expect(node.attributes['sandbox']).toBe('allow-scripts allow-same-origin');
    expect(node.attributes['title']).toBe('Live preview');
    expect(effectiveStyle(node)).toContain('width: 100%');
    expect(effectiveStyle(node)).toContain('height: 360px');
    expect(effectiveStyle(node)).toContain('border: 0');

    // Locked frame: the attribute stays, EMPTY — omitting it would be no isolation at all.
    const locked = lowerVisualNode(
      { nodeKind: 'webFrame', source: '/x', sandbox: 0 } as unknown as VisualNodeValue,
      ctx,
    );
    expect(locked.attributes['sandbox']).toBe('');
    expect(locked.attributes['src']).toBe('/x');
  });

  it('single-line MONO text keeps its spaces (nowrap would collapse the indentation)', () => {
    const code: VisualNodeValue = {
      nodeKind: 'text',
      content: '    private int _count;',
      role: 'bodyM',
      mono: true,
      maxLines: 1,
    } as VisualNodeValue;
    expect(effectiveStyle(lowerVisualNode(code, ctx))).toContain('white-space: pre');
    expect(effectiveStyle(lowerVisualNode(code, ctx))).not.toContain('white-space: nowrap');

    const label = { ...code, mono: false, content: 'a long label' } as unknown as VisualNodeValue;
    expect(effectiveStyle(lowerVisualNode(label, ctx))).toContain('white-space: nowrap');
  });

  it('Components expand via build(context) — the write-once client path', () => {
    const component: VisualNodeValue = {
      nodeKind: 'component',
      build: () =>
        box(
          { background: primaryBase, height: { kind: 'fixed', value: 40 } },
          { nodeKind: 'text', content: 'Pay', role: 'label', maxLines: 1 } as VisualNodeValue,
        ),
    } as VisualNodeValue;

    const node = lowerVisualNode(component, ctx);
    expect(node.tag).toBe('div');
    expect(effectiveStyle(node)).toContain('background-color: light-dark(#0050a0, #5ca2e8)');
    expect(node.children[0].tag).toBe('span');
  });
});

/**
 * FIXED means fixed. A flex item's `flex-shrink` is 1 by default, so a box with an explicit width
 * beside an overflowing sibling was quietly squeezed — the code editor's gutter lost width on
 * exactly the lines whose code ran past the viewport, and every line number in it slid left.
 * Photon never shrinks a fixed box; the web mirror must not either.
 */
describe('a fixed size does not shrink', () => {
  const fixed = (value: number) => ({ kind: 'fixed' as const, value });

  it('a fixed-width box refuses to shrink beside an overflowing sibling', () => {
    const gutter = box({ width: fixed(68) }, undefined);
    expect(effectiveStyle(lowerVisualNode(gutter as VisualNodeValue, ctx))).toContain('flex-shrink: 0');
  });

  it('a fixed-height row refuses to shrink in a column', () => {
    const row = {
      nodeKind: 'row',
      gap: 0,
      main: 'start',
      cross: 'center',
      height: fixed(18),
      children: [],
    } as unknown as VisualNodeValue;
    expect(effectiveStyle(lowerVisualNode(row, ctx))).toContain('flex-shrink: 0');
  });

  it('leaves HUG and FILL alone — those shrink by design (a long label ellipsizes)', () => {
    const hug = box({}, undefined);
    const fill = box({ width: { kind: 'fill' as const, value: 0 } }, undefined);
    expect(effectiveStyle(lowerVisualNode(hug as VisualNodeValue, ctx))).not.toContain('flex-shrink');
    expect(effectiveStyle(lowerVisualNode(fill as VisualNodeValue, ctx))).not.toContain('flex-shrink');
  });
});
