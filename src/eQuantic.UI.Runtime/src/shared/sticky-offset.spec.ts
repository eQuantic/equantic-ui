/**
 * The room a bookmark keeps above itself is MEASURED from the chrome that would cover it, because
 * the height of a content-sized bar is not knowable before layout — and because one app here has a
 * 60dp nav on one page and a 56dp topbar on another, neither of which states a number.
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { publishAnchorOffset, resetColdLoadRealignmentForTests } from './sticky-offset';
import { PINNED_MARKER } from './markers';
import { lowerVisualNode } from './lowering';
import { photonTheme } from './design-system.generated';
import type { LoweringContext } from './lowering';
import type { VisualNodeValue } from './nodes';

/**
 * The reader and the emitter must agree on the marker, and NOTHING in the type system says so. This
 * suite used to set the attribute by hand, which meant it tested the reader against markup nothing
 * emits: the `Sticky` → `Pinned` rename moved both emitters, missed the reader, and every fragment
 * link on a live site landed behind the header for a release while this file stayed green.
 *
 * The first test below now takes the attribute FROM THE LOWERING — the same function the runtime
 * calls — so the two cannot drift again without it failing.
 */
describe('the marker the reader queries is the one the lowering writes', () => {
  it('a lowered Pinned carries the attribute overlappingChrome looks for', () => {
    const ctx: LoweringContext = { textPrimary: photonTheme.textPrimary };
    const lowered = lowerVisualNode(
      {
        nodeKind: 'pinned',
        child: { nodeKind: 'box', style: {} } as unknown as VisualNodeValue,
        offset: 0,
      } as unknown as VisualNodeValue,
      ctx,
    );

    const marked = document.createElement('div');
    for (const [name, value] of Object.entries(lowered?.attributes ?? {})) {
      if (typeof value === 'string') marked.setAttribute(name, value);
    }
    marked.getBoundingClientRect = () => ({
      top: 0,
      height: 61,
      bottom: 61,
      left: 0,
      right: 0,
      width: 0,
      x: 0,
      y: 0,
      toJSON: () => ({}),
    });
    document.body.appendChild(marked);

    publishAnchorOffset();
    expect(document.documentElement.style.getPropertyValue('--eq-anchor-offset')).toBe('61px');

    marked.remove();
    document.documentElement.style.removeProperty('--eq-anchor-offset');
  });
});

describe('the anchor offset comes from the sticky, not from a constant', () => {
  const sticky = (top: number, height: number): HTMLElement => {
    const element = document.createElement('div');
    element.setAttribute(PINNED_MARKER, '1');
    element.getBoundingClientRect = () => ({
      top,
      height,
      bottom: top + height,
      left: 0,
      right: 0,
      width: 0,
      x: 0,
      y: top,
      toJSON: () => ({}),
    });
    document.body.appendChild(element);
    return element;
  };

  beforeEach(() => {
    document.body.innerHTML = '';
    document.documentElement.style.removeProperty('--eq-anchor-offset');
  });

  afterEach(() => document.documentElement.style.removeProperty('--eq-anchor-offset'));

  const offset = (): string =>
    document.documentElement.style.getPropertyValue('--eq-anchor-offset');

  it('publishes the height of the chrome pinned at the top', () => {
    sticky(0, 60);
    publishAnchorOffset();
    expect(offset()).toBe('60px');
  });

  it('takes the TALLEST when more than one is pinned', () => {
    sticky(0, 56);
    sticky(0, 60);
    publishAnchorOffset();
    expect(offset()).toBe('60px');
  });

  it('ignores a sticky that has not pinned yet', () => {
    // Still down the page: it covers nothing a bookmark would land under.
    sticky(300, 60);
    publishAnchorOffset();
    expect(offset()).toBe('0px');
  });

  it('never under-offsets on a fractional device pixel', () => {
    // 55.4 rounds DOWN to 55 and leaves the target four tenths of a pixel behind the bar. Visible;
    // the extra sub-pixel in the other direction is not.
    sticky(0, 55.4);
    publishAnchorOffset();
    expect(offset()).toBe('56px');
  });

  it('is zero when the page has no chrome at all', () => {
    publishAnchorOffset();
    expect(offset()).toBe('0px');
  });
});

