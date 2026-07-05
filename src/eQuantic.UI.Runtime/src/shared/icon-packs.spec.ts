/**
 * Icon packs on the write-once architecture (client half): an IconGlyph rides the node whole —
 * stroke glyphs lower with the outline attribute set, foreign grids keep their viewBox. Byte
 * cross-pins with the C# IconRealizerTests pack cases.
 */

import { describe, expect, it } from 'vitest';
import { photonTheme } from './design-system.generated';
import { lowerVisualNode } from './lowering';
import { setPhotonTheme } from './photon-context';
import { Icon, IconGlyph } from './vocabulary';

setPhotonTheme(photonTheme);

const lower = (node: unknown) =>
  lowerVisualNode(node as never, {
    textPrimary: photonTheme.textPrimary,
    componentContext: { theme: photonTheme, typeScale: 1 },
  });

describe('icon pack glyphs (C# cross-pin)', () => {
  it('a stroke glyph lowers with the outline attribute set', () => {
    const glyph = new IconGlyph('camera', 'M14.5 4h-5L7 7H4a2 2 0 0 0-2 2v9', 'stroke');
    const node = lower(new Icon(glyph, 24));

    expect(node.tag).toBe('svg');
    expect(node.attributes['viewBox']).toBe('0 0 24 24');
    expect(node.attributes['fill']).toBe('none');
    expect(node.attributes['stroke']).toBe('currentColor');
    expect(node.attributes['stroke-width']).toBe('2');
    expect(node.children[0].attributes['d']).toBe(glyph.path);
  });

  it('a foreign-grid glyph keeps its viewBox; curated names still resolve', () => {
    const fa = new IconGlyph('bolt', 'M0 0h448v512H0z', 'fill', '0 0 448 512');
    const node = lower(new Icon(fa, 16));
    expect(node.attributes['viewBox']).toBe('0 0 448 512');
    expect(node.attributes['fill']).toBe('currentColor');

    const curated = lower(new Icon('search', 20));
    expect(curated.attributes['viewBox']).toBe('0 0 24 24');
    expect(curated.children[0].attributes['d']).toContain('M15.5 14h-.79');
  });
});
