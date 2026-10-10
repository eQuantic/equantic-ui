import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { PINNED_MARKER, SCROLLED_MARKER } from './markers';
import {
  installScrolledController,
  resetScrolledController,
  syncScrolledPinned,
  SCROLLED_THRESHOLD,
} from './scrolled-pinned';

/**
 * Which pinned headers are scrolled (#506). The state is the HEADER's, set from the surface it pins
 * to: the nearest ancestor that scrolls, or the page. It was one class on <html>, which a header in a
 * scrolling panel could not use, since the window does not move when the panel does.
 */
describe('scrolled pinned headers', () => {
  const header = (): HTMLElement => {
    const element = document.createElement('div');
    element.setAttribute(PINNED_MARKER, '1');
    return element;
  };
  const scroller = (...children: HTMLElement[]): HTMLElement => {
    const element = document.createElement('div');
    element.style.overflowY = 'auto';
    element.append(...children);
    return element;
  };
  // The controller syncs once per frame, so a scroll is read after the frame that follows it.
  const frame = (): Promise<void> => new Promise((resolve) => requestAnimationFrame(() => resolve()));
  const scrollPageTo = async (y: number): Promise<void> => {
    Object.defineProperty(window, 'scrollY', { value: y, configurable: true });
    document.dispatchEvent(new Event('scroll'));
    await frame();
  };
  const scrollTo = async (element: HTMLElement, y: number): Promise<void> => {
    element.scrollTop = y;
    element.dispatchEvent(new Event('scroll'));
    await frame();
  };
  const scrolled = (element: HTMLElement): boolean => element.hasAttribute(SCROLLED_MARKER);

  beforeEach(() => {
    document.body.innerHTML = '';
    Object.defineProperty(window, 'scrollY', { value: 0, configurable: true });
    installScrolledController();
  });

  afterEach(() => resetScrolledController());

  it('follows the page for a header in no scroller', async () => {
    const bar = header();
    document.body.append(bar);

    await scrollPageTo(20);
    expect(scrolled(bar)).toBe(true);
    await scrollPageTo(0);
    expect(scrolled(bar)).toBe(false);
  });

  it('is scrolled PAST the threshold, as Photon draws it past 8dp', async () => {
    const bar = header();
    document.body.append(bar);

    await scrollPageTo(SCROLLED_THRESHOLD);
    expect(scrolled(bar)).toBe(false);
    await scrollPageTo(SCROLLED_THRESHOLD + 1);
    expect(scrolled(bar)).toBe(true);
  });

  it('follows its own scroller, which the page does not move', async () => {
    const bar = header();
    const panel = scroller(bar);
    document.body.append(panel);

    await scrollPageTo(300);
    expect(scrolled(bar), 'the panel has not scrolled').toBe(false);
    await scrollTo(panel, 40);
    expect(scrolled(bar), 'its own surface has').toBe(true);
  });

  it('is decided by the NEAREST scroller', async () => {
    const bar = header();
    const inner = scroller(bar);
    const outer = scroller(inner);
    document.body.append(outer);

    await scrollTo(outer, 40);
    expect(scrolled(bar), 'the panel it pins to is at its top').toBe(false);
    await scrollTo(inner, 40);
    expect(scrolled(bar)).toBe(true);
  });

  // A horizontal ScrollView lowers to `overflow-x: auto` and `overflow-y: hidden`. It is the surface
  // `position: sticky` pins to, and Photon's nearest scroll view, and it never scrolls along the axis a
  // header pins on (found by Copilot on #688).
  it('is never scrolled inside a horizontal scroller, whatever the page under it does', async () => {
    const bar = header();
    const row = document.createElement('div');
    row.style.overflowX = 'auto';
    row.style.overflowY = 'hidden';
    row.append(bar);
    document.body.append(row);

    await scrollPageTo(300);
    expect(scrolled(bar)).toBe(false);
  });

  it('is decided by a box that only clips, never: it is not a surface', async () => {
    const bar = header();
    const card = document.createElement('div');
    card.style.overflow = 'hidden';
    card.append(bar);
    document.body.append(card);

    await scrollPageTo(300);
    expect(scrolled(bar), 'the page is its surface').toBe(true);
  });

  it('sets a header mounted on a page that is already scrolled, without a scroll event', async () => {
    Object.defineProperty(window, 'scrollY', { value: 120, configurable: true });
    const bar = header();
    document.body.append(bar);

    syncScrolledPinned();
    expect(scrolled(bar)).toBe(true);
  });
});
