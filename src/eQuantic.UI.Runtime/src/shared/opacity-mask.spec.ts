/**
 * A subtree faded along a gradient, lowered in the browser (#1938): the twin of the C# `OpacityMaskRealizerTests`.
 * ONE wrapper `div` carrying `mask-image`, the mask's RESOLVED stops written as CSS writes a gradient by
 * `opacityGradientCss`, and the width contract of the child it stands in for. Every literal below is the one the C# test
 * asserts, so the server's `OpacityGradientCss` and this twin cannot drift, and the `opacity-mask-*` cases of the component parity
 * fixture pin their atomic class names to each other.
 */

import { describe, expect, it } from 'vitest';
import { effectiveStyle } from './style-atomizer';
import { Box, BoxStyle, Hoverable, LinearOpacityGradient, OpacityMask, OpacityStop, RadialExtent, RadialOpacityGradient, Row, Stack, Positioned } from './vocabulary';
import { Point, SizeValue } from './value-types';

const ramp = [new OpacityStop(0, 1), new OpacityStop(1, 0)];
const hugging = () => new Box(new BoxStyle({ width: 120, height: 80 }));

/** The `mask-image` of the wrapper a mask lowers to. */
function maskImage(gradient: LinearOpacityGradient | RadialOpacityGradient): string {
  const style = effectiveStyle(new OpacityMask(gradient, hugging()).render());
  const match = /mask-image: (.*?)(?:; [a-z-]+: |$)/.exec(style);
  expect(match, `the wrapper carries the gradient: ${style}`).not.toBeNull();
  return match![1];
}

describe('opacityGradientCss writes each row of the C# OpacityMaskRealizerTests', () => {
  it('a linear mask', () => {
    expect(maskImage(new LinearOpacityGradient(135, ramp))).toBe('linear-gradient(135deg, rgb(0 0 0 / 1) 0%, rgb(0 0 0 / 0) 100%)');
  });

  it('the resolved stops, never the stops as given', () => {
    expect(maskImage(new LinearOpacityGradient(90, [new OpacityStop(0.6, 1), new OpacityStop(0.3, 0)]))).toBe(
      'linear-gradient(90deg, rgb(0 0 0 / 1) 60%, rgb(0 0 0 / 0) 60%)',
    );
  });

  it('a single stop, twice', () => {
    expect(maskImage(new LinearOpacityGradient(90, [new OpacityStop(0.3, 0.5)]))).toBe(
      'linear-gradient(90deg, rgb(0 0 0 / 0.5) 0%, rgb(0 0 0 / 0.5) 100%)',
    );
  });

  it("the hero's field", () => {
    expect(
      maskImage(new RadialOpacityGradient(new Point(0.7, 0.35), RadialExtent.fraction(0.7, 0.7), [new OpacityStop(0.35, 1), new OpacityStop(0.76, 0)])),
    ).toBe('radial-gradient(70% 70% at 70% 35%, rgb(0 0 0 / 1) 35%, rgb(0 0 0 / 0) 76%)');
  });

  it('a side keyword', () => {
    expect(maskImage(new RadialOpacityGradient(new Point(0.5, 0.5), RadialExtent.closestSide, ramp))).toBe(
      'radial-gradient(closest-side at 50% 50%, rgb(0 0 0 / 1) 0%, rgb(0 0 0 / 0) 100%)',
    );
  });

  it.each([
    ['farthest-side', RadialExtent.farthestSide],
    ['closest-corner', RadialExtent.closestCorner],
    ['farthest-corner', new RadialExtent()],
  ])('the %s keyword', (keyword, extent) => {
    expect(maskImage(new RadialOpacityGradient(new Point(0.2, 0.9), extent, ramp))).toBe(
      `radial-gradient(${keyword} at 20% 90%, rgb(0 0 0 / 1) 0%, rgb(0 0 0 / 0) 100%)`,
    );
  });

  it('two lengths in dp', () => {
    expect(maskImage(new RadialOpacityGradient(new Point(0.5, 0.5), RadialExtent.dp(120, 80.5), ramp))).toBe(
      'radial-gradient(120px 80.5px at 50% 50%, rgb(0 0 0 / 1) 0%, rgb(0 0 0 / 0) 100%)',
    );
  });

  it('the decimals of a float, not of the double it widens to', () => {
    // 0.055f * 100 is 5.5 and 0.325f * 100 is 32.5 in a float; the doubles say 5.4999999... and 32.4999988...
    expect(maskImage(new LinearOpacityGradient(200.5, [new OpacityStop(0.055, 1), new OpacityStop(0.325, 0.2)]))).toBe(
      'linear-gradient(200.5deg, rgb(0 0 0 / 1) 5.5%, rgb(0 0 0 / 0.2) 32.5%)',
    );
  });

  it('an offset outside 0..1', () => {
    expect(maskImage(new LinearOpacityGradient(90, [new OpacityStop(-0.5, 1), new OpacityStop(1.5, 0)]))).toBe(
      'linear-gradient(90deg, rgb(0 0 0 / 1) -50%, rgb(0 0 0 / 0) 150%)',
    );
  });
});

