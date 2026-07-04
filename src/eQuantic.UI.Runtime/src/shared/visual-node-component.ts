/**
 * Client mirror of the C# `eQuantic.UI.Web.VisualNodeComponent` — the Core⇄Shared bridge a Core page
 * uses to compose write-once components (`new VisualNodeComponent(new Card(...))`). The transpiled
 * call imports this class from `@equantic/runtime` (the C# type carries [RuntimeProvided]); it lowers
 * the abstract subtree with the ambient Photon context, producing the same DOM the SSR side realized
 * — the hydration-parity pair the cross-pinned suites guarantee.
 */

import { Component } from '../core/types';
import type { HtmlNode } from '../core/types';
import type { VisualNodeValue } from './nodes';
import { lowerVisualNode } from './lowering';
import { ambientLoweringContext } from './photon-context';
import type { AppTheme } from './value-types';

export class VisualNodeComponent extends Component {
  private readonly node: VisualNodeValue;
  private readonly theme?: AppTheme;

  constructor(node: VisualNodeValue, theme?: AppTheme, _typeScale?: number, props?: unknown) {
    super(props);
    this.node = node;
    this.theme = theme;
  }

  render(): HtmlNode {
    const context = this.theme
      ? { textPrimary: this.theme.textPrimary, componentContext: { theme: this.theme, typeScale: 1 } }
      : ambientLoweringContext();
    return lowerVisualNode(this.node, context);
  }
}
