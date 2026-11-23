/**
 * A page holds one arm of each adaptive node once it has hydrated (#1783): the arm the window's width
 * resolves, as Photon holds it, so the arms the window hides do not run and a control is in the page
 * once. A window crossing a threshold renders the page again with the other arm, and a component that
 * carries a global key crosses with its state (#797).
 *
 * The document starts holding every arm, as a served page hydrates; each case flips it the way the boot
 * does, and puts it back after.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { StatefulComponent } from '../core/component';
import { AdaptiveNode, Column, GlobalKey, Pressable, Row, Text } from './vocabulary';
import type { VisualNodeValue } from './nodes';
import {
  adaptiveWindowWidth,
  documentHoldsEveryArm,
  holdOneArm,
  noteThreshold,
  resetAdaptiveWindow,
  setAdaptiveInvalidator,
} from './adaptive-window';

const nextFrame = () =>
  new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));

let builds: Record<string, number> = {};

/** A counter with a press, which counts its builds by its label. */
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
    builds[this.label] = (builds[this.label] ?? 0) + 1;
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

function row(...children: unknown[]): VisualNodeValue {
  const node = new Row(0);
  for (const child of children) node.add(child as never);
  return node as unknown as VisualNodeValue;
}

function counter(label: string, key: GlobalKey | null = null): Counter {
  const made = new Counter(label);
  made.globalKey = key;
  return made;
}

/** Sets the window's width as a resize would: its `innerWidth` and what its media queries answer. */
function widthIs(width: number): void {
  (window as unknown as { happyDOM: { setViewport(viewport: { width: number }): void } }).happyDOM.setViewport({
    width,
  });
}

function mount(page: StatefulComponent): HTMLElement {
  const container = document.createElement('div');
  document.body.appendChild(container);
  page.mount(container);
  return container;
}

function buttons(container: HTMLElement): HTMLElement[] {
  return Array.from(container.querySelectorAll('button')) as HTMLElement[];
}

async function press(container: HTMLElement, label: string): Promise<void> {
  buttons(container)
    .find((button) => button.getAttribute('aria-label') === label)!
    .click();
  await nextFrame();
}

/** Renders the page again, as the boot's invalidator does when the window crosses a threshold. */
async function renderAgain(page: StatefulComponent): Promise<void> {
  (page as unknown as { _scheduleRender(): void })._scheduleRender();
  await nextFrame();
}

const originalWidth = window.innerWidth;

beforeEach(() => {
  builds = {};
  document.body.innerHTML = '';
  resetAdaptiveWindow();
});

afterEach(() => {
  resetAdaptiveWindow();
  widthIs(originalWidth);
});

