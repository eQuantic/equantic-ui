import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { installDraggableController, resetDraggableController } from './draggable';

/**
 * The web half of the continuous gesture. The controller is a MECHANISM: it never learns what a
 * particular gesture means. Everything it obeys — the axis, the limits, whether it reports dp or a
 * fraction, whether the element follows the finger at all — travels with the element as data
 * attributes, written by the same lowering on the server and on the client.
 */
describe('draggable controller', () => {
  let surface: HTMLElement;
  let released: number[];
  let moved: number[];

  const at = (el: Element | Document, type: string, x: number, y: number): void => {
    el.dispatchEvent(
      new PointerEvent(type, { clientX: x, clientY: y, bubbles: true, cancelable: true }),
    );
  };
  // Only pointerdown is delegated from the element; the rest of the gesture rides the document, so
  // it keeps working when the finger leaves the surface it started on.
  const move = (x: number, y: number): void => at(document, 'pointermove', x, y);
  const up = (x: number, y: number): void => at(document, 'pointerup', x, y);
  const down = (x: number, y: number): void => at(surface, 'pointerdown', x, y);

  beforeEach(() => {
    released = [];
    moved = [];
    document.body.innerHTML = '<div id="s"></div>';
    surface = document.getElementById('s') as HTMLElement;
    surface.setAttribute('data-eq-drag', 'x');
    surface.setAttribute('data-eq-drag-min', '-96');
    surface.setAttribute('data-eq-drag-max', '0');
    surface.setAttribute('data-eq-drag-rest', '0');
    surface.addEventListener('eq-drag-released', (e) =>
      released.push((e as CustomEvent<number>).detail),
    );
    surface.addEventListener('eq-drag-moved', (e) => moved.push((e as CustomEvent<number>).detail));
    installDraggableController();
  });

  afterEach(() => resetDraggableController());

  it('ignores travel inside the slop — a tap is a tap', () => {
    down(100, 100);
    move(94, 100);
    up(94, 100);

    expect(surface.style.getPropertyValue('translate')).toBe('');
    expect(released).toEqual([]);
  });

  it('follows the finger past the slop and reports where it was left', () => {
    down(100, 100);
    move(80, 100);
    expect(surface.style.getPropertyValue('translate')).toBe('-20px');

    up(80, 100);
    expect(released).toEqual([-20]);
  });

  it('moves by translate, so the box keeps its own transform for the whole gesture (#511)', () => {
    // The lowering writes the rest offset to `translate` and the box's own transform (a resting
    // scale, a hover's lift) to `transform`: CSS applies the two together. Written into the
    // transform, the drag replaced the box's own for as long as it lasted.
    surface.style.transform = 'scale(0.5)';
    down(100, 100);
    move(80, 100);

    expect(surface.style.getPropertyValue('translate')).toBe('-20px');
    expect(surface.style.transform).toBe('scale(0.5)');
    up(80, 100);
    expect(surface.style.transform).toBe('scale(0.5)');
  });

  it('hands the surface back to its markup after it reports the release', () => {
    // The report comes first, so a re-render it runs writes the new rest while the surface is
    // still where the finger left it; then the gesture's inline offset and its `transition: none`
    // go, and the markup's rest applies under the markup's glide. A release that changes no rest
    // (a swipe short of opening) re-writes nothing, and the surface still goes home: it used to
    // stay where the finger left it.
    const during: string[] = [];
    surface.addEventListener('eq-drag-released', () => during.push(surface.style.getPropertyValue('translate')));
    down(100, 100);
    move(80, 100);
    expect(surface.style.getPropertyValue('transition')).toBe('none');

    up(80, 100);
    expect(during).toEqual(['-20px']);
    expect(surface.style.getPropertyValue('translate')).toBe('');
    expect(surface.style.getPropertyValue('transition')).toBe('');
  });

  it('never leaves the limits the node carries', () => {
    down(100, 100);
    move(-400, 100);

    expect(surface.style.getPropertyValue('translate')).toBe('-96px');
    up(-400, 100);
    expect(released).toEqual([-96]);
  });

  it('does not arm on travel across its own axis', () => {
    // Without this a list holding a swipeable row cannot be scrolled.
    down(100, 100);
    move(100, 180);
    up(100, 180);

    expect(surface.style.getPropertyValue('translate')).toBe('');
    expect(released).toEqual([]);
  });

  it('measures from the rest it started at, not from zero', () => {
    // A row already open at -96, dragged 40 back, is at -56 — not at +40.
    surface.setAttribute('data-eq-drag-rest', '-96');
    down(100, 100);
    move(140, 100);
    up(140, 100);

    expect(released).toEqual([-56]);
  });

  it('reports a fraction of its own extent when the gesture is normalized', () => {
    // A slider's track is fluid: the component cannot know its pixel width, and the DOM can.
    surface.getBoundingClientRect = () => ({ width: 200, height: 40, left: 0, top: 0 }) as DOMRect;
    surface.setAttribute('data-eq-drag-normalized', '1');
    surface.setAttribute('data-eq-drag-min', '0');
    surface.setAttribute('data-eq-drag-max', '1');
    surface.setAttribute('data-eq-drag-rest', '0.25');
    surface.setAttribute('data-eq-drag-moves', '1');

    down(100, 100);
    move(150, 100);
    expect(surface.style.getPropertyValue('translate')).toBe('100px'); // painted back in dp
    up(150, 100);

    expect(moved).toEqual([0.5]); // 0.25 + 50/200
    expect(released).toEqual([0.5]);
  });

  it('does not translate a gesture the caller paints itself', () => {
    surface.setAttribute('data-eq-drag-follows', '0');
    surface.setAttribute('data-eq-drag-moves', '1');

    down(100, 100);
    move(60, 100);

    expect(moved).toEqual([-40]);
    expect(surface.style.getPropertyValue('translate')).toBe('');
    up(60, 100);
  });

  it('gives the surface back when the system cancels the gesture', () => {
    // A cancel decides nothing — the browser claimed the scroll, a call arrived — so nothing is
    // reported and the surface returns to where the caller last put it: the rest its markup
    // carries, once the gesture's inline offset is gone.
    surface.setAttribute('data-eq-drag-rest', '-96');
    down(100, 100);
    move(140, 100);
    expect(surface.style.getPropertyValue('translate')).toBe('-56px');

    at(document, 'pointercancel', 140, 100);
    expect(surface.style.getPropertyValue('translate')).toBe('');
    expect(surface.style.getPropertyValue('transition')).toBe('');
    expect(released).toEqual([]);

    // The gesture is over: nothing follows a pointer that is no longer being tracked.
    move(200, 100);
    expect(surface.style.getPropertyValue('translate')).toBe('');
  });

  it('swallows the click an armed drag would otherwise fire', () => {
    // A row's own buttons must not receive a tap that was really a swipe.
    let clicks = 0;
    surface.addEventListener('click', () => clicks++);

    down(100, 100);
    move(60, 100);
    up(60, 100);
    surface.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));

    expect(clicks).toBe(0);
  });

  it('lets the click through when the gesture never armed', () => {
    let clicks = 0;
    surface.addEventListener('click', () => clicks++);

    down(100, 100);
    move(96, 100);
    up(96, 100);
    surface.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));

    expect(clicks).toBe(1);
  });

  /**
   * The gesture schedules ONE thing for later: removing the click guard fifty milliseconds after
   * the finger lifts. A page cannot lose its `document` inside that window, and a torn-down test
   * environment can. Vitest removed ours while the timer was pending, the callback read a global
   * that no longer existed, and a run in which all 134 files had passed failed on
   * `document is not defined` — a red CI on a green tree. The timer holds the document it
   * registered on, so what it cleans up cannot go missing under it.
   */
  it('the click guard it scheduled still cleans up after the environment is gone', () => {
    vi.useFakeTimers();
    try {
      down(100, 100);
      move(80, 100);
      up(80, 100);

      // What a test environment's teardown does to the global, reproduced. Through `stubGlobal`
      // rather than by hand: it is what restores the ORIGINAL descriptor, and putting the value
      // back with `defineProperty` would leave every later spec a `document` that is writable and
      // enumerable when the environment's own was neither.
      vi.stubGlobal('document', undefined);

      expect(() => vi.advanceTimersByTime(60)).not.toThrow();
    } finally {
      vi.unstubAllGlobals();
      vi.useRealTimers();
    }
  });
});
