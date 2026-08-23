import { describe, expect, it } from 'vitest';
import { Text } from './vocabulary';
import { lowerVisualNode } from './lowering';
import type { ColorTokenValue, VisualNodeValue } from './nodes';

/**
 * The document's OUTLINE, on the twin (C# cross-pin: HeadingOutlineTests).
 *
 * The tag is the whole contract here: the server renders the heading and the browser hydrates it,
 * so a span on one side and an h2 on the other is a replaced subtree at best and a silently
 * different document at worst.
 */
const textPrimary: ColorTokenValue = {
  light: { r: 0x17, g: 0x1b, b: 0x21, a: 255 },
  dark: { r: 0xf2, g: 0xf4, b: 0xf7, a: 255 },
};
const ctx = { textPrimary };

const tagOf = (node: unknown): string => (node as { tag: string }).tag;

describe('heading outline', () => {
  for (const level of [1, 2, 3, 4, 5, 6]) {
    it(`level ${level} lowers to h${level}`, () => {
      const text = new Text('Portfolio', 'heading', null, 0, undefined, undefined, undefined, undefined, level);
      expect(tagOf(lowerVisualNode(text as unknown as VisualNodeValue, ctx))).toBe(`h${level}`);
    });
  }

  it('no level stays a span — the default cannot be a heading', () => {
    const text = new Text('Total', 'bodyL');
    expect(tagOf(lowerVisualNode(text as unknown as VisualNodeValue, ctx))).toBe('span');
  });

  it('a node literal without the field is a span, not an h0', () => {
    // Hydration receives PLAIN OBJECTS, and one built before this field existed has no
    // headingLevel at all. Reading it as a number would produce `h0`, which is not an element.
    const legacy = { nodeKind: 'text', content: 'Total', role: 'bodyL', maxLines: 0 };
    expect(tagOf(lowerVisualNode(legacy as unknown as VisualNodeValue, ctx))).toBe('span');
  });

  it('a level HTML does not have degrades to a span, it does not take the page down', () => {
    // The lowering reads PLAIN OBJECTS from hydration, so it cannot trust the field. `h7` is not
    // an element and `h-1` is not a tag; both were reachable from a malformed payload.
    for (const bad of [7, -1, 1.5, '2', null, Number.NaN]) {
      const payload = { nodeKind: 'text', content: 'Total', role: 'bodyL', maxLines: 0, headingLevel: bad };
      expect(tagOf(lowerVisualNode(payload as unknown as VisualNodeValue, ctx))).toBe('span');
    }
  });

  it('the Text CLASS refuses the same values, because there the caller is code', () => {
    const seven = () => new Text('Nope', 'bodyL', null, 0, undefined, undefined, undefined, undefined, 7);
    expect(seven).toThrow(RangeError);

    // The config form is the other door into the same field.
    const viaConfig = () => new Text('Nope', 'bodyL', null, 0, { headingLevel: 7 });
    expect(viaConfig).toThrow(RangeError);
  });

  it('the level does not touch the paint — only the tag differs', () => {
    const plain = lowerVisualNode(new Text('Portfolio', 'heading') as unknown as VisualNodeValue, ctx);
    const levelled = lowerVisualNode(
      new Text('Portfolio', 'heading', null, 0, undefined, undefined, undefined, undefined, 1) as unknown as VisualNodeValue,
      ctx,
    );

    expect(tagOf(levelled)).toBe('h1');
    expect((levelled as { attributes: unknown }).attributes).toEqual(
      (plain as { attributes: unknown }).attributes,
    );
  });
});
