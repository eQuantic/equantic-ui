/**
 * Icon packs on the write-once architecture (client half): an IconGlyph rides the node whole —
 * stroke glyphs lower with the outline attribute set, foreign grids keep their viewBox. Byte
 * cross-pins with the C# IconRealizerTests pack cases.
 */

import { describe, expect, it } from 'vitest';
import { photonTheme } from './design-system.generated';
import { lowerVisualNode } from './lowering';
import { setPhotonTheme } from './photon-context';
import { Icon, IconGlyph, Vector } from './vocabulary';
import { SharedStatefulComponent } from '../core/component';

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

const nextFrame = () =>
  new Promise<void>((resolve) =>
    requestAnimationFrame(() => requestAnimationFrame(() => resolve())),
  );

/**
 * The client-dynamic fence proven end to end: a stateful component swaps between two INLINED pack
 * glyphs (what eqc emits for `LucideIcons.Camera` / `LucideIcons.Heart`) on setState, and the real
 * DOM path updates — no pack module, just the glyph constructors the compiler inlines.
 */
class GlyphToggle extends SharedStatefulComponent {
  private cameraShown = true;
  private readonly camera = new IconGlyph('camera', 'M14.5 4h-5L7 7', 'stroke');
  private readonly heart = new IconGlyph('heart', 'M12 21l-1-1');

  build(): Icon {
    return new Icon(this.cameraShown ? this.camera : this.heart, 24) as never;
  }

  toggle(): void {
    this.setState(() => {
      this.cameraShown = !this.cameraShown;
    });
  }
}

describe('inlined pack glyph in a client re-render', () => {
  it('swaps the DOM path when state changes', async () => {
    const container = document.createElement('div');
    document.body.appendChild(container);

    const toggle = new GlyphToggle();
    toggle.mount(container);

    const path = () => container.querySelector('path')!.getAttribute('d');
    const svgFill = () => container.querySelector('svg')!.getAttribute('fill');
    expect(path()).toBe('M14.5 4h-5L7 7');
    expect(svgFill()).toBe('none'); // camera is a stroke glyph

    toggle.toggle();
    await nextFrame();
    expect(path()).toBe('M12 21l-1-1');
    expect(svgFill()).toBe('currentColor'); // heart is a fill glyph

    container.remove();
  });

  it('a vector lowers like an icon at an authored size (C# cross-pin)', () => {
    const glyph = new IconGlyph('sunburst', 'M10 10 L90 10 L90 90 Z', 'fill', '0 0 100 100');
    const node = lower(new Vector(glyph, 300, photonTheme.textPrimary));

    expect(node.tag).toBe('svg');
    expect(node.attributes['viewBox']).toBe('0 0 100 100');
    expect(node.attributes['fill']).toBe('currentColor');
    expect(node.children[0]?.attributes['d']).toBe('M10 10 L90 10 L90 90 Z');
  });
});
