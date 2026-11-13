/**
 * The app's layout stays mounted across client navigations (#1129, openspec change
 * `an-apps-layout-stays-mounted`): one owner for the document, above the page, built from the layout
 * around the slot the page stands in. Each case drives the document the way the boot does: `mount` or
 * `hydrate` for the first page, `show` for each navigation, after the router moved the address.
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { StatefulComponent, StatelessComponent, resetComponentKeys } from './component';
import { LayoutHost, type LayoutClass } from './layout-host';
import { Navigator } from '../router/navigator';
import { Column, Pressable, Text } from '../shared/vocabulary';
import type { VisualNodeValue } from '../shared/nodes';
import type { UiComponent } from './component';

const nextFrame = () =>
  new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));

function column(...children: unknown[]): VisualNodeValue {
  const node = new Column(0);
  for (const child of children) node.add(child as never);
  return node as unknown as VisualNodeValue;
}

async function press(container: HTMLElement, label: string): Promise<void> {
  const trigger = Array.from(container.querySelectorAll('button')).find(
    (button) => button.getAttribute('aria-label') === label,
  ) as HTMLElement;
  trigger.click();
  await nextFrame();
}

/** What the lifecycle said, in order. */
let events: string[] = [];
/** Every layout instance that built, in order. */
let layoutBuilds: object[] = [];

/** A layout with a sidebar whose open state is the layout's own, and the location it shows. */
class SidebarLayout extends StatefulComponent {
  open = false;

  constructor(private readonly body: VisualNodeValue) {
    super();
  }

  onMount(): void {
    events.push('mount layout');
  }

  onUnmount(): void {
    events.push('unmount layout');
  }

  build(context: unknown): VisualNodeValue {
    layoutBuilds.push(this);
    return column(
      new Text(`layout at ${Navigator.of(context).location}`, 'label'),
      new Pressable(new Text(this.open ? 'sidebar open' : 'sidebar closed', 'label'), () =>
        this.setState(() => (this.open = !this.open)), { label: 'toggle the sidebar' }),
      this.body,
    );
  }
}

/** A page that counts its presses, and says when it arrives and leaves. */
function pageNamed(name: string): new () => StatefulComponent {
  return class extends StatefulComponent {
    presses = 0;

    onMount(): void {
      events.push(`mount ${name}`);
    }

    onUnmount(): void {
      events.push(`unmount ${name}`);
    }

    build(): VisualNodeValue {
      return new Pressable(new Text(`${name} page, pressed ${this.presses}`, 'label'), () =>
        this.setState(() => this.presses++), { label: `press ${name}` }) as unknown as VisualNodeValue;
    }
  };
}

const Charts = pageNamed('charts');
const Table = pageNamed('table');

function hostAt(path: string, page: UiComponent): { host: LayoutHost; container: HTMLElement } {
  history.replaceState(null, '', path);
  const container = document.createElement('div');
  document.body.appendChild(container);
  const host = new LayoutHost(SidebarLayout as unknown as LayoutClass, page);
  host.mount(container);
  return { host, container };
}

beforeEach(() => {
  events = [];
  layoutBuilds = [];
  document.body.innerHTML = '';
});

describe('the layout stays mounted across client navigations', () => {
  it('keeps the layout and its state, replaces the page, and reads the new location', async () => {
    const { host, container } = hostAt('/charts', new Charts());
    await press(container, 'toggle the sidebar');
    expect(container.textContent).toContain('sidebar open');
    const layout = layoutBuilds[0];

    // The router moves the address before the document builds for the navigation.
    history.pushState(null, '', '/table');
    host.show(new Table());

    expect(layoutBuilds.every((built) => built === layout)).toBe(true);
    expect(container.textContent).toContain('sidebar open');
    expect(container.textContent).toContain('layout at /table');
    expect(container.textContent).toContain('table page');
    expect(container.textContent).not.toContain('charts page');
  });

  it("unmounts the old page before the new page mounts, and never the layout", () => {
    const { host } = hostAt('/charts', new Charts());
    expect(events).toEqual(['mount layout', 'mount charts']);
    events = [];

    history.pushState(null, '', '/table');
    host.show(new Table());

    expect(events).toEqual(['unmount charts', 'mount table']);
  });

  it('replaces a page of the same type, as a navigation without a layout makes a fresh one', async () => {
    const first = new Charts();
    const { host, container } = hostAt('/charts', first);
    await press(container, 'press charts');
    expect(container.textContent).toContain('charts page, pressed 1');
    events = [];

    host.show(new Charts());

    expect(events).toEqual(['unmount charts', 'mount charts']);
    expect(container.textContent).toContain('charts page, pressed 0');
  });

  it("builds the page alone on the page's own setState, as any nested component", async () => {
    const { container } = hostAt('/charts', new Charts());
    const layoutBuildsBefore = layoutBuilds.length;

    await press(container, 'press charts');

    expect(container.textContent).toContain('charts page, pressed 1');
    expect(layoutBuilds.length).toBe(layoutBuildsBefore);
  });

  it('takes the layout and the page down when the document leaves', () => {
    const { host } = hostAt('/charts', new Charts());
    events = [];

    host.disposeQuietly();

    expect(events).toEqual(['unmount charts', 'unmount layout']);
  });

  it('patches nothing for a page shown after the document left', () => {
    const { host, container } = hostAt('/charts', new Charts());
    host.disposeQuietly();
    const drawn = container.innerHTML;
    events = [];

    host.show(new Table());

    expect(events).toEqual([]);
    expect(container.innerHTML).toBe(drawn);
  });

  it('holds a page shown before it mounts, and draws that page when it mounts', () => {
    history.replaceState(null, '', '/table');
    const host = new LayoutHost(SidebarLayout as unknown as LayoutClass, new Charts());

    host.show(new Table());
    expect(events).toEqual([]);

    const container = document.createElement('div');
    document.body.appendChild(container);
    host.mount(container);

    expect(container.textContent).toContain('table page');
    expect(container.textContent).not.toContain('charts page');
    expect(events).toEqual(['mount layout', 'mount table']);
  });
});

