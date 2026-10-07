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
  const scrollPageTo = (y: number): void => {
    Object.defineProperty(window, 'scrollY', { value: y, configurable: true });
    document.dispatchEvent(new Event('scroll'));
  };
  const scrollTo = (element: HTMLElement, y: number): void => {
    element.scrollTop = y;
    element.dispatchEvent(new Event('scroll'));
  };
  const scrolled = (element: HTMLElement): boolean => element.hasAttribute(SCROLLED_MARKER);

  beforeEach(() => {
    document.body.innerHTML = '';
    Object.defineProperty(window, 'scrollY', { value: 0, configurable: true });
    installScrolledController();
  });

  afterEach(() => resetScrolledController());

  it('follows the page for a header in no scroller', () => {
    const bar = header();
    document.body.append(bar);

    scrollPageTo(20);
    expect(scrolled(bar)).toBe(true);
    scrollPageTo(0);
    expect(scrolled(bar)).toBe(false);
  });

  it('is scrolled PAST the threshold, as Photon draws it past 8dp', () => {
    const bar = header();
    document.body.append(bar);

    scrollPageTo(SCROLLED_THRESHOLD);
    expect(scrolled(bar)).toBe(false);
    scrollPageTo(SCROLLED_THRESHOLD + 1);
    expect(scrolled(bar)).toBe(true);
  });

  it('follows its own scroller, which the page does not move', () => {
    const bar = header();
    const panel = scroller(bar);
    document.body.append(panel);

    scrollPageTo(300);
    expect(scrolled(bar), 'the panel has not scrolled').toBe(false);
    scrollTo(panel, 40);
    expect(scrolled(bar), 'its own surface has').toBe(true);
  });

  it('is decided by the NEAREST scroller', () => {
    const bar = header();
    const inner = scroller(bar);
    const outer = scroller(inner);
    document.body.append(outer);

    scrollTo(outer, 40);
    expect(scrolled(bar), 'the panel it pins to is at its top').toBe(false);
    scrollTo(inner, 40);
    expect(scrolled(bar)).toBe(true);
  });

  it('sets a header mounted on a page that is already scrolled, without a scroll event', () => {
    Object.defineProperty(window, 'scrollY', { value: 120, configurable: true });
    const bar = header();
    document.body.append(bar);

    syncScrolledPinned();
    expect(scrolled(bar)).toBe(true);
  });
});