describe('the offset reaches a real run', () => {
  it('is published when a reconciler pass ends', async () => {
    // The first version of this shipped with the publisher WRITTEN AND NEVER CALLED: the edit that
    // wired it used an anchor that only existed on another branch and silently did nothing. A
    // module nobody calls is a feature nobody gets, so the call is asserted through the real seam.
    document.body.innerHTML = '';
    document.documentElement.style.removeProperty('--eq-anchor-offset');

    const bar = document.createElement('div');
    bar.setAttribute(PINNED_MARKER, '1');
    bar.getBoundingClientRect = () => ({
      top: 0,
      height: 56,
      bottom: 56,
      left: 0,
      right: 0,
      width: 0,
      x: 0,
      y: 0,
      toJSON: () => ({}),
    });
    document.body.appendChild(bar);

    const { ComponentInstanceStore, enterPass, exitPass } = await import('./instance-store');
    enterPass(new ComponentInstanceStore(), null);
    exitPass();

    // NOT yet: the pass ends before the render manager writes its tree, so the measurement is
    // deferred to the microtask after the write. This assertion is the one that would have caught
    // the synchronous version — the spec put the chrome in the DOM by hand and so measured
    // something that a real client-only render would not have had yet.
    expect(document.documentElement.style.getPropertyValue('--eq-anchor-offset')).toBe('');

    await Promise.resolve();
    expect(document.documentElement.style.getPropertyValue('--eq-anchor-offset')).toBe('56px');
  });
});

describe('the room a bookmark keeps is an atomic class', () => {
  it('matches what the C# atomiser produces, not an inline declaration', async () => {
    // SSR under the style sink turns every declaration into a class and leaves inline only the
    // custom-property tail. Writing this one inline on the client — which the first version did —
    // put SSR and hydration one attribute apart on every bookmarked element, and the reconciler
    // would patch it on every hydrate.
    const { atomizeEntries } = await import('./style-atomizer');
    const { lowerVisualNode } = await import('./lowering');
    const { Box, BoxStyle } = await import('./vocabulary');

    const box = new Box(new BoxStyle({}));
    (box as unknown as { bookmark: string }).bookmark = 'features';
    const node = lowerVisualNode(box as never, {} as never);

    const expected = atomizeEntries({ 'scroll-margin-top': 'var(--eq-anchor-offset, 0px)' }).class;
    expect(expected).not.toBe('');
    expect(node.attributes['class'] ?? '').toContain(expected);
    expect(node.attributes['style'] ?? '').not.toContain('scroll-margin-top');
  });
});

/**
 * A COLD load with a fragment is the one case the offset arrives too late for. The browser performs
 * the jump while `--eq-anchor-offset` is unset, so `scroll-margin-top` resolves to its `0px`
 * fallback and the target lands at the very top of the viewport, behind the header. Measured on a
 * live `/privacy#rights`: it scrolled, and it scrolled to the wrong place.
 */
describe('the first measurement corrects a cold load that landed under the chrome', () => {
  function chrome(height: number): HTMLElement {
    const bar = document.createElement('div');
    bar.setAttribute(PINNED_MARKER, '');
    bar.getBoundingClientRect = () =>
      ({ top: 0, bottom: height, height, left: 0, right: 0, width: 0, x: 0, y: 0 }) as DOMRect;
    document.body.appendChild(bar);
    return bar;
  }

  function bookmark(id: string, top: number): { element: HTMLElement; seen: () => number } {
    const element = document.createElement('div');
    element.id = id;
    let calls = 0;
    element.scrollIntoView = () => {
      calls += 1;
    };
    element.getBoundingClientRect = () =>
      ({ top, bottom: top, height: 0, left: 0, right: 0, width: 0, x: 0, y: top }) as DOMRect;
    document.body.appendChild(element);
    return { element, seen: () => calls };
  }

  beforeEach(() => {
    resetColdLoadRealignmentForTests();
    document.documentElement.style.removeProperty('--eq-anchor-offset');
    window.history.replaceState(null, '', '/probe');
  });

  afterEach(() => {
    document.body.innerHTML = '';
    window.history.replaceState(null, '', '/probe');
  });

  it('re-scrolls the fragment target that the browser left behind the header', () => {
    const bar = chrome(64);
    const target = bookmark('rights', 0); // where a 0px scroll-margin left it
    window.history.replaceState(null, '', '/probe#rights');

    publishAnchorOffset();

    expect(target.seen()).toBe(1);
    bar.remove();
  });

  it('leaves a reader who has scrolled somewhere else alone', () => {
    const bar = chrome(64);
    // Well clear of the chrome: this is not the broken state, so nothing may move.
    const target = bookmark('rights', 400);
    window.history.replaceState(null, '', '/probe#rights');

    publishAnchorOffset();

    expect(target.seen()).toBe(0);
    bar.remove();
  });

  it('corrects once, so a later pass never yanks the page back', () => {
    const bar = chrome(64);
    const target = bookmark('rights', 0);
    window.history.replaceState(null, '', '/probe#rights');

    publishAnchorOffset();
    document.documentElement.style.removeProperty('--eq-anchor-offset');
    publishAnchorOffset();

    expect(target.seen()).toBe(1);
    bar.remove();
  });
});