describe("hydration adopts the layout's markup", () => {
  const win = window as unknown as { __INITIAL_STATE__?: Record<string, Record<string, unknown>> };

  // The tree `AppLayoutTests` renders on the server, with the identities the server names it by: a
  // layout of a heading and the slot, a page of a line and a card that loads its own number.
  class ConsoleLayout extends StatefulComponent {
    static $typeId = 'eQuantic.UI.Server.Tests.Rendering.AppLayoutTests+ConsoleLayout';

    constructor(private readonly body: VisualNodeValue) {
      super();
    }

    build(context: unknown): VisualNodeValue {
      return column(new Text(`layout at ${Navigator.of(context).location}`, 'heading'), this.body);
    }
  }

  /** What a card holds before any data lands: the server's number while the served markup is drawn. */
  let unloaded = '-1';

  class StatsCard extends StatelessComponent {
    static $typeId = 'eQuantic.UI.Server.Tests.Rendering.AppLayoutTests+StatsCard';
    static $hydration = { _downloads: 'declared' } as const;
    _downloads = unloaded;

    build(): VisualNodeValue {
      return new Text(`Downloads: ${this._downloads}`, 'bodyM') as unknown as VisualNodeValue;
    }
  }

  class StatsPage extends StatelessComponent {
    static $typeId = 'eQuantic.UI.Server.Tests.Rendering.AppLayoutTests+StatsPage';

    build(): VisualNodeValue {
      return column(new Text('stats page', 'bodyM'), new StatsCard());
    }
  }

  // THE KEY THE SERVER WRITES (`AppLayoutTests.AComposedComponentsData_IsKeyedWhereTheDocumentOwnerReachesIt`):
  // the layout at the root, its column, the slot it placed second, the page, the page's column, the card.
  const cardKey = 'r0/eQuantic.UI.Server.Tests.Rendering.AppLayoutTests+StatsCard@0.1.0.0.1';

  beforeEach(() => {
    resetComponentKeys();
    history.replaceState(null, '', '/layout/stats');
    win.__INITIAL_STATE__ = { [cardKey]: { _downloads: '42' } };
  });
  afterEach(() => {
    delete win.__INITIAL_STATE__;
    resetComponentKeys();
  });

  /**
   * The markup the server sent: a document of this tree drawn with the card holding the server's number
   * of itself, whatever key the browser reads, so only the key brings the number to the hydrating card.
   */
  function served(): HTMLElement {
    const drawn = document.createElement('div');
    const state = win.__INITIAL_STATE__;
    delete win.__INITIAL_STATE__;
    unloaded = '42';
    try {
      new LayoutHost(ConsoleLayout as unknown as LayoutClass, new StatsPage()).mount(drawn);
    } finally {
      unloaded = '-1';
      win.__INITIAL_STATE__ = state;
    }
    const container = document.createElement('div');
    container.dataset.ssr = 'true';
    container.innerHTML = drawn.innerHTML;
    document.body.appendChild(container);
    return container;
  }

  it("hands a composed component the server's data under the key the server wrote", () => {
    const container = served();
    const seen: string[] = [];
    class Probed extends StatsPage {
      build(): VisualNodeValue {
        const built = super.build();
        seen.push(container.textContent ?? '');
        return built;
      }
    }

    new LayoutHost(ConsoleLayout as unknown as LayoutClass, new Probed()).hydrate(container);

    expect(seen.length).toBe(1);
    expect(container.textContent).toContain('Downloads: 42');
    expect(container.textContent).toContain('layout at /layout/stats');
  });

  it('adopts the markup as it is, with no element written again', () => {
    const container = served();
    const elements = Array.from(container.querySelectorAll('*'));
    const observer = new MutationObserver(() => {});
    observer.observe(container, { childList: true, subtree: true, characterData: true, attributes: true });

    new LayoutHost(ConsoleLayout as unknown as LayoutClass, new StatsPage()).hydrate(container);

    const mutations = observer.takeRecords();
    observer.disconnect();
    expect(mutations).toEqual([]);
    expect(Array.from(container.querySelectorAll('*'))).toEqual(elements);
    expect(container.textContent).toContain('Downloads: 42');
  });
});
