import { effectiveStyle } from './style-atomizer';
import { describe, expect, it } from 'vitest';
import type { HtmlNode } from '../core/types';
import { photonTheme } from './design-system.generated';
import { tokenValue } from './lowering';
import { Box, BoxStyle, Column, Pressable, Row, Spacer, Text } from './vocabulary';
import { ColorToken, CornerRadii, EdgeInsets, SizeValue } from './value-types';

describe('vocabulary value types', () => {
  it('normalizes raw numbers to fixed sizes (the C# implicit float→SizeValue)', () => {
    const style = new BoxStyle({ width: 120, height: SizeValue.fill });
    expect(style.width).toEqual(SizeValue.fixed(120));
    expect(style.height?.kind).toBe('fill');
  });

  it('CornerRadii single argument is the uniform overload', () => {
    expect(new CornerRadii(10)).toMatchObject({
      topLeft: 10,
      topRight: 10,
      bottomRight: 10,
      bottomLeft: 10,
    });
    expect(new CornerRadii(1, 2, 3, 4)).toMatchObject({
      topLeft: 1,
      topRight: 2,
      bottomRight: 3,
      bottomLeft: 4,
    });
  });

  it('EdgeInsets.symmetric maps (horizontal, vertical) like the C# factory', () => {
    expect(EdgeInsets.symmetric(16, 4)).toMatchObject({ start: 16, top: 4, end: 16, bottom: 4 });
  });

  it('ColorToken.withOpacity scales alpha byte-rounded on both modes', () => {
    const token = new ColorToken(
      { r: 0, g: 80, b: 160, a: 255 },
      { r: 92, g: 162, b: 232, a: 255 },
    );
    const disabled = token.withOpacity(0.38);
    expect(disabled.light.a).toBe(97); // round(255 × 0.38)
    expect(disabled.dark.a).toBe(97);
    expect(disabled.light.r).toBe(0);
  });

  it('Spacer.fixed is rigid, plain Spacer is flexible', () => {
    expect(Spacer.fixed(24)).toMatchObject({ flex: 0, fixedLength: 24 });
    expect(new Spacer()).toMatchObject({ flex: 1, fixedLength: 0 });
  });

  it('Row cross defaults to center, Column to stretch (spec A2)', () => {
    expect(new Row().cross).toBe('center');
    expect(new Column().cross).toBe('stretch');
  });
});

describe('vocabulary self-lowering (render)', () => {
  it('a Box renders itself to the cross-pinned style string', () => {
    // The SAME literal WebRealizerTests.Box_ExactStyleString_CrossPin (C#) and lowering.spec.ts pin.
    const box = new Box(
      new BoxStyle({
        width: 120,
        height: 40,
        padding: EdgeInsets.symmetric(16, 0),
        background: photonTheme.colors('primary').base,
        cornerRadius: new CornerRadii(10),
        borderWidth: 1,
        borderColor: photonTheme.borderStrong,
      }),
    );

    const node = box.render();
    expect(node.tag).toBe('div');
    expect(effectiveStyle(node)).toBe(
      'background-color: light-dark(#0050a0, #5ca2e8); border-radius: 10px; border: 1px solid light-dark(#c9ced6, #3d4754); box-sizing: border-box; height: 40px; padding: 0 16px 0 16px; width: 120px',
    );
  });

  it('Text without a color inherits the generated theme textPrimary', () => {
    const node = new Text('hello').render();
    expect(effectiveStyle(node)).toContain(`color: ${tokenValue(photonTheme.textPrimary)}`);
  });

  it('a web Component mixes into an abstract tree through the render seam', () => {
    const legacy = {
      render(): HtmlNode {
        return { tag: 'em', attributes: {}, events: {}, children: [] };
      },
    };
    const row = new Row(0);
    row.add(legacy as never);
    const node = row.render();
    expect(node.children[0].tag).toBe('em');
  });

  it('Pressable renders a neutralized button and wires onPressed to click', () => {
    let fired = false;
    const pressable = new Pressable(new Text('Go'), () => {
      fired = true;
    });
    const node = pressable.render();
    expect(node.tag).toBe('button');
    (node.events.click as () => void)();
    expect(fired).toBe(true);
  });
});
