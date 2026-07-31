import { describe, expect, it } from 'vitest';
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
    expect(html.attributes['class']).toBe('eq-link');
    expect(html.children).toHaveLength(1);
  });

  it('carries the accessible label for icon-only links', () => {
    const html = lower(new Link('/x', new Box(new BoxStyle({})), { label: 'Home' }))!;
    expect(html.attributes['aria-label']).toBe('Home');
  });
});
