import { describe, expect, it } from 'vitest';
import { Flexible, Row, Text } from '../index';
import { UI } from './components/UI';
import { photonTheme } from './design-system.generated';
import { lowerVisualNode } from './lowering';
import type { LoweringContext } from './lowering';
import { effectiveStyle } from './style-atomizer';

/**
 * The weight an app writes is the weight that renders (#680), on the twin. C# cross-pin:
 * FlexibleWeightTests asserts the same declarations from the server render, so the browser adopts
 * the server's DOM instead of repainting it.
 *
 * The C# constructor raised a zero weight to 1 and this twin never did: `Flexible(child, flex: 0,
 * basis: 540)` was `flex: 1 1 540px` on the server and `flex: 0 1 540px` here. Both now write the
 * zero, and both refuse a negative where it is written.
 */
const ctx: LoweringContext = { textPrimary: photonTheme.textPrimary };

/** The style of a Flexible lowered inside a row, as the C# suite reads it. */
function styleOf(flexible: Flexible): string {
  const row = new Row(0);
  row.add(flexible);
  return effectiveStyle(lowerVisualNode(row as never, ctx).children[0]) ?? '';
}

describe('a Flexible of weight zero (C# cross-pin)', () => {
  it('is written as zero and keeps its basis', () => {
    expect(styleOf(UI.flexible(new Text('t'), 0, 540))).toContain('flex: 0 1 540px');
    expect(styleOf(new Flexible(new Text('t'), 0, 540, 0))).toContain('flex: 0 0 540px');
  });

  it('starts from its content without a basis, where a weighted child keeps 0%', () => {
    expect(styleOf(new Flexible(new Text('t'), 0))).toContain('flex: 0 1 auto');
    expect(styleOf(new Flexible(new Text('t'), 2))).toContain('flex: 2 1 0%');
  });

  it('renders the same when the trailing config, the C# initializer, sets it', () => {
    expect(styleOf(new Flexible(new Text('t'), 1, 0, 1, { flex: 0, basis: 540 }))).toContain(
      'flex: 0 1 540px',
    );
  });
});

describe('the Flexible twin refuses what the C# refuses', () => {
  it('a negative weight, basis or shrink, through the parameters and the trailing config', () => {
    // The trailing config is the door a check on the parameters leaves open: it is assigned after
    // them, as C# runs an object initializer after the constructor.
    const doors: (() => Flexible)[] = [
      () => new Flexible(new Text('t'), -1),
      () => new Flexible(new Text('t'), 1, 0, 1, { flex: -1 }),
      () => UI.flexible(new Text('t'), -1),
      () => new Flexible(new Text('t'), 1, -20),
      () => new Flexible(new Text('t'), 1, 0, 1, { basis: -20 }),
      () => new Flexible(new Text('t'), 1, 0, -2),
      () => new Flexible(new Text('t'), 1, 0, 1, { shrink: -2 }),
    ];
    for (const door of doors) expect(door).toThrow(RangeError);
  });

  it('a basis that is not a finite size, and a weight or shrink C# could not hold', () => {
    expect(() => new Flexible(new Text('t'), 1, Number.NaN)).toThrow(RangeError);
    expect(() => new Flexible(new Text('t'), 1, Number.POSITIVE_INFINITY)).toThrow(RangeError);
    expect(() => new Flexible(new Text('t'), 0.5)).toThrow(RangeError);
    expect(() => new Flexible(new Text('t'), 1, 0, 0.5)).toThrow(RangeError);
  });
});

describe('a basis is written by the rule every other length uses (C# cross-pin, #692)', () => {
  it('rounds to two decimals as TokenCss.Px does, so both sides mint one class', () => {
    // Written raw, the twin wrote `540.125px` where the server wrote `540.13px`, and a float basis
    // the transpiled code produces (`Math.fround(540.12)`) wrote all of its digits.
    expect(styleOf(new Flexible(new Text('t'), 1, 540.125))).toContain('flex: 1 1 540.13px');
    expect(styleOf(new Flexible(new Text('t'), 1, Math.fround(540.12)))).toContain(
      'flex: 1 1 540.12px',
    );
  });
});
