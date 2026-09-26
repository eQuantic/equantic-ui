import { describe, expect, it } from 'vitest';
import type { HtmlNode } from '../core/types';
import type { VisualNodeValue } from './nodes';
import { lowerVisualNode } from './lowering';
import { dictionary } from '../utils/dictionary';

/**
 * The browser's half of "one door per node".
 *
 * `nodeKind` is a GENERATED union now (`node-kinds.generated.ts`) rather than `string`, and
 * `lowerNodeKind` ends in `assertNever` — so a kind the vocabulary declares with no case in the
 * switch stops `tsc`, which is the runtime's own build. That guarantee is a COMPILE-time one and
 * cannot be asserted from a test; what a test can hold is the two runtime behaviours the change
 * moved, and they are what this file is for.
 */
const context = { textPrimary: { light: { r: 0, g: 0, b: 0, a: 255 }, dark: { r: 255, g: 255, b: 255, a: 255 } } };

describe('the mixing seam, now ahead of the switch', () => {
  it('embeds what a web component renders for itself', () => {
    // A transpiled shared component or a low-level HtmlElement composed into an abstract tree. It
    // is handed to the lowering as a VisualNodeValue and is not one: it carries NO nodeKind (the
    // absence is the signal — see code-editor.spec.ts, which pins it on the class side) and renders
    // its own HtmlNode. Before the union this fell out of a default arm; the default arm is now the
    // compiler's, so the seam had to become what it always really was — a test for the absence.
    const own: HtmlNode = { tag: 'aside', attributes: { id: 'mine' }, events: {}, children: [] };
    const webComponent = { render: () => own } as unknown as VisualNodeValue;

    expect(lowerVisualNode(webComponent, context)).toBe(own);
  });

  it("takes a node built in C# with its bags as plain objects, which every later pass writes by name", () => {
    // A consumer's HtmlElement builds its node in C#, where Attributes and Events are dictionaries.
    // The passes after the seam stamp a bookmark, an origin, a drag handle or an alignment onto the
    // node's attributes by name, which a dictionary would have taken as a stray property.
    const click = () => {};
    const built = {
      tag: 'aside',
      attributes: dictionary([['id', 'mine']]),
      events: dictionary([['click', click]]),
      children: [],
    } as unknown as HtmlNode;
    const webComponent = { render: () => built } as unknown as VisualNodeValue;

    const lowered = lowerVisualNode(webComponent, context);
    expect(lowered.attributes).toEqual({ id: 'mine' });
    expect(lowered.events).toEqual({ click });
  });

  it('answers nothing for a foreign object that cannot render either', () => {
    // No nodeKind and no render: there is nothing to draw and nothing to refuse. The lowering's
    // public entry substitutes its empty span, exactly as it did before.
    const stranger = { notANode: true } as unknown as VisualNodeValue;

    expect(lowerVisualNode(stranger, context).tag).toBe('span');
  });
});

describe('a kind this bundle has never heard of', () => {
  it('throws instead of rendering nothing', () => {
    // UNREACHABLE by any typed path — that is the point of the union — so this is the behaviour at
    // the one door left open: an object that lied about its type, which in practice means a tree
    // built past the types or a bundle mixed with another build. It used to return null, and a
    // silently missing piece of a page is the exact defect the union was added to prevent; failing
    // loudly is the repo's rule applied to the case the compiler cannot reach.
    const impostor = { nodeKind: 'holograph' } as unknown as VisualNodeValue;

    expect(() => lowerVisualNode(impostor, context)).toThrow(/Unhandled node kind/);
  });
});
