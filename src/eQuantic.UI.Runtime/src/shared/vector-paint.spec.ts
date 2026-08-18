import { describe, expect, it } from 'vitest';
import { Drawing, VectorDrawing, VectorPaint, VectorShape } from './vocabulary';
import type { HtmlNode } from '../core/types';

/**
 * The paints an artwork names rather than builds.
 *
 * `VectorPaint.None` and `.Inherit` are static FIELDS in C#, so eqc emits a field access and the
 * mirror has to answer with a value. It answered with a method, so every stroked shape reached the
 * lowering holding a FUNCTION: `kind` came back undefined, the paint fell through to "none", and
 * the shape drew nothing. It rendered correctly from the server — where the fields are real — and
 * vanished the moment the page hydrated, which is why a mouth, a blink and a pair of eyes went
 * missing and a gradient-filled body did not.
 */
describe('VectorPaint the way C# declares it', () => {
  it('none and inherit are values, not functions', () => {
    expect(typeof VectorPaint.none).not.toBe('function');
    expect(VectorPaint.none.kind).toBe('none');
    expect(VectorPaint.inherit.kind).toBe('inherit');
  });

  it('a stroked shape survives the client lowering', () => {
    const artwork = new VectorDrawing(0, 0, 200, 200, [
      new VectorShape('M 81 118 H 99', VectorPaint.none, VectorPaint.inherit, 3.5),
    ]);
    const svg = new Drawing(artwork, 200, 200).render() as HtmlNode;
    const path = svg.children[0] as HtmlNode;

    expect(path.tag).toBe('path');
    // `currentColor` is what makes the mark follow the theme's ink. "none" here is the bug's own
    // signature: an undefined kind falls through to it, and the line is drawn invisibly.
    expect(path.attributes['stroke']).toBe('currentColor');
    expect(path.attributes['stroke-width']).toBe('3.5');
    expect(path.attributes['fill']).toBe('none');
  });
});
