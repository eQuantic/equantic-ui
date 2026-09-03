import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { StatefulComponent } from './component';
import { Component, HtmlNode } from './types';

/**
 * Hidden-tab re-rendering (see render-scheduler.ts).
 *
 * A background tab gets NO animation frames, so a rAF-only `_scheduleRender` left a setState driven
 * by a timer/socket/background event mutating state with the DOM frozen — and latched
 * `_renderScheduled`, swallowing every later setState into the frame that never arrived. These tests
 * pin the fallback: rAF is stubbed so it can never fire, exactly like a real backgrounded tab.
 */

/** Minimal concrete leaf — the state's rendered output, kept independent of the component library. */
class Label extends Component {
  constructor(private readonly text: string) {
    super();
  }

  render(): HtmlNode {
    return {
      tag: 'p',
      attributes: {},
      events: {},
      // Text is a child node in this vocabulary; an element's own textContent is ignored.
      children: [
        { tag: '#text', attributes: {}, events: {}, children: [], textContent: this.text },
      ],
    };
  }
}

/** The stateful shape: state lives on the component, setState rebuilds directly. */
class SharedCounter extends StatefulComponent {
  count = 0;

  bump(): void {
    this.setState(() => {
      this.count++;
    });
  }

  build(): Component {
    return new Label(`shared: ${this.count}`);
  }
}

/** Shadow the prototype getter — happy-dom always reports 'visible' while a defaultView exists. */
function setHidden(hidden: boolean): void {
  Object.defineProperty(document, 'visibilityState', {
    configurable: true,
    get: () => (hidden ? 'hidden' : 'visible'),
  });
}

function mountIn<T extends { mount(container: HTMLElement): void }>(component: T): HTMLElement {
  const container = document.createElement('div');
  document.body.appendChild(container);
  component.mount(container);
  return container;
}

const flushTimers = () => new Promise((resolve) => setTimeout(resolve, 5));
/** Long enough to outlast the visible-path backstop (100ms). */
const flushBackstop = () => new Promise((resolve) => setTimeout(resolve, 150));

describe('re-render scheduling in a hidden tab', () => {
  beforeEach(() => {
    document.body.innerHTML = '';
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
    delete (document as unknown as Record<string, unknown>)['visibilityState'];
  });

  it('StatefulComponent: setState repaints through the timer fallback', async () => {
    const raf = vi.fn();
    vi.stubGlobal('requestAnimationFrame', raf);
    setHidden(true);

    const counter = new SharedCounter();
    const container = mountIn(counter);
    expect(container.textContent).toContain('shared: 0');

    counter.bump();
    await flushTimers();

    expect(container.textContent).toContain('shared: 1');
    expect(raf).not.toHaveBeenCalled();

    counter.bump();
    await flushTimers();
    expect(container.textContent).toContain('shared: 2');
  });

  it('backstops a frame requested while visible that the tab then swallows by going hidden', async () => {
    const raf = vi.fn(); // requested while visible, never delivered — hidden before the frame ran
    vi.stubGlobal('requestAnimationFrame', raf);
    setHidden(false);

    const page = new SharedCounter();
    const container = mountIn(page);

    page.bump();
    expect(raf).toHaveBeenCalledTimes(1); // visible: the frame IS the fast path
    expect(container.textContent).toContain('shared: 0'); // ...but it never fires

    await flushBackstop();
    expect(container.textContent).toContain('shared: 1');
  });

  it('keeps rAF as the fast path while visible', () => {
    const raf = vi.fn((cb: FrameRequestCallback) => {
      cb(0);
      return 1;
    });
    vi.stubGlobal('requestAnimationFrame', raf);
    setHidden(false);

    const page = new SharedCounter();
    const container = mountIn(page);

    page.bump();

    // The frame drove the flush — no timer wait needed.
    expect(raf).toHaveBeenCalledTimes(1);
    expect(container.textContent).toContain('shared: 1');
  });

  it('flushes pending work on visibilitychange without waiting for the throttled timer', () => {
    vi.stubGlobal('requestAnimationFrame', vi.fn());
    vi.useFakeTimers(); // background timers are throttled to >=1s; never advance them here
    setHidden(true);

    const page = new SharedCounter();
    const container = mountIn(page);

    page.bump();
    expect(container.textContent).toContain('shared: 0'); // no frame, no timer

    setHidden(false);
    document.dispatchEvent(new Event('visibilitychange'));

    expect(container.textContent).toContain('shared: 1');
  });
});