describe('a document that holds one arm', () => {
  it('builds and draws only the arm the window resolves, with no gate', () => {
    holdOneArm();
    widthIs(400);
    class Page extends StatefulComponent {
      build(): VisualNodeValue {
        return column(new AdaptiveNode(counter('narrow') as never, null, counter('wide') as never));
      }
    }

    const container = mount(new Page());

    expect(builds).toEqual({ narrow: 1 });
    expect(buttons(container).map((button) => button.getAttribute('aria-label'))).toEqual(['bump narrow']);
    expect(container.querySelector('[class*="eq-v"]')).toBeNull();
  });

  it('shows the other arm when the window crosses a threshold, and an arm of another type starts again', async () => {
    holdOneArm();
    widthIs(400);
    class Page extends StatefulComponent {
      build(): VisualNodeValue {
        return column(new AdaptiveNode(counter('narrow') as never, null, column(counter('wide')) as never));
      }
    }
    const page = new Page();
    const container = mount(page);
    await press(container, 'bump narrow');
    expect(container.textContent).toContain('narrow: 1');

    widthIs(1200);
    await renderAgain(page);
    expect(container.textContent).toContain('wide: 0');
    expect(container.textContent).not.toContain('narrow');

    widthIs(400);
    await renderAgain(page);
    expect(container.textContent).toContain('narrow: 0');
  });

  it('keeps the position across a threshold where both arms are of one type, as Photon kept it', async () => {
    holdOneArm();
    widthIs(400);
    class Page extends StatefulComponent {
      build(): VisualNodeValue {
        return column(new AdaptiveNode(counter('narrow') as never, null, counter('wide') as never));
      }
    }
    const page = new Page();
    const container = mount(page);
    await press(container, 'bump narrow');

    widthIs(1200);
    await renderAgain(page);

    expect(container.textContent).toContain('wide: 1');
  });

  it('carries a component with a global key across the arms, with its state, and back', async () => {
    holdOneArm();
    widthIs(400);
    const key = new GlobalKey('screen');
    class Page extends StatefulComponent {
      build(): VisualNodeValue {
        // The drawer template's shape: the screen alone on a phone, beside a rail on a wide window.
        const compact = column(counter('screen', key));
        const docked = row(new Text('rail', 'label'), counter('screen', key));
        return new AdaptiveNode(compact as never, null, docked as never) as unknown as VisualNodeValue;
      }
    }
    const page = new Page();
    const container = mount(page);
    await press(container, 'bump screen');
    await press(container, 'bump screen');

    widthIs(1200);
    await renderAgain(page);
    expect(container.textContent).toContain('rail');
    expect(container.textContent).toContain('screen: 2');

    widthIs(400);
    await renderAgain(page);
    expect(container.textContent).not.toContain('rail');
    expect(container.textContent).toContain('screen: 2');
  });

  it('keeps the shown arm and its DOM when a page served with every arm stops holding them', async () => {
    widthIs(1200);
    class Page extends StatefulComponent {
      build(): VisualNodeValue {
        return column(new AdaptiveNode(counter('narrow') as never, null, counter('wide') as never));
      }
    }
    const page = new Page();
    const container = mount(page);
    expect(documentHoldsEveryArm()).toBe(true);
    expect(buttons(container)).toHaveLength(2);
    await press(container, 'bump wide');
    const wide = buttons(container).find((button) => button.getAttribute('aria-label') === 'bump wide')!;

    holdOneArm();
    await renderAgain(page);

    // By identity: `toEqual` compares DOM nodes by their structure, and a box rebuilt alike would pass.
    expect(buttons(container)).toHaveLength(1);
    expect(buttons(container)[0]).toBe(wide);
    expect(container.textContent).toContain('wide: 1');
  });
});

describe('the window an arm is resolved against', () => {
  /** A `matchMedia` that answers each `(min-width: Npx)` against `width`, and lets the test change it. */
  function mediaAt(initial: number) {
    let width = initial;
    const queries: { min: number; listeners: (() => void)[]; matches: boolean }[] = [];
    vi.stubGlobal(
      'matchMedia',
      vi.fn((media: string) => {
        const min = Number(/min-width:\s*([\d.]+)px/.exec(media)![1]);
        const query = {
          min,
          listeners: [] as (() => void)[],
          get matches() {
            return width >= min;
          },
          addEventListener: (_: string, listener: () => void) => query.listeners.push(listener),
        };
        queries.push(query);
        return query;
      }),
    );
    return {
      set(next: number) {
        const before = queries.map((query) => query.matches);
        width = next;
        queries.forEach((query, i) => {
          if (query.matches !== before[i]) query.listeners.forEach((listener) => listener());
        });
      },
    };
  }

  afterEach(() => vi.unstubAllGlobals());

  it("stays on each threshold's side its media query says, where a whole innerWidth would cross it", () => {
    // A zoomed window 839.6 CSS pixels wide: its innerWidth rounds to 840, and the gates' media query
    // for 840 does not match.
    mediaAt(839.6);
    widthIs(840);
    noteThreshold(600);
    noteThreshold(840);

    const width = adaptiveWindowWidth();

    expect(width).toBeLessThan(840);
    expect(width).toBeGreaterThanOrEqual(600);
    expect(new AdaptiveNode(new Text('a') as never, new Text('b') as never, new Text('c') as never).resolveIndex(width)).toBe(1);
  });

  it('renders the page again when the window crosses a threshold, only once one arm is held', () => {
    const media = mediaAt(400);
    widthIs(400);
    const render = vi.fn();
    setAdaptiveInvalidator(render);
    noteThreshold(840);

    media.set(1000);
    expect(render).not.toHaveBeenCalled();

    holdOneArm();
    media.set(400);
    expect(render).toHaveBeenCalledTimes(1);
  });
});
