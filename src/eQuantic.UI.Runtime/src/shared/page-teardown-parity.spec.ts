import { describe, expect, it } from 'vitest';
import * as componentModule from '../core/component';
import {
  ComponentState,
  SharedStatefulComponent,
  StatefulComponent,
  StatelessComponent,
} from '../core/component';
import type { VisualNodeValue } from './nodes';
import { Column, Text } from './vocabulary';

/**
 * Every shape a PAGE can be, held to the same promise: what it retained leaves when it does.
 *
 * A page owns an instance store — that is how nested stateful components keep their state across
 * its re-renders — so navigating away is those components leaving the tree, and `onUnmount` is owed
 * to each. Two of the three shapes were not paying it: the stateless page said it had "no lifecycle
 * to release" (true of itself, not of its store), and the Core stateful page released its own state
 * and left the store alone. What that costs is a subscription that outlives its component: the
 * abandoned component's setState keeps drawing it into whatever page the visitor navigated to, and
 * one timer per visit stays alive.
 *
 * The shapes are DISCOVERED from the module rather than listed here. A new page shape either says
 * how to build one below or fails this test, which is the only way a guard about "every shape"
 * stays true of a shape nobody thought about.
 */
describe('every page shape releases what it retained', () => {
  let unmounted = 0;

  class Leaf extends SharedStatefulComponent {
    onUnmount(): void {
      unmounted++;
    }

    build(): VisualNodeValue {
      return new Text('leaf');
    }
  }

  const subtree = (): VisualNodeValue => {
    const column = new Column(0);
    column.add(new Leaf());
    return column;
  };

  /** One page of each shape, built the way that shape demands. */
  const shapes: Record<string, () => { mount(host: HTMLElement): void; disposeQuietly(): void }> = {
    StatelessComponent: () =>
      new (class extends StatelessComponent {
        build(): VisualNodeValue {
          return subtree();
        }
      })(),

    SharedStatefulComponent: () =>
      new (class extends SharedStatefulComponent {
        build(): VisualNodeValue {
          return subtree();
        }
      })(),

    StatefulComponent: () =>
      new (class extends StatefulComponent {
        createState(): ComponentState {
          return new (class extends ComponentState {
            build(): VisualNodeValue {
              return subtree();
            }
          })();
        }
      })(),
  };

  /** What the boot can mount as a page: it reconciles one in and disposes the one going out. */
  const pageShapes = Object.entries(componentModule)
    .filter(([, exported]) => typeof exported === 'function')
    .filter(([, exported]) => {
      const proto = (exported as { prototype?: object }).prototype;
      return !!proto && 'mountReconcile' in proto && 'disposeQuietly' in proto;
    })
    .map(([name]) => name);

  it('covers every shape the module exports', () => {
    expect(pageShapes.length).toBeGreaterThan(0);
    expect(Object.keys(shapes).sort()).toEqual(pageShapes.sort());
  });

  for (const name of Object.keys(shapes)) {
    it(`${name} unmounts the components it held`, () => {
      unmounted = 0;
      const host = document.createElement('div');
      document.body.appendChild(host);

      const page = shapes[name]();
      page.mount(host);
      expect(host.textContent).toContain('leaf');

      page.disposeQuietly();

      expect(unmounted).toBe(1);
    });
  }
});
