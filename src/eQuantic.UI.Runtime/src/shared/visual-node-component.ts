/**
 * Client mirror of the C# `eQuantic.UI.Web.VisualNodeComponent` — the Core⇄Shared bridge a Core page
 * uses to compose write-once components (`new VisualNodeComponent(new Card(...))`). The transpiled
 * call imports this class from `@equantic/runtime` (the C# type carries [RuntimeProvided]); it lowers
 * the abstract subtree with the ambient Photon context, producing the same DOM the SSR side realized
 * — the hydration-parity pair the cross-pinned suites guarantee.
 */

import { HtmlElement } from '../core/types';
import type { HtmlNode } from '../core/types';
import type { ComponentChild, VisualNodeValue } from './nodes';
import { ElementRoots, enterPass, exitPass } from './element-roots';
import { lowerVisualNode } from './lowering';
import {
  ambientLoweringContext,
  getPhotonTheme,
  getPhotonTypeScale,
  setPhotonTheme,
  themedLoweringContext,
} from './photon-context';
import type { AppTheme } from './value-types';

/**
 * Extends `HtmlElement`, not `Component`, because the C# type does (`VisualNodeComponent :
 * HtmlElement`). While the DOM surface sat on `Component` the two agreed by accident; moving it
 * down one class (#245) broke the twin, and TypeScript said so exactly:
 *
 *     TS2739: Type 'VisualNodeComponent' is missing the following properties from type
 *             'HtmlElement': buildAttributes, buildEvents, htmlNode
 *
 * which is what a Core page composing `const el: HtmlElement = new VisualNodeComponent(...)` would
 * have hit — valid C# whose emitted module no longer type-checks. The twin mirrors the C#
 * hierarchy now, so the agreement is structural rather than incidental.
 */
export class VisualNodeComponent extends HtmlElement {
  private readonly node: VisualNodeValue | ComponentChild;
  private readonly theme?: AppTheme;
  private readonly typeScale: number;
  /** The element trees this bridge builds through when it renders outside a page. Nothing renders it
   *  again but its own parent, so a mark in them waits for that render, as it always has. */
  private readonly _instances = new ElementRoots(null);

  constructor(
    node: VisualNodeValue | ComponentChild,
    theme?: AppTheme,
    typeScale = 1,
    props?: unknown,
  ) {
    super(props);
    this.node = node;
    this.theme = theme;
    this.typeScale = typeScale;
  }

  render(): HtmlNode {
    const context = this.theme
      ? themedLoweringContext(this.theme, this.typeScale)
      : ambientLoweringContext();
    // The subtree reads ONE theme and one type scale, as the C# realizer's does: a stateless twin
    // builds from the ambient ones, not from this context, so the bridge's ARE the ambient ones for
    // the length of this render. A stateless checkbox under a bridge with its own theme drew the
    // page's colours on the client, over the ones the server had drawn (#701).
    const ambientTheme = getPhotonTheme();
    const ambientScale = getPhotonTypeScale();
    // Inside a host page render this JOINS the page's pass: the page owns the element trees, since
    // bridges are rebuilt every render and cannot. Standalone (tests, direct embedding) this
    // instance's own trees keep what it composes across its renders, and every render builds.
    enterPass(this._instances, null, true);
    try {
      // Inside the try, so the finally that puts the ambient theme back covers every way out.
      if (this.theme) setPhotonTheme(this.theme, this.typeScale);
      // Component roots are wire-compatible: the lowering expands them via build() (nodeKind
      // dispatch), so the cast states what the runtime already does.
      return lowerVisualNode(this.node as VisualNodeValue, context);
    } finally {
      exitPass();
      setPhotonTheme(ambientTheme, ambientScale);
    }
  }
}
