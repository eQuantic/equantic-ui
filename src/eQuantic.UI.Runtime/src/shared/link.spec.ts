import { describe, expect, it } from 'vitest';
import { effectiveStyle } from './style-atomizer';
import { Box, BoxStyle, Link, Text } from './vocabulary';
import { lowerVisualNode } from './lowering';
import { ambientLoweringContext } from './photon-context';

/**
 * The client half of the write-once Link: a real `<a href>` the SPA router intercepts (guards and
 * prefetch apply for free), UA chrome neutralized by the generated .eq-link — identical DOM to the
 * C# SSR realizer, so hydration adopts it untouched.
 */
describe('link lowering (navigation semantics)', () => {
  const lower = (node: Link) => lowerVisualNode(node, ambientLoweringContext());

  it('lowers to a real anchor with the child inside', () => {
    const html = lower(new Link('/users/2', new Text('Open', 'label')))!;

    expect(html.tag).toBe('a');
    expect(html.attributes['href']).toBe('/users/2');
    // `eq-link` plus the atomic class that declares the anchor a hit target — a transparent row
    // above it now carries `pointer-events: none`, which inherits.
    expect(html.attributes['class']).toContain('eq-link');
    expect(effectiveStyle(html)).toContain('pointer-events: auto');
    expect(html.children).toHaveLength(1);
  });

  // The router reads this off the anchor to decide where the new page starts (C# twin).
  it('marks a link that keeps the reader position', () => {
    expect(
      lower(new Link('/a', new Text('a', 'label')))!.attributes['data-eq-keep-position'],
    ).toBeUndefined();
    expect(
      lower(new Link('/a', new Text('a', 'label'), { keepsPosition: true }))!.attributes[
        'data-eq-keep-position'
      ],
    ).toBe('');
  });

  // An app-internal link invites the router to warm it on hover; an external one does not (C# twin).
  it('invites prefetch for app links only', () => {
    expect(lower(new Link('/docs/b', new Text('b', 'label')))!.attributes['data-prefetch']).toBe(
      '',
    );
    expect(
      lower(new Link('https://example.test/x', new Text('x', 'label')))!.attributes[
        'data-prefetch'
      ],
    ).toBeUndefined();
  });

  it('carries the accessible label for icon-only links', () => {
    const html = lower(new Link('/x', new Box(new BoxStyle({})), { label: 'Home' }))!;
    expect(html.attributes['aria-label']).toBe('Home');
  });
});
