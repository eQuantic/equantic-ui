/**
 * The browser builds through the element engine (openspec change
 * `the-browser-builds-through-the-element-engine`, specs/element-tree): one case per scenario of the
 * requirement, each through a page mounted the way the boot mounts one.
 *
 * Builds are COUNTED, because what the change moves is how much builds: a nested component's
 * `setState` used to render its page again, and the page built every component it composes.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { StatefulComponent, resetComponentKeys } from '../core/component';
import { AdaptiveNode, Column, Pressable, Text } from './vocabulary';
import type { VisualNodeValue } from './nodes';

const nextFrame = () =>
  new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));

/** How many times each component built, by its label. */
let builds: Record<string, number> = {};
const built = (label: string) => {
  builds[label] = (builds[label] ?? 0) + 1;
};

/** A counter with a press, which counts its builds: the smallest thing whose state shows. */
class Counter extends StatefulComponent {
  label: string;
  count = 0;

  constructor(label: string) {
    super();
    this.label = label;
  }

  adoptConfig(next: Counter): void {
    this.label = next.label;
  }

  build(): VisualNodeValue {
    built(this.label);
    return new Pressable(new Text(`${this.label}: ${this.count}`, 'label'), () => this.setState(() => this.count++), {
      label: `bump ${this.label}`,
    }) as unknown as VisualNodeValue;
  }
}

function column(...children: unknown[]): VisualNodeValue {
  const node = new Column(0);
  for (const child of children) node.add(child as never);
  return node as unknown as VisualNodeValue;
}

function mount(page: StatefulComponent): HTMLElement {
  const container = document.createElement('div');
  document.body.appendChild(container);
  page.mount(container);
  return container;
}

async function press(container: HTMLElement, label: string): Promise<void> {
  const trigger = Array.from(container.querySelectorAll('button')).find(
    (button) => button.getAttribute('aria-label') === label,
  ) as HTMLElement;
  trigger.click();
  await nextFrame();
}

beforeEach(() => {
  builds = {};
  document.body.innerHTML = '';
});

describe('the browser builds through the element engine', () => {
  it('a nested state change builds only its own element', async () => {
    class Page extends StatefulComponent {
      build(): VisualNodeValue {
        built('page');
        return column(new Counter('a'), new Counter('b'));
      }
    }
    const container = mount(new Page());
    expect(builds).toEqual({ page: 1, a: 1, b: 1 });

    await press(container, 'bump a');

    // The counter that changed built again, and neither its sibling nor the page did.
    expect(builds).toEqual({ page: 1, a: 2, b: 1 });
    expect(container.textContent).toContain('a: 1');
    expect(container.textContent).toContain('b: 0');
  });

  it("a page's own state change builds what it composes, and the nested instance keeps its state", async () => {
    class Page extends StatefulComponent {
      generation = 0;

      build(): VisualNodeValue {
        built('page');
        return column(
          new Pressable(new Text('next', 'label'), () => this.setState(() => this.generation++), {
            label: 'next',
          }),
          new Counter(`g${this.generation}`),
        );
      }
    }
    const container = mount(new Page());
    await press(container, 'bump g0');
    expect(container.textContent).toContain('g0: 1');

    await press(container, 'next');

    // The page built again and handed the counter a new label: the instance its position keeps took
    // the new configuration, kept its count, and built with both.
    expect(builds.page).toBe(2);
    expect(builds.g1).toBe(1);
    expect(container.textContent).toContain('g1: 1');
  });

  describe('hydration', () => {
    const win = window as unknown as { __INITIAL_STATE__?: Record<string, Record<string, unknown>> };

    beforeEach(() => resetComponentKeys());
    afterEach(() => {
      delete win.__INITIAL_STATE__;
      resetComponentKeys();
    });

    it("adopts the server's state before the first build, under the Type#ordinal the server wrote", () => {
      const seen: string[] = [];
      class Probe extends StatefulComponent {
        static $typeId = 'App.Probe';
        static $hydration = { label: 'declared' } as const;
        label = 'default';

        build(): VisualNodeValue {
          seen.push(this.label);
          return new Text(this.label) as unknown as VisualNodeValue;
        }
      }
      class Page extends StatefulComponent {
        static $typeId = 'App.Page';

        build(): VisualNodeValue {
          return column(new Probe(), new Probe());
        }
      }
      win.__INITIAL_STATE__ = {
        'App.Probe#0': { label: 'first from the server' },
        'App.Probe#1': { label: 'second from the server' },
      };
      // The DOM the server wrote for the page, which hydration adopts.
      const server = document.createElement('div');
      server.innerHTML = '<div><span>first from the server</span><span>second from the server</span></div>';
      document.body.appendChild(server);

      new Page().hydrate(server);

      // Each component built ONCE before hydration walked the DOM, and that build read the server's
      // value: never its default first.
      expect(seen).toEqual(['first from the server', 'second from the server']);
      expect(server.textContent).toContain('second from the server');
    });
  });

  describe('a window that crosses a threshold', () => {
    const happyDOM = (
      window as unknown as { happyDOM: { setViewport(viewport: { width: number; height: number }): void } }
    ).happyDOM;
    const at = (width: number) => happyDOM.setViewport({ width, height: 900 });

    afterEach(() => at(1024));

    // The web draws every arm behind a media gate, and a resize opens another one without a render.
    // The arm a position holds is the one the page's last build resolved, and only that one keeps its
    // instances: a crossing nothing built for would show an arm whose component answers no press.
    // It crosses UPWARD: happy-dom's media list starts from "not matching" whatever the width, so it
    // reports a list that starts to match and stays quiet about one that stops.
    it('builds the page again, so a component in the arm it shows keeps its state', async () => {
      at(500);
      class Page extends StatefulComponent {
        build(): VisualNodeValue {
          built('page');
          return new AdaptiveNode(
            new Counter('compact') as never,
            null,
            new Counter('expanded') as never,
          ) as unknown as VisualNodeValue;
        }
      }
      const page = new Page();
      const container = mount(page);

      at(1024);
      await nextFrame();
      expect(builds.page).toBe(2);

      await press(container, 'bump expanded');
      await press(container, 'bump expanded');

      expect(container.textContent).toContain('expanded: 2');
      // A page that leaves stops listening: no crossing builds it again.
      page.disposeQuietly();
      at(500);
      at(1024);
      await nextFrame();
      expect(builds.page).toBe(2);
    });
  });

  it('a component that throws costs its own subtree, and the rest of the page draws as before', async () => {
    const errors = vi.spyOn(console, 'error').mockImplementation(() => {});
    class BrokenCard extends StatefulComponent {
      build(): never {
        throw new TypeError("Cannot read properties of undefined (reading 'title')");
      }
    }
    class Page extends StatefulComponent {
      build(): VisualNodeValue {
        built('page');
        return column(new Text('before'), new BrokenCard(), new Counter('after'));
      }
    }
    try {
      const container = mount(new Page());

      expect(container.textContent).toContain('before');
      expect(container.textContent).toContain('could not be displayed');
      expect(container.textContent).toContain('after: 0');

      // The sibling still answers, and builds alone.
      await press(container, 'bump after');
      expect(container.textContent).toContain('after: 1');
      expect(builds).toEqual({ page: 1, after: 2 });
      expect(errors.mock.calls.map(([message]) => String(message)).join(' ')).toContain('BrokenCard');
    } finally {
      errors.mockRestore();
    }
  });
});
