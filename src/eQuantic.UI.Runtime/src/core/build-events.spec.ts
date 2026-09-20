import { describe, it, expect } from 'vitest';
import { HtmlElement, type EventHandler, type HtmlNode } from './types';

/**
 * Concrete probe that exposes the protected buildEvents() for assertions — on `HtmlElement`,
 * which is where the DOM surface lives. It hung off `Component` before #245, where every
 * component in the tree paid for it; the behaviour is unchanged and the base is not.
 */
class Probe extends HtmlElement {
  render(): HtmlNode {
    return { tag: 'div', attributes: {}, events: this.events(), children: [] };
  }
  events(): Record<string, EventHandler> {
    return this.buildEvents();
  }
}

describe('HtmlElement.buildEvents', () => {
  it('discovers native on* handlers (onClick -> click)', () => {
    const fn = () => {};
    const p = new Probe({ onClick: fn });
    expect(p.events()).toEqual({ click: fn });
  });

  it('spells double-click the way the DOM does (onDoubleClick -> dblclick)', () => {
    // Lowercasing alone yields "doubleclick" — a listener that attaches fine and fires never.
    // The C# side maps OnDoubleClick -> "dblclick" (HtmlElement.EventNameMap); the mirror must too.
    const fn = () => {};
    const p = new Probe({ onDoubleClick: fn, onMouseEnter: fn });
    expect(p.events()).toEqual({ dblclick: fn, mouseenter: fn });
  });

  it('merges forwarded customEvents (composite components forward to a child element)', () => {
    const handler = () => {};
    // Mirrors Button -> Box: the resolved handler set is forwarded via customEvents (already keyed
    // by DOM event name). Without the merge the child rebuilds from its own on* props and drops it.
    const p = new Probe({ customEvents: { click: handler } });
    expect(p.events()).toEqual({ click: handler });
  });

  it('customEvents take precedence over a same-named native handler', () => {
    const native = () => {};
    const custom = () => {};
    const p = new Probe({ onClick: native, customEvents: { click: custom } });
    expect(p.events().click).toBe(custom);
  });

  it('ignores non-function custom entries', () => {
    const p = new Probe({ customEvents: { click: undefined as unknown as EventHandler } });
    expect(p.events()).toEqual({});
  });

  /**
   * WHAT THE ESCAPE HATCH INHERITS, and why deleting it broke code no test in this repo runs.
   *
   * `buildAttributes` and `buildEvents` have no caller in the RUNTIME — which is true, and was the
   * reason #245 deleted them. A consumer's own `class MyTag : HtmlElement` is the caller, and eqc
   * lowers its `BuildAttributes()` to `this.buildAttributes()`:
   *
   * ```js
   * export class MyTag extends HtmlElement {
   *     render() { let a = this.buildAttributes(); let e = this.buildEvents(); … }
   * }
   * ```
   *
   * So the base a consumer extends has to carry both. That is `HtmlElement`, not `Component`: a
   * component carries none of this, which is the whole of #245, and the C# side already puts the DOM
   * surface exactly here.
   */
  it('gives a subclass the two builders eqc emits calls to', () => {
    const probe = new Probe() as unknown as Record<string, unknown>;
    expect(typeof probe['buildAttributes']).toBe('function');
    expect(typeof probe['buildEvents']).toBe('function');
  });
});
