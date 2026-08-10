/**
 * Every declaration the lowering emits carries a REAL CSS property name.
 *
 * The keys of the style objects in `lowering.ts` are the property names themselves — no conversion
 * happens between there and the stylesheet — so a key written in camelCase produces
 * `.eq-9sbvjs{borderWidth:0 1px 0 0}`. Two things then go wrong at once, and the second is the
 * expensive one:
 *
 * 1. The browser's parser drops the declaration, so the style silently does not apply.
 * 2. The class name is a hash OF the declaration text, so C# (which writes `border-width`) and the
 *    client mint DIFFERENT classes for the same style — a hydrated element gets a class the server
 *    never sent, which is the atomic pipeline's whole invariant broken.
 *
 * Most properties in that file are one word, where the mistake is impossible. It shipped on the
 * first multi-word one added in a while.
 */

import { describe, expect, it } from 'vitest';
import { effectiveStyle } from './style-atomizer';
import { lowerVisualNode } from './lowering';
import { ambientLoweringContext } from './photon-context';
import { Box, BoxStyle, Column, Text } from './vocabulary';
import { CornerRadii } from './value-types';
import { getPhotonTheme } from './photon-context';

/** A CSS property is lowercase, hyphen-separated — or a custom property, which is inline anyway. */
const VALID = /^[a-z][a-z0-9-]*$/;

function propertiesOf(node: unknown): string[] {
  const element = node as {
    attributes?: Record<string, string | undefined>;
    children?: unknown[];
  };
  const own = element.attributes
    ? effectiveStyle({ attributes: element.attributes })
        .split(';')
        .map((part) => part.split(':')[0]?.trim())
        .filter((name): name is string => Boolean(name))
    : [];
  const nested = (element.children ?? []).flatMap(propertiesOf);
  return [...own, ...nested];
}

describe('emitted CSS property names', () => {
  it('are real CSS names, not the camelCase the object key is easy to write', () => {
    const theme = getPhotonTheme();
    const tree = new Column(8, 'start', 'stretch', false, null, null, {
      padding: undefined,
    });
    // A partial border is the case that broke it: it is the only place the lowering writes
    // border-width / border-style / border-color as separate declarations.
    tree.add(
      new Box(
        new BoxStyle({
          borderWidth: 1,
          borderColor: theme.border,
          borderSides: 1, // Top
          cornerRadius: new CornerRadii(4),
        }),
        new Text('x', 'bodyM'),
      ),
    );
    tree.add(
      new Box(
        new BoxStyle({ borderWidth: 1, borderColor: theme.border, borderSides: 15 }),
        new Text('y', 'bodyM'),
      ),
    );

    const lowered = lowerVisualNode(tree as never, ambientLoweringContext());
    const properties = propertiesOf(lowered);

    expect(properties.length).toBeGreaterThan(0);
    expect(properties.filter((name) => !VALID.test(name))).toEqual([]);
  });
});
