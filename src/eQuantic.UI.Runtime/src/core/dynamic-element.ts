/**
 * Client mirror of `eQuantic.UI.Core.DynamicElement` — the escape hatch for custom tags, plain
 * anchors and web components. Runtime-provided: eqc routes the C# references here instead of
 * transpiling the source (the class carries [RuntimeProvided]). Faithful to the C# `Render()`:
 * arbitrary tag, custom attributes merged over the typed ones (custom wins), `InnerText` as a
 * leading text child (`#raw` for script/style), events from the config.
 */

import type { EventHandler, HtmlNode } from './types';
import { HtmlElement } from './types';

interface DynamicElementConfig {
  tagName?: string;
  innerText?: string;
  className?: string;
  customAttributes?: Record<string, string>;
  children?: Array<{ render(): HtmlNode }>;
  onClick?: EventHandler;
}

export class DynamicElement extends HtmlElement {
  tagName = 'div';
  innerText?: string;
  className?: string;
  customAttributes?: Record<string, string>;
  children: Array<{ render(): HtmlNode }> = [];
  onClick?: EventHandler;

  constructor(config?: DynamicElementConfig) {
    super();
    if (config) Object.assign(this, config);
  }

  render(): HtmlNode {
    const children: HtmlNode[] = this.children.map((c) => c.render());

    if (this.innerText) {
      const raw = /^(script|style)$/i.test(this.tagName);
      children.unshift({
        tag: raw ? '#raw' : '#text',
        attributes: {},
        events: {},
        children: [],
        textContent: this.innerText,
      } as HtmlNode);
    }

    const attributes: Record<string, string> = {};
    if (this.className) attributes['class'] = this.className;
    if (this.customAttributes) {
      for (const key of Object.keys(this.customAttributes)) {
        attributes[key] = this.customAttributes[key];
      }
    }

    const events: Record<string, EventHandler> = {};
    if (this.onClick) events['click'] = this.onClick;

    return { tag: this.tagName, attributes, events, children } as HtmlNode;
  }
}
