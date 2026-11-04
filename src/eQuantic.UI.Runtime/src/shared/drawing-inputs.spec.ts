import { describe, expect, it } from 'vitest';
import { Drawing, Pressable, StyleDiff, VectorDrawing, VectorPaint, VectorShape } from './vocabulary';
import type { HtmlNode } from '../core/types';

/**
 * The shapes of a drawing as inputs on the web (#674). The tree both producers write is pinned by the
 * component parity fixture; what is pinned here is what the client alone does with it: the press a
 * click and a key make, and the hover a path cannot draw refused as C# refuses it.
 */
describe('a drawing whose shapes are inputs', () => {
  const map = (pressed: string[]) =>
    new Drawing(
      new VectorDrawing(0, 0, 100, 100, [
        new VectorShape('M0 0 L100 0 L0 100 Z', VectorPaint.solid({ r: 0, g: 0, b: 0, a: 255 }), undefined, undefined,
          undefined, undefined, { label: 'Porto', onPressed: () => pressed.push('Porto') }),
        new VectorShape('M0 0 L100 100', VectorPaint.none, VectorPaint.inherit, 1),
      ]),
      200,
    ).render() as HtmlNode;

  const key = (name: string) => {
    let prevented = false;
    return {
      event: { key: name, preventDefault: () => (prevented = true) } as unknown as Event,
      prevented: () => prevented,
    };
  };

  it('presses a shape on a click, on Enter and on Space, and on no other key', () => {
    const pressed: string[] = [];
    const path = map(pressed).children[0] as HtmlNode;

    path.events['click']!(new MouseEvent('click'));
    path.events['keydown']!(key('Enter').event);
    const space = key(' ');
    path.events['keydown']!(space.event);
    const letter = key('a');
    path.events['keydown']!(letter.event);

    expect(pressed).toEqual(['Porto', 'Porto', 'Porto']);
    expect(space.prevented(), 'Space presses the shape and does not scroll the page').toBe(true);
    expect(letter.prevented()).toBe(false);
  });

  it('keeps a shape with nothing to do a picture, and the group in a reader\'s reach', () => {
    const svg = map([]);
    const line = svg.children[1] as HtmlNode;

    expect(svg.attributes['role']).toBe('group');
    expect(svg.attributes['aria-hidden']).toBeUndefined();
    expect(line.attributes['role']).toBeUndefined();
    expect(line.attributes['tabindex']).toBeUndefined();
    expect(Object.keys(line.events)).toEqual([]);
  });

  it('makes a pressable around it yield, as it yields to a button inside it', () => {
    const drawing = new Drawing(
      new VectorDrawing(0, 0, 100, 100, [
        new VectorShape('M0 0 L100 0 L0 100 Z', VectorPaint.none, undefined, undefined, undefined, undefined, {
          onPressed: () => {},
        }),
      ]),
      200,
    );

    const outer = new Pressable(drawing, () => {}).render() as HtmlNode;

    expect(outer.tag, 'a button may not hold the shapes that are buttons').toBe('span');
    expect(outer.attributes['role']).toBe('button');
  });

  it('refuses a hover a path cannot draw, in the words C# uses', () => {
    expect(
      () =>
        new VectorShape('M0 0 L1 1', VectorPaint.none, undefined, undefined, undefined, undefined, {
          hover: new StyleDiff({ elevation: 2 }),
        }),
    ).toThrow(/fill \(Background\)/);
  });
});
