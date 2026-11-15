import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { EscapeHatchPage, StatefulComponent, StatelessComponent, componentIdentity, resetComponentKeys } from './component';
import { HtmlElement, type HtmlNode } from './types';
import type { VisualNodeValue } from '../shared/nodes';
import { Column, Text } from '../shared/vocabulary';
import { VisualNodeComponent } from '../shared/visual-node-component';
import { ElementRoots, enterPass, exitPass } from '../shared/element-roots';
import { lowerVisualNode } from '../shared/lowering';
import { ambientLoweringContext } from '../shared/photon-context';

interface Payload {
  __INITIAL_STATE__?: Record<string, Record<string, unknown>>;
}
const win = window as unknown as Payload;

/**
 * The SSR-to-client seam holds for every page shape and names every component by what it IS.
 *
 * Two defects, one seam. An escape-hatch page — a page written as DOM, routed with `MapPage<T>` —
 * was served and never mounted: the boot script's only door for it was a `render()` that attaches
 * nothing (#279). And a component was keyed by its SIMPLE name, so two `Row`s from different
 * namespaces looked the same to the one check that makes a drift between the two trees safe (#278).
 */
describe('the SSR-to-client seam', () => {
  beforeEach(() => resetComponentKeys());
  afterEach(() => {
    delete win.__INITIAL_STATE__;
    document.body.innerHTML = '';
    resetComponentKeys();
  });

  class Probe extends StatefulComponent {
    static $typeId = 'App.Components.Probe';
    static $hydration = { label: 'declared' } as const;
    label = 'default';
    build(): VisualNodeValue {
      return new Text(this.label);
    }
  }

  /** An escape-hatch page: DOM of its own, and two write-once subtrees through two bridges. */
  class Host extends HtmlElement {
    static $typeId = 'App.Pages.Host';
    static $hydration = { title_: 'declared' } as const;
    clicks = 0;
    title_ = 'none';
    render(): HtmlNode {
      return {
        tag: 'main',
        attributes: {},
        events: {},
        children: [
          {
            tag: 'button',
            attributes: { id: 'go' },
            events: { click: () => { this.clicks++; } },
            children: [{ tag: '#text', attributes: {}, events: {}, children: [], textContent: this.title_ }],
          },
          new VisualNodeComponent(new Probe()).render(),
          new VisualNodeComponent(new Probe()).render(),
        ],
      };
    }
  }

  const host = (): HTMLElement => {
    const element = document.createElement('div');
    document.body.appendChild(element);
    return element;
  };

  it('hydrates the markup an escape-hatch page was served, and its handlers work', () => {
    const served = host();
    new EscapeHatchPage(new Host()).mount(served);
    const ssr = served.innerHTML;

    const root = host();
    root.innerHTML = ssr;
    const button = root.querySelector('#go');
    const page = new Host();

    new EscapeHatchPage(page).hydrate(root);
    (root.querySelector('#go') as HTMLButtonElement).click();

    expect(root.querySelector('#go')).toBe(button); // adopted, not replaced
    expect(page.clicks).toBe(1);
  });

  it('is reachable by a client-side navigation, and leaves by one', () => {
    class Other extends StatelessComponent {
      build(): VisualNodeValue {
        return new Text('other page');
      }
    }
    const root = host();
    const first = new Other();
    first.mount(root);

    const escape = new EscapeHatchPage(new Host());
    const tree = escape.mountReconcile(root, first.getCurrentTree());
    first.disposeQuietly();
    expect(root.querySelector('#go')).not.toBeNull();

    const back = new Other();
    back.mountReconcile(root, escape.getCurrentTree());
    escape.disposeQuietly();
    expect(tree).not.toBeNull();
    expect(root.textContent).toContain('other page');
    expect(root.querySelector('#go')).toBeNull();
  });

  it('names the next page\'s components from ITS payload when a navigation lands, a stateless one nested among them', () => {
    // A component without state is a `component` node too, so a page's nested stateless component
    // has a position and is named by it (#1477). Hydration defects show on NAVIGATION rather than on
    // the first load: the second page has to read the payload the router handed it, as a walk of its own.
    class Section extends StatelessComponent {
      static $typeId = 'App.Components.Section';
      static $hydration = { heading: 'declared' } as const;
      heading = 'no heading';
      constructor(readonly inner: Probe) {
        super();
      }
      build(): VisualNodeValue {
        const column = new Column();
        column.children.push(new Text(this.heading) as never, this.inner as never);
        return column;
      }
    }
    class FirstPage extends StatelessComponent {
      static $typeId = 'App.Pages.First';
      build(): VisualNodeValue {
        const column = new Column();
        column.children.push(new Section(new Probe()) as never);
        return column;
      }
    }
    class SecondPage extends StatelessComponent {
      static $typeId = 'App.Pages.Second';
      build(): VisualNodeValue {
        const column = new Column();
        column.children.push(new Section(new Probe()) as never, new Probe() as never);
        return column;
      }
    }
    win.__INITIAL_STATE__ = {
      'r0/App.Components.Section@0': { heading: 'first heading' },
      'r0/App.Components.Probe@0.0.1': { label: 'first page' },
    };
    const root = host();
    const first = new FirstPage();
    first.mount(root);
    expect(root.textContent).toContain('first heading');
    expect(root.textContent).toContain('first page');

    // The router replaces the hydration state with the next page's, then mounts it over the first.
    win.__INITIAL_STATE__ = {
      'r0/App.Components.Section@0': { heading: 'second heading' },
      'r0/App.Components.Probe@0.0.1': { label: 'in the section' },
      'r0/App.Components.Probe@1': { label: 'beside it' },
    };
    const second = new SecondPage();
    second.mountReconcile(root, first.getCurrentTree());
    first.disposeQuietly();

    expect(root.textContent).toContain('second heading');
    expect(root.textContent).toContain('in the section');
    expect(root.textContent).toContain('beside it');
    expect(root.textContent).not.toContain('first');
  });

  it('is ONE walk: the page takes its own key, and each bridge is the next tree of that walk', () => {
    // What the server ships for this page: the root under its own key, then each component by where it
    // stands, in the tree its bridge draws, the bridges counted across the WHOLE page render.
    win.__INITIAL_STATE__ = {
      'App.Pages.Host#0': { title_: 'loaded' },
      'r0/App.Components.Probe@': { label: 'first' },
      'r1/App.Components.Probe@': { label: 'second' },
    };
    const root = host();
    new EscapeHatchPage(new Host()).mount(root);

    expect(root.querySelector('#go')?.textContent).toBe('loaded');
    // If each bridge restarted the count, both would be the first tree and read 'first'.
    expect(root.textContent).toContain('first');
    expect(root.textContent).toContain('second');
  });

  const rowIn = (namespace: string) =>
    class Row extends StatefulComponent {
      static $typeId = `${namespace}.Row`;
      static $hydration = { label: 'declared' } as const;
      label = 'default';
      build(): VisualNodeValue {
        return new Text(this.label);
      }
    };

  it('keys a component by its full identity, so two same-named types never share state', () => {
    const ARow = rowIn('A');
    const BRow = rowIn('B');
    expect(ARow.name).toBe(BRow.name); // the simple name cannot tell them apart
    expect(componentIdentity(new ARow())).not.toBe(componentIdentity(new BRow()));

    // The server expanded A.Row; a drifted client builds B.Row in that position. B.Row must keep its
    // defaults rather than take the other type's state, and A.Row still gets its own.
    win.__INITIAL_STATE__ = { 'r0/A.Row@0': { label: 'A-state' } };
    const page = (Row: new () => StatefulComponent) =>
      new (class extends StatelessComponent {
        build(): VisualNodeValue {
          const column = new Column(0);
          column.add(new Row());
          return column;
        }
      })();

    const drifted = host();
    page(BRow).mount(drifted);
    expect(drifted.textContent).toContain('default');

    resetComponentKeys();
    const agreeing = host();
    page(ARow).mount(agreeing);
    expect(agreeing.textContent).toContain('A-state');
  });

  it('keeps an instance by the same identity, so a B.Row at a position never becomes the A.Row there', () => {
    // The OTHER place the runtime asks "is this the same component": the element tree that keeps a
    // nested stateful instance, and its state, across a page's re-renders. The store it replaced
    // keyed by the class name once, so after the walk had learned the full identity it could still
    // hand an A.Row's instance to a B.Row built at the same path. A position keeps its instance only
    // for a node of the same runtime type.
    const ARow = rowIn('A');
    const BRow = rowIn('B');
    const roots = new ElementRoots(null);
    const render = (row: StatefulComponent) => {
      const column = new Column(0);
      column.add(row);
      enterPass(roots, null, true);
      try {
        lowerVisualNode(column as never, ambientLoweringContext());
      } finally {
        exitPass();
      }
    };

    const first = new ARow();
    render(first);
    const fresh = new BRow();
    render(fresh);

    expect(first.mountedAt).toBeNull();
    expect(fresh.mountedAt).not.toBeNull();
    roots.unmountAll();
  });
});
