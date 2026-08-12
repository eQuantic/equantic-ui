import { describe, it, expect } from 'vitest';
import { Component, type EventHandler, type HtmlNode } from './types';

/** Concrete probe that exposes the protected buildEvents() for assertions. */
class Probe extends Component {
  render(): HtmlNode {
    return { tag: 'div', attributes: {}, events: this.events(), children: [] };
  }
  events(): Record<string, EventHandler> {
    return this.buildEvents();
  }
}

describe('Component.buildEvents', () => {
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
});
