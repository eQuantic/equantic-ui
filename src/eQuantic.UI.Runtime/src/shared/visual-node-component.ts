/**
 * Client mirror of the C# `eQuantic.UI.Web.VisualNodeComponent` — the Core⇄Shared bridge a Core page
 * uses to compose write-once components (`new VisualNodeComponent(new Card(...))`). The transpiled
 * call imports this class from `@equantic/runtime` (the C# type carries [RuntimeProvided]); it lowers
 * the abstract subtree with the ambient Photon context, producing the same DOM the SSR side realized
 * — the hydration-parity pair the cross-pinned suites guarantee.
 */

import { Component } from '../core/types';
import type { HtmlNode } from '../core/types';
import type { ComponentChild, VisualNodeValue } from './nodes';
import { ComponentInstanceStore, enterPass, exitPass } from './instance-store';
import { lowerVisualNode } from './lowering';
import { ambientLoweringContext } from './photon-context';
import type { AppTheme } from './value-types';

export class VisualNodeComponent extends Component {
  private readonly node: VisualNodeValue | ComponentChild;
  private readonly theme?: AppTheme;
  private readonly _instances = new ComponentInstanceStore();

  constructor(node: VisualNodeValue | ComponentChild, theme?: AppTheme, _typeScale?: number, props?: unknown) {
    super(props);
    this.node = node;
    this.theme = theme;
  }

  render(): HtmlNode {
    const context = this.theme
      ? {
          textPrimary: this.theme.textPrimary,
          componentContext: { theme: this.theme, typeScale: 1 },
        }
      : ambientLoweringContext();
    // Reconciler pass join (W6 slice 2): inside a host page render this JOINS the page's pass —
    // the page owns retention (bridges are rebuilt every pass and cannot). Standalone (tests,
    // direct embedding) this instance's own store carries retention across its renders.
    enterPass(this._instances, null);
    try {
      // Component roots are wire-compatible: the lowering expands them via build() (nodeKind
      // dispatch), so the cast states what the runtime already does.
      return lowerVisualNode(this.node as VisualNodeValue, context);
    } finally {
      exitPass();
    }
  }
}
