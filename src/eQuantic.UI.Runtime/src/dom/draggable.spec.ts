import { describe, it, expect, beforeEach, afterEach } from 'vitest';
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

    expect(surface.style.transform).toBe('');
    expect(released).toEqual([]);
  });

  it('follows the finger past the slop and reports where it was left', () => {
    down(100, 100);
    move(80, 100);
    expect(surface.style.transform).toBe('translateX(-20px)');

    up(80, 100);
    expect(released).toEqual([-20]);
  });

  it('never leaves the limits the node carries', () => {
    down(100, 100);
    move(-400, 100);

    expect(surface.style.transform).toBe('translateX(-96px)');
    up(-400, 100);
    expect(released).toEqual([-96]);
  });

  it('does not arm on travel across its own axis', () => {
    // Without this a list holding a swipeable row cannot be scrolled.
    down(100, 100);
    move(100, 180);
    up(100, 180);

    expect(surface.style.transform).toBe('');
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
    up(150, 100);

    expect(moved).toEqual([0.5]); // 0.25 + 50/200
    expect(released).toEqual([0.5]);
    expect(surface.style.transform).toBe('translateX(100px)'); // painted back in dp
  });

  it('does not translate a gesture the caller paints itself', () => {
    surface.setAttribute('data-eq-drag-follows', '0');
    surface.setAttribute('data-eq-drag-moves', '1');

    down(100, 100);
    move(60, 100);

    expect(moved).toEqual([-40]);
    expect(surface.style.transform).toBe('');
    up(60, 100);
  });

  it('gives the surface back when the system cancels the gesture', () => {
    // A cancel decides nothing — the browser claimed the scroll, a call arrived — so nothing is
    // reported and the surface returns to where the caller last put it.
    surface.setAttribute('data-eq-drag-rest', '-96');
    down(100, 100);
    move(140, 100);
    expect(surface.style.transform).toBe('translateX(-56px)');

    at(document, 'pointercancel', 140, 100);
    expect(surface.style.transform).toBe('translateX(-96px)');
    expect(released).toEqual([]);

    // The gesture is over: nothing follows a pointer that is no longer being tracked.
    move(200, 100);
    expect(surface.style.transform).toBe('translateX(-96px)');
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
});
