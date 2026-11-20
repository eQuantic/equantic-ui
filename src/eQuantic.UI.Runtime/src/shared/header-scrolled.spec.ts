import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import type { HtmlNode } from '../core/types';
import { PINNED_MARKER, SCROLLED_MARKER } from './markers';
import { resetScrolledController } from './scrolled-pinned';
import { effectiveStyle, HEADER_SCROLLED, stateSelector, stateStyle } from './style-atomizer';
import { Transform2D } from './value-types';
import { Box, BoxStyle, Pinned, StyleDiff } from './vocabulary';

/**
 * A box's `scrolled` diff (C# `BoxStyle.Scrolled`, #1769): the boxes INSIDE a pinned header follow
 * its scrolled state, so a header swaps what it shows as the page scrolls without a rebuild. The diff
 * lands in the header-scrolled family, whose rule selects on the nearest header's SCROLLED_MARKER, the
 * attribute scrolled-pinned.ts already sets for the header's own `scrolledStyle`.
 */
describe('a box follows the scrolled state of the header it is in (#1769)', () => {
  const badge = (): Box =>
    new Box(
      new BoxStyle({
        width: 96,
        height: 96,
        scrolled: new StyleDiff({ opacity: 0, transform: Transform2D.scale(0.6) }),
      }),
    );

  it('lowers every member of the diff into the family, and none of it at rest', () => {
    const header = new Pinned(badge()).render();
    const box = header.children[0] as HtmlNode;

    expect(stateStyle(box, HEADER_SCROLLED)).toBe('opacity: 0; transform: scale(0.6)');
    expect(effectiveStyle(box)).not.toContain('opacity');
  });

  it("writes the C# twin's selector, byte for byte", () => {
    expect(stateSelector(HEADER_SCROLLED, 'eq-x')).toBe(
      'html :where([data-eq-scrolled]) .eq-x' +
        ':not(:where([data-eq-scrolled] [data-eq-pinned]:not([data-eq-scrolled]) .eq-x))',
    );
  });

  describe('the header decides', () => {
    /** A header, scrolled or not, around `children`. */
    const header = (scrolled: boolean, ...children: HTMLElement[]): HTMLElement => {
      const element = document.createElement('div');
      element.setAttribute(PINNED_MARKER, '1');
      if (scrolled) element.setAttribute(SCROLLED_MARKER, '');
      element.append(...children);
      return element;
    };
    const box = (): HTMLElement => {
      const element = document.createElement('div');
      element.className = 'eq-x';
      return element;
    };
    const follows = (element: HTMLElement): boolean =>
      element.matches(stateSelector(HEADER_SCROLLED, 'eq-x'));

    beforeEach(() => {
      document.body.innerHTML = '';
    });

    it('in a scrolled header the box shows its diff, and at rest it does not', () => {
      const shown = box();
      const resting = box();
      document.body.append(header(true, shown), header(false, resting));

      expect(follows(shown)).toBe(true);
      expect(follows(resting)).toBe(false);
    });

    it('in no header it never does, however far the page has scrolled', () => {
      const alone = box();
      document.body.append(alone);

      expect(follows(alone)).toBe(false);
    });

    // A header nested in another is the browser's to answer: happy-dom matches no complex selector
    // inside :where() (measured: `:where([data-eq-scrolled] [data-eq-pinned] .eq-x)` is false where
    // Chromium says true), so it would call every nested box scrolled. The C# suite pins the rule's
    // shape, and the pull request's Chromium run measured both nestings.
  });

  describe('the header is tracked once a box asks', () => {
    const frame = (): Promise<void> =>
      new Promise((resolve) => requestAnimationFrame(() => resolve()));

    beforeEach(() => {
      document.body.innerHTML = '';
      Object.defineProperty(window, 'scrollY', { value: 0, configurable: true });
      resetScrolledController();
    });

    afterEach(() => resetScrolledController());

    // The controller was installed by a header with a scrolled style of its own; a header that styles
    // nothing kept no attribute, and its boxes never moved.
    it('a header with no scrolled style of its own is marked when the page scrolls', async () => {
      new Pinned(badge()).render();

      const bar = document.createElement('div');
      bar.setAttribute(PINNED_MARKER, '1');
      document.body.append(bar);
      Object.defineProperty(window, 'scrollY', { value: 20, configurable: true });
      document.dispatchEvent(new Event('scroll'));
      await frame();

      expect(bar.hasAttribute(SCROLLED_MARKER)).toBe(true);
    });
  });
});
