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
    expect(node.attributes.style).toBe(
      'width: 120px; height: 40px; padding: 0 16px 0 16px; ' +
        'background-color: light-dark(#0050a0, #5ca2e8); ' +
        'border: 1px solid light-dark(#c9ced6, #3d4754); border-radius: 10px; box-sizing: border-box',
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
    expect(node.attributes.style).toContain('display: flex');
    expect(node.attributes.style).toContain('flex-direction: row');
    expect(node.attributes.style).toContain('justify-content: space-between');
    expect(node.attributes.style).toContain('align-items: center');
    expect(node.attributes.style).toContain('gap: 8px');
  });

  it('Column defaults to stretch', () => {
    const column: VisualNodeValue = {
      nodeKind: 'column',
      gap: 0,
      main: 'start',
      cross: 'stretch',
      children: [],
    } as VisualNodeValue;
    expect(lowerVisualNode(column, ctx).attributes.style).toContain('align-items: stretch');
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
    expect(node.attributes['class']).toBe('eq-type-caption');
    expect(node.attributes.style).toContain('color: light-dark(#5f6b7a, #8b95a3)');
    expect(node.attributes.style).toContain('white-space: nowrap');
    expect(node.attributes.style).toContain('text-overflow: ellipsis');
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

    const style = lowerVisualNode(text, ctx).attributes.style!;
    expect(style).toContain(`color: ${tokenValue(textPrimary)}`);
    expect(style).toContain('font-size: 15px');
    expect(style).toContain('font-weight: 600');
    expect(style).toContain('letter-spacing: 0.1px');
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
    expect(node.attributes.style).toContain('background: none');
    expect(node.attributes.style).toContain('border: none');
    expect(node.attributes.style).toContain('cursor: pointer');

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
    expect(node.attributes.style).not.toContain('cursor');
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
    expect(wrapper.attributes.style).toContain('flex: 2 1 0%');
    expect(wrapper.attributes.style).toContain('min-width: 0');
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
    expect(node.children[0].attributes.style).toBe('flex: 1 1 0%');
    expect(node.children[0].attributes['aria-hidden']).toBe('true');
    // flex-shrink precedes width — the C# HtmlStyle.ToCssString canonical order.
    expect(node.children[1].attributes.style).toBe('flex-shrink: 0; width: 24px');

    const column = { ...row, nodeKind: 'column', cross: 'stretch' } as unknown as VisualNodeValue;
    expect(lowerVisualNode(column, ctx).children[1].attributes.style).toBe(
      'flex-shrink: 0; height: 24px',
    );
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
    expect(node.attributes.style).toContain('background-color: light-dark(#0050a0, #5ca2e8)');
    expect(node.children[0].tag).toBe('span');
  });
});