describe('the wrapper', () => {
  it('is one div holding the child, and writes no pointer-events', () => {
    const node = new OpacityMask(new LinearOpacityGradient(90, ramp), hugging()).render();

    expect(node.tag).toBe('div');
    expect(node.children).toHaveLength(1);
    expect(node.children[0].tag).toBe('div');
    expect(effectiveStyle(node)).toContain('mask-image: linear-gradient(90deg');
    expect(effectiveStyle(node)).not.toContain('pointer-events');
  });

  it('leaves a hugging child without a size', () => {
    const style = effectiveStyle(new OpacityMask(new LinearOpacityGradient(90, ramp), hugging()).render());

    expect(style).not.toContain('width');
    expect(style).not.toContain('height');
  });

  it("carries a Fill child's 100% and its cap", () => {
    const capped = new Box(new BoxStyle({ width: SizeValue.fill, maxWidth: 980, height: SizeValue.fill }));

    const style = effectiveStyle(new OpacityMask(new LinearOpacityGradient(90, ramp), capped).render());

    expect(style).toContain('width: 100%');
    expect(style).toContain('height: 100%');
    expect(style).toContain('max-width: 980px');
  });

  it("is a row's item with the child's contract", () => {
    const row = new Row(0, { width: SizeValue.fill, main: 'center' });
    row.add(new OpacityMask(new LinearOpacityGradient(90, ramp), new Box(new BoxStyle({ width: SizeValue.fill, maxWidth: 980 }))));

    const wrapper = row.render().children[0];

    expect(effectiveStyle(wrapper)).toContain('width: 100%');
    expect(effectiveStyle(wrapper)).toContain('max-width: 980px');
  });

  it('still spans between its anchors when it is a positioned box', () => {
    const stack = new Stack('topStart', { width: SizeValue.fixed(200), height: SizeValue.fixed(90) });
    stack.add(
      new Positioned(
        new OpacityMask(new LinearOpacityGradient(90, ramp), new Box(new BoxStyle({ width: SizeValue.fill, height: SizeValue.fill }))),
        30,
        0,
      ),
    );

    const anchor = stack.render().children[0];

    expect(effectiveStyle(anchor)).toContain('inset-inline-start: 0');
    expect(effectiveStyle(anchor)).toContain('bottom: 0');
    expect(effectiveStyle(anchor.children[0])).toContain('width: 100%');
    expect(effectiveStyle(anchor.children[0])).toContain('height: 100%');
  });

  it('passes its fill and its cap through a hoverable around it (the browser `fills` and `capsAt`)', () => {
    const capped = new Box(new BoxStyle({ width: SizeValue.fill, maxWidth: 320 }));

    const host = effectiveStyle(new Hoverable(new OpacityMask(new LinearOpacityGradient(90, ramp), capped), () => {}).render());

    expect(host).toContain('width: 100%');
    expect(host).toContain('max-width: 320px');
  });
});
