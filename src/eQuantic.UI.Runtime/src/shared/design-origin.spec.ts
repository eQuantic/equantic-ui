import { describe, it, expect } from 'vitest';
import { lowerVisualNode } from './lowering';
import { photonTheme } from './design-system.generated';
import type { LoweringContext } from './lowering';
import type { HtmlNode } from '../core/types';
import type { VisualNodeValue } from './nodes';

const ctx: LoweringContext = { textPrimary: photonTheme.textPrimary };

const node = (extra: Record<string, unknown>): VisualNodeValue =>
  ({ nodeKind: 'box', style: {}, children: [], ...extra }) as unknown as VisualNodeValue;

function walk(html: HtmlNode): HtmlNode[] {
  return [html, ...html.children.flatMap(walk)];
}

const originsOf = (html: HtmlNode): (string | undefined)[] =>
  walk(html).map((n) => n.attributes['data-eq-origin']);

/**
 * `VisualNode.Origin` reaching the DOM as `data-eq-origin` — the client half of the identity a visual
 * editor selects on.
 *
 * The C# twin is `DesignOriginAttributeTests`. Both producers render the same tree on either side of
 * hydration, so the attribute NAME and the "only when set" rule have to be pinned in both places: if
 * one emitted an attribute the other did not, the reconciler would see a diverged subtree and replace
 * it — and the failure would show up as flicker on hydration, nowhere near this code.
 */
describe('design-mode origin (C# cross-pin)', () => {
  it('a node with an origin carries it as a data attribute', () => {
    const origin = '/src/Screens/Payments.cs|41:8|41:52';
    expect(originsOf(lowerVisualNode(node({ origin }), ctx))).toContain(origin);
  });

  it('without an origin nothing is emitted', () => {
    // Production is every build where nothing set an origin. An empty `data-eq-origin=""` would
    // still be an attribute the C# producer does not write.
    expect(originsOf(lowerVisualNode(node({}), ctx))).toEqual(expect.arrayContaining([undefined]));
    expect(originsOf(lowerVisualNode(node({}), ctx)).filter(Boolean)).toHaveLength(0);
  });

  it('origins are per node, not inherited', () => {
    // A Box holds ONE child, like its C# twin.
    const tree = node({
      origin: '/src/Screen.cs|10:0|10:20',
      child: node({ origin: '/src/Screen.cs|11:4|11:40' }),
    });

    expect(originsOf(lowerVisualNode(tree, ctx)).filter(Boolean)).toEqual([
      '/src/Screen.cs|10:0|10:20',
      '/src/Screen.cs|11:4|11:40',
    ]);
  });
});
