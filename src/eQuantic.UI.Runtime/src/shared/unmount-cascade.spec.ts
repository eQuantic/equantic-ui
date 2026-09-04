import { describe, expect, it } from 'vitest';
import { StatefulComponent, StatelessComponent } from '../core/component';
import { Column, Text } from './vocabulary';
import type { VisualNodeValue } from './nodes';

/**
 * What leaving the tree means for everything BELOW the component that left.
 *
 * On Photon one store belongs to the host, so a pass that drops a subtree unmounts every instance
 * in it whatever its depth. On the web each component keeps its own store, and tearing one down
 * only reached its DIRECT children: a component two levels down never heard that it left, so
 * whatever it subscribed to in `onMount` went on running, and its `setState` redrew it into the
 * page the visitor had navigated to.
 *
 * The framework's own contract says why this matters: "Without it every mount is a leak, so the
 * pair ships together."
 */
describe('leaving the tree reaches every depth', () => {
  class Leaf extends StatefulComponent {
    static mounted = 0;
    static unmounted = 0;

    onMount(): void {
      Leaf.mounted++;
    }

    onUnmount(): void {
      Leaf.unmounted++;
    }

    build(): VisualNodeValue {
      return new Text('leaf');
    }
  }

  class Section extends StatefulComponent {
    build(): VisualNodeValue {
      // The leaf is nested inside THIS component, which is itself nested in the page: two component
      // boundaries between the page and the subscription.
      const column = new Column(0);
      column.add(new Leaf());
      return column;
    }
  }

  class Page extends StatefulComponent {
    build(): VisualNodeValue {
      const column = new Column(0);
      column.add(new Section());
      return column;
    }
  }

  /**
   * The page shape that actually ships most sections: a page with no state of its own, composing
   * components that have plenty. It reached navigation saying it had nothing to release, which was
   * true when it was written and stopped being true when a stateless page gained an instance store
   * to retain nested stateful components across passes. The page holds no lifecycle; it holds a
   * STORE FULL of components that do.
   *
   * The visible half is worse than the leak: the abandoned component's `setState` keeps drawing it
   * into the page the visitor navigated to, so a section from the previous page reappears on top of
   * the new one and its title runs into the new title.
   */
  it('a stateless page releases the components it retained', () => {
    Leaf.mounted = 0;
    Leaf.unmounted = 0;

    class StatelessPage extends StatelessComponent {
      build(): VisualNodeValue {
        const column = new Column(0);
        column.add(new Section());
        return column;
      }
    }

    const host = document.createElement('div');
    document.body.appendChild(host);

    const page = new StatelessPage();
    page.mount(host);
    expect(Leaf.mounted).toBe(1);
    expect(host.textContent).toContain('leaf');

    page.disposeQuietly();

    expect(Leaf.unmounted).toBe(1);
  });

  it('a component two levels down is unmounted when the page goes', () => {
    Leaf.mounted = 0;
    Leaf.unmounted = 0;

    const host = document.createElement('div');
    document.body.appendChild(host);

    const page = new Page();
    page.mount(host);
    // The tree has to be there at all: a first version of this test built an empty column, so
    // nothing mounted and the assertion below was measuring its own mistake.
    expect(Leaf.mounted).toBe(1);
    expect(host.textContent).toContain('leaf');

    // What a client-side navigation does to the page it leaves.
    page.disposeQuietly();

    expect(Leaf.unmounted).toBe(1);
  });
});
