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
  function chrome(height: number): {
    remove: () => void;
    growTo: (next: number) => void;
  } {
    const bar = document.createElement('div');
    bar.setAttribute(PINNED_MARKER, '');
    let tall = height;
    bar.getBoundingClientRect = () =>
      ({ top: 0, bottom: tall, height: tall, left: 0, right: 0, width: 0, x: 0, y: 0 }) as DOMRect;
    document.body.appendChild(bar);
    return { remove: () => bar.remove(), growTo: (next) => (tall = next) };
  }

  function bookmark(
    id: string,
    top: number,
  ): { element: HTMLElement; seen: () => number; moveTo: (next: number) => void } {
    const element = document.createElement('div');
    element.id = id;
    let calls = 0;
    let at = top;
    element.scrollIntoView = () => {
      calls += 1;
    };
    element.getBoundingClientRect = () =>
      ({ top: at, bottom: at, height: 0, left: 0, right: 0, width: 0, x: 0, y: at }) as DOMRect;
    document.body.appendChild(element);
    return { element, seen: () => calls, moveTo: (next) => (at = next) };
  }

  /**
   * jsdom reports `complete` from the first line of the file, so a spec that does not say otherwise
   * exercises the already-loaded branch and NEVER the `load` listener — which is the branch a real
   * cold load takes, and the only one that matters. Stated rather than inherited.
   */
  function stillLoading(): void {
    Object.defineProperty(document, 'readyState', { value: 'loading', configurable: true });
  }

  function loaded(): void {
    Object.defineProperty(document, 'readyState', { value: 'complete', configurable: true });
  }

  /**
   * Frames, driven by hand. The correction watches a WINDOW of them rather than one event, because
   * the browser's fragment jump lands in some frame and no event names which — so a spec that
   * cannot step frames cannot express the ordering this file is about.
   */
  let pending: Array<() => void> = [];

  function frame(times = 1): void {
    for (let i = 0; i < times; i++) {
      const due = pending;
      pending = [];
      for (const callback of due) callback();
    }
  }

  const realRaf = window.requestAnimationFrame;
  const realNow = performance.now;
  let clock = 0;

  beforeEach(() => {
    resetColdLoadRealignmentForTests();
    pending = [];
    clock = 0;
    performance.now = () => clock;
    window.requestAnimationFrame = ((cb: FrameRequestCallback) => {
      pending.push(() => cb(0));
      return pending.length;
    }) as typeof window.requestAnimationFrame;
    document.documentElement.style.removeProperty('--eq-anchor-offset');
    window.history.replaceState(null, '', '/probe');
  });

  afterEach(() => {
    window.requestAnimationFrame = realRaf;
    performance.now = realNow;
    pending = [];
    Object.defineProperty(document, 'readyState', { value: 'complete', configurable: true });
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

  /**
   * THE ORDER THE OTHER THREE DO NOT TEST: the measurement lands BEFORE the browser has finished
   * its fragment jump.
   *
   * <para>
   * Every case above calls `publishAnchorOffset` with the target already where the jump left it, so
   * "out of the band" only ever meant "a reader who scrolled away". It also means a target still
   * far down the page — which is where it sits until the browser jumps, and the browser re-runs
   * that jump as late content settles the layout. The one-shot flag is spent by that early
   * measurement, so when the jump finally lands the target at 0 there is no chance left.
   * </para>
   *
   * <para>
   * Reported from a live site: `/privacy#rights` cold, `scrollY 3992`, `targetTop 0` on all eight
   * samples over four seconds, with `--eq-anchor-offset` and `scroll-margin-top` both reading 65px.
   * The same site's WARM navigation to the same fragment lands at 64, which is what says the
   * mechanism is right and only this ordering is wrong.
   * </para>
   */
  it('still corrects when the first measurement beat the browser to the jump', () => {
    const bar = chrome(64);
    // Where the target sits while the document is still settling: far below the fold.
    const target = bookmark('rights', 3992);
    window.history.replaceState(null, '', '/probe#rights');

    publishAnchorOffset();
    expect(target.seen()).toBe(0);

    // The browser now performs the jump it had not finished, with `scroll-margin-top` resolving
    // through a variable that is only NOW set — so it lands at the top, behind the chrome.
    target.moveTo(0);
    document.documentElement.style.removeProperty('--eq-anchor-offset');
    publishAnchorOffset();

    expect(target.seen()).toBe(1);
    bar.remove();
  });

  /**
   * The SAME ordering, with the thing the real page does not have: a second render pass.
   *
   * <para>
   * `publishAnchorOffset` runs after a pass, and a settled page has none — so on a live site the
   * correction's second chance cannot come from another measurement. It comes from WATCHING: the
   * jump lands in some frame and no event names which, so the window looks every frame until the
   * target is in the band. Without that, moving the one-shot flag onto the correction fixes nothing
   * here — the page stays behind the header with the flag still armed and nobody left to read it.
   * </para>
   */
  it('takes its second chance from the frame window when no further pass ever comes', () => {
    stillLoading();
    const bar = chrome(64);
    const target = bookmark('rights', 3992);
    window.history.replaceState(null, '', '/probe#rights');

    publishAnchorOffset();
    expect(target.seen()).toBe(0);

    // The browser finishes its jump as the last of the layout settles, and then the document loads.
    target.moveTo(0);
    frame();

    expect(target.seen()).toBe(1);
    bar.remove();
  });

  /**
   * And the chance is spent for good once the window closes, so a reader who scrolls the target
   * into the band later is never yanked. This is what the A/B against the plausible wrong fix
   * — "spend the flag on the correction instead of the measurement" — lands on: on its own that
   * leaves the page armed forever.
   */
  it('is retired by the window even when it had nothing to correct', () => {
    stillLoading();
    const bar = chrome(64);
    const target = bookmark('rights', 3992);
    window.history.replaceState(null, '', '/probe#rights');

    publishAnchorOffset();
    loaded();
    frame(40); // the window closes: still out of the band throughout, chance spent

    // The reader now scrolls the target into the band by hand. Nothing may move.
    target.moveTo(10);
    document.documentElement.style.removeProperty('--eq-anchor-offset');
    publishAnchorOffset();

    expect(target.seen()).toBe(0);
    bar.remove();
  });

  /**
   * The deferred chance measures the chrome AGAIN, never the number that booked it.
   *
   * <para>
   * Found in review, and it is the same mistake one level down: the whole point of deferring is
   * that the layout was not final, and the chrome is part of that layout — a bar wraps at a narrow
   * width, or grows when a webfont finally arrives. Correcting against the height measured at first
   * paint leaves the target under the header the page actually ended up with, which is the bug this
   * file exists to prevent, reintroduced by its own fix.
   * </para>
   */
  it('corrects against the chrome the page ENDED with, not the one that booked the re-check', () => {
    stillLoading();
    const bar = chrome(64);
    const target = bookmark('rights', 3992);
    window.history.replaceState(null, '', '/probe#rights');

    publishAnchorOffset();
    expect(target.seen()).toBe(0);

    // The webfont lands: the bar grows, and the browser's jump leaves the target at 70 — clear of
    // the 64 that booked this, still buried under the 80 the header now is.
    bar.growTo(80);
    target.moveTo(70);
    frame();

    expect(target.seen()).toBe(1);
    expect(document.documentElement.style.getPropertyValue('--eq-anchor-offset')).toBe('80px');
    bar.remove();
  });

  /**
   * Chrome that is not THERE yet measures zero, and zero is a reason to come back rather than a
   * reason to stop.
   *
   * <para>
   * An image-backed header before its image, or a bar whose webfont has not arrived, is zero-high at
   * first paint and grows with no further render pass to notice. The first version of this fix
   * returned on `offset <= 0` before it had even looked for a target, so the page this correction
   * exists for was the one page it retired on. Found in review.
   * </para>
   */
  it('comes back for chrome that was not there yet', () => {
    stillLoading();
    const bar = chrome(0);
    const target = bookmark('rights', 0);
    window.history.replaceState(null, '', '/probe#rights');

    publishAnchorOffset();
    expect(target.seen()).toBe(0);

    bar.growTo(64); // the header's image finally decodes
    frame();

    expect(target.seen()).toBe(1);
    bar.remove();
  });

  /** A page that never asks for an element books nothing — no listener, no frame, no work. */
  it('books nothing for a URL with no fragment', () => {
    stillLoading();
    const bar = chrome(64);
    const target = bookmark('rights', 0);
    window.history.replaceState(null, '', '/probe');

    publishAnchorOffset();
    loaded();
    frame(40);

    expect(target.seen()).toBe(0);
    bar.remove();
  });

  /**
   * Work booked by one document may never land on the next one.
   *
   * <para>
   * A deferred callback outlives the reset a spec performs between cases, so without a generation
   * the frame booked here would run against the following case's DOM and either suppress its
   * correction or perform one nobody asked for. That is a suite that lies about itself, which is
   * worse than one that fails — and this file's whole value is that it can be believed. Found in
   * review, against these very tests.
   * </para>
   */
  it('lets no deferred work cross into the next document', () => {
    stillLoading();
    const stale = chrome(64);
    const staleTarget = bookmark('rights', 3992);
    window.history.replaceState(null, '', '/probe#rights');
    publishAnchorOffset(); // books a re-check that belongs to THIS document
    stale.remove();
    document.body.innerHTML = '';

    // A new document begins, exactly as `beforeEach` does it.
    resetColdLoadRealignmentForTests();
    document.documentElement.style.removeProperty('--eq-anchor-offset');

    // …and the PREVIOUS document's deferred work lands now, before this one has measured anything.
    frame();
    // Ungenerationed it retires the flag here, and the correction below then finds its one chance
    // already spent — the suppression this guard exists for, and the reason the first version of
    // this case proved nothing: with both documents sharing a hash, the stale callback happened to
    // do the right thing by accident and the test passed either way.

    const bar = chrome(64);
    const target = bookmark('rights', 0); // squarely in the band: this one MUST be corrected
    window.history.replaceState(null, '', '/probe#rights');
    publishAnchorOffset();

    expect(staleTarget.seen()).toBe(0);
    expect(target.seen()).toBe(1);
    bar.remove();
  });

  /**
   * THE COLUMN THE FIRST FIX WAS BLIND TO: a WARM load, where the document is complete before the
   * browser has performed its fragment jump.
   *
   * <para>
   * Measured on a live site, same URL and same bytes, with the cache as the only variable:
   * </para>
   *
   * <para>
   * <c>cold, loadEventEnd 1310ms → target at 65, correct.</c><br/>
   * <c>warm, loadEventEnd 36ms → target at 0, wrong.</c>
   * </para>
   *
   * <para>
   * The failing case lands at the anchor's exact document offset, which is where a jump with no
   * scroll-margin puts it — so nothing scrolled OVER the correction, the correction never ran. A
   * single chance taken at `load` is a bet that `load` comes after the jump, and the faster the
   * page the more reliably it does not. A returning visitor is almost everyone, and a test that
   * loads instantly is always in this column, which is why 1,036 of them never saw it.
   * </para>
   */
  it('corrects a WARM load, where the document completed before the browser jumped', () => {
    loaded(); // 36ms: complete already, and the jump has not happened
    const bar = chrome(65);
    const target = bookmark('rights', 3992); // still where the document put it
    window.history.replaceState(null, '', '/probe#rights');

    publishAnchorOffset();
    expect(target.seen()).toBe(0);

    // The browser performs its jump a frame or two later, with scroll-margin-top still 0px.
    frame(2);
    target.moveTo(0);
    frame();

    expect(target.seen()).toBe(1);
    bar.remove();
  });

  /** And the window is long enough to be worth having: a jump several frames out is still caught. */
  it('is still watching several frames after the document completed', () => {
    loaded();
    const bar = chrome(65);
    const target = bookmark('rights', 3992);
    window.history.replaceState(null, '', '/probe#rights');

    publishAnchorOffset();
    frame(10);
    target.moveTo(0);
    frame();

    expect(target.seen()).toBe(1);
    bar.remove();
  });

  /**
   * A page that NEVER completes must not be watched forever.
   *
   * <para>
   * The frame budget only starts counting once `readyState` is complete, which is right — before
   * that the browser may still have a jump to perform. But a stalled subresource can hold a page
   * non-complete for the life of the tab, and a budget that never starts is a rAF loop reading
   * layout every frame for all of it. Found in review.
   * </para>
   */
  it('stops watching a page that never completes', () => {
    stillLoading();
    const bar = chrome(65);
    const target = bookmark('rights', 3992); // never enters the band
    window.history.replaceState(null, '', '/probe#rights');

    publishAnchorOffset();
    frame(40);
    // Still watching, because the wall clock has not run out.
    expect(pending.length).toBeGreaterThan(0);

    clock += 10_001; // the watch's own bound, with readyState still 'loading'
    frame();

    expect(pending).toHaveLength(0);

    // …and it is retired, not merely idle: the reader scrolling into the band later moves nothing.
    target.moveTo(10);
    document.documentElement.style.removeProperty('--eq-anchor-offset');
    publishAnchorOffset();
    expect(target.seen()).toBe(0);
    bar.remove();
  });

  /**
   * Without `requestAnimationFrame` the watch uses a TIMER, never a direct call. Invoking the
   * callback synchronously is unbounded recursion on a loading document, and burns the whole budget
   * in one stack frame on a complete one — both before the browser could perform the jump being
   * waited for. Found in review.
   */
  it('falls back to a timer, not to calling itself', () => {
    stillLoading();
    const raf = window.requestAnimationFrame;
    // @ts-expect-error — the environment this guards against is one with no rAF at all.
    delete window.requestAnimationFrame;
    const timers: Array<() => void> = [];
    const realTimeout = window.setTimeout;
    window.setTimeout = ((cb: () => void) => {
      timers.push(cb);
      return timers.length;
    }) as typeof window.setTimeout;

    try {
      const bar = chrome(65);
      const target = bookmark('rights', 3992);
      window.history.replaceState(null, '', '/probe#rights');

      // Returns rather than recursing: one timer is booked and nothing has run yet.
      publishAnchorOffset();
      expect(timers).toHaveLength(1);

      target.moveTo(0);
      timers.pop()!();
      expect(target.seen()).toBe(1);
      bar.remove();
    } finally {
      window.setTimeout = realTimeout;
      window.requestAnimationFrame = raf;
    }
  });

  /**
   * A BACKGROUND TAB keeps `requestAnimationFrame` defined and stops calling it back. Guarding on
   * `typeof` is therefore not enough: the rAF branch is taken, nothing runs, and neither the frame
   * budget nor the wall clock is enforced — the watch stays armed until the tab returns and could
   * yank a reader long past its own deadline. Found in review, and the timer beside rAF is what
   * makes the bound hold rather than the correction happen.
   */
  it('is still bounded when frames are suspended and only timers run', () => {
    stillLoading();
    const timers: Array<() => void> = [];
    const realTimeout = window.setTimeout;
    window.setTimeout = ((cb: () => void) => {
      timers.push(cb);
      return timers.length;
    }) as typeof window.setTimeout;

    try {
      const bar = chrome(65);
      const target = bookmark('rights', 3992); // never enters the band
      window.history.replaceState(null, '', '/probe#rights');

      publishAnchorOffset();
      // rAF was called and will never call back, the way a hidden tab behaves. `pending` holds the
      // frame nobody will run; the timer beside it is the only thing that moves.
      expect(pending.length).toBeGreaterThan(0);
      expect(timers.length).toBeGreaterThan(0);

      clock += 10_001;
      while (timers.length > 0) timers.pop()!();

      // Retired by the clock, through the timer alone — no frame ever ran.
      target.moveTo(10);
      document.documentElement.style.removeProperty('--eq-anchor-offset');
      publishAnchorOffset();
      expect(target.seen()).toBe(0);
      bar.remove();
    } finally {
      window.setTimeout = realTimeout;
    }
  });

  /**
   * A tick that arrives AFTER the deadline corrects nothing.
   *
   * <para>
   * Ticks can be late: rAF resumes when a background tab returns, a backstop timer runs when the
   * event loop gets to it. Correcting on one of those is the yank the bound exists to prevent,
   * performed by the instrument meant to stop it — so the deadline is checked BEFORE the
   * correction, not after. Found in review, and it is the order I had already described in an
   * earlier round of this PR and then not written.
   * </para>
   */
  it('a tick arriving past the deadline corrects nothing', () => {
    stillLoading();
    const bar = chrome(65);
    const target = bookmark('rights', 3992); // out of the band while the watch is legitimate
    window.history.replaceState(null, '', '/probe#rights');

    publishAnchorOffset();
    frame(3);
    expect(target.seen()).toBe(0);

    // The tab was hidden for a while. The reader comes back having scrolled the target into the
    // band by hand, and the frame that was queued finally runs.
    clock += 10_001;
    target.moveTo(10);
    frame();

    expect(target.seen()).toBe(0);
    bar.remove();
  });

  /**
   * The watch belongs to the VIEW it was booked in. This runtime is a SPA: the router pushes state
   * on the same document and applies each new fragment scroll itself, so a cold-load watch still
   * alive across a navigation would read the NEW `location.hash` and scroll to somebody else's
   * target. Found in review.
   */
  it('does not follow the reader into another view', () => {
    stillLoading();
    const bar = chrome(65);
    const first = bookmark('rights', 3992);
    window.history.replaceState(null, '', '/probe#rights');
    publishAnchorOffset();
    frame(2);
    expect(first.seen()).toBe(0);

    // The router navigates. A different page, a different anchor, squarely in the band.
    const second = bookmark('liability', 0);
    window.history.replaceState(null, '', '/terms#liability');
    frame();

    expect(second.seen()).toBe(0);
    expect(first.seen()).toBe(0);
    bar.remove();
  });

  /**
   * Expiry belongs to the SHARED path, not to the deferred tick's closure. `publishAnchorOffset`
   * calls the correction directly on any later render pass whose measurement changed, so a bound
   * that lived only in the tick left that door open — a background render an hour in could still
   * yank a reader. Found in review.
   */
  it('a later render pass cannot correct past the deadline either', () => {
    stillLoading();
    const bar = chrome(65);
    const target = bookmark('rights', 3992);
    window.history.replaceState(null, '', '/probe#rights');
    publishAnchorOffset();

    // Frames and timers are suspended for longer than the watch is allowed to live, and then a
    // render pass changes the header height — the direct path, with no tick involved.
    clock += 10_001;
    target.moveTo(10);
    bar.growTo(80);
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
