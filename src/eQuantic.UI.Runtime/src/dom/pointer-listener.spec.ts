/**
 * The pointer controller (#1562): every pointer event is hit-tested against the listeners under it, by the rule
 * Photon runs too, and each one that hears it is told in its own box. The browser's hit test is stubbed with the
 * elements a real page would stack under the point (`elementsFromPoint`, topmost first), and each case is one
 * `ListenerHostTests` proves for Photon.
 */
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { StatefulComponent } from '../core/component';
import { photonTheme } from '../shared/design-system.generated';
import { setPhotonTheme } from '../shared/photon-context';
import { Box, BoxStyle, Listener } from '../shared/vocabulary';
import type { PointerEvent as HeardPointer } from '../shared/value-types';
import { installPointerListener, isSolid, resetPointerListener } from './pointer-listener';

setPhotonTheme(photonTheme);

type Heard = { who: string; event: HeardPointer };

/** A listener element, as the lowering marks one, telling `heard` what it hears. */
function listener(who: string, behavior: string, heard: Heard[], child: Element): HTMLElement {
  const element = document.createElement('div');
  element.setAttribute('data-eq-listener', behavior);
  element.addEventListener('eq-pointer', (ev) =>
    heard.push({ who, event: (ev as CustomEvent<HeardPointer>).detail }),
  );
  element.appendChild(child);
  return element;
}

/** A box that paints, as a filled `Box` lowers. */
function filled(): HTMLElement {
  const element = document.createElement('div');
  element.style.backgroundColor = 'rgb(16, 32, 48)';
  return element;
}

/** A box that paints nothing: a layout's empty space. */
function empty(): HTMLElement {
  return document.createElement('div');
}

function placeAt(element: Element, left: number, top: number, width = 200, height = 100): void {
  element.getBoundingClientRect = () =>
    ({ left, top, right: left + width, bottom: top + height, width, height, x: left, y: top }) as DOMRect;
}

/** What the browser stacks under the point, topmost first, down to the root. */
function under(...topmostFirst: Element[]): void {
  document.elementsFromPoint = () => [...topmostFirst, document.body, document.documentElement];
}

function pointer(
  type: string,
  x: number,
  y: number,
  init: { buttons?: number; pointerType?: string; pointerId?: number } = {},
): MouseEvent {
  const event = new MouseEvent(type, { bubbles: true, cancelable: true, clientX: x, clientY: y, buttons: init.buttons ?? 0 });
  Object.defineProperty(event, 'pointerType', { value: init.pointerType ?? 'mouse' });
  Object.defineProperty(event, 'pointerId', { value: init.pointerId ?? 1 });
  document.body.dispatchEvent(event);
  return event;
}

describe('a Listener, hearing the pointer in the browser', () => {
  let heard: Heard[];

  beforeEach(() => {
    heard = [];
    document.body.innerHTML = '';
    installPointerListener();
  });

  afterEach(() => {
    resetPointerListener();
  });

  it('follows a press dragged outside and released, in its own box', () => {
    const child = filled();
    const l = listener('L', 'defer', heard, child);
    document.body.appendChild(l);
    placeAt(l, 50, 100);
    under(child, l);

    pointer('pointerdown', 60, 106, { buttons: 1 });
    under(document.body);
    pointer('pointermove', 260, 106, { buttons: 1 });
    pointer('pointerup', 260, 106, { buttons: 0 });

    expect(heard.map((h) => [h.event.phase, h.event.position.x, h.event.position.y, h.event.buttons])).toEqual([
      ['down', 10, 6, 1],
      ['move', 210, 6, 1],
      ['up', 210, 6, 0],
    ]);
    expect(heard.every((h) => h.event.kind === 'mouse' && h.event.pointer === 1)).toBe(true);
  });

  it('takes nothing from a button inside it', () => {
    const button = document.createElement('button');
    const l = listener('L', 'defer', heard, button);
    document.body.appendChild(l);
    placeAt(l, 0, 0);
    under(button, l);
    let clicked = 0;
    button.addEventListener('click', () => clicked++);

    const down = pointer('pointerdown', 20, 20, { buttons: 1 });
    pointer('pointerup', 20, 20);
    button.click();

    expect(heard.map((h) => h.event.phase)).toEqual(['down', 'up']);
    expect(down.defaultPrevented).toBe(false);
    expect(clicked).toBe(1);
  });

  for (const [behavior, phases] of [
    ['defer', []],
    ['opaque', ['down', 'up']],
    ['translucent', ['down', 'up']],
  ] as const) {
    it(`hears the gap of a column only when it is not deferring (${behavior})`, () => {
      const column = empty();
      column.append(filled(), filled());
      const l = listener('L', behavior, heard, column);
      document.body.appendChild(l);
      placeAt(l, 0, 0);
      under(column, l);

      pointer('pointerdown', 50, 50, { buttons: 1 });
      pointer('pointerup', 50, 50);

      expect(heard.map((h) => h.event.phase)).toEqual(phases);
    });
  }

  for (const [behavior, who] of [
    ['translucent', ['upper', 'lower']],
    ['opaque', ['upper']],
    ['defer', ['lower']],
  ] as const) {
    it(`two listeners in a stack hear as the upper one's behaviour says (${behavior})`, () => {
      const stack = empty();
      const lowerChild = filled();
      const upperChild = empty();
      const lower = listener('lower', 'defer', heard, lowerChild);
      const upper = listener('upper', behavior, heard, upperChild);
      stack.append(lower, upper);
      document.body.appendChild(stack);
      placeAt(lower, 0, 0);
      placeAt(upper, 0, 0);
      under(upperChild, upper, lowerChild, lower, stack);

      pointer('pointerdown', 30, 30, { buttons: 1 });

      expect(heard.map((h) => h.who)).toEqual(who);
    });
  }

  it('hovers for a mouse and never for a finger', () => {
    const child = filled();
    const l = listener('L', 'defer', heard, child);
    document.body.appendChild(l);
    placeAt(l, 0, 0);
    under(child, l);

    pointer('pointermove', 5, 5, { pointerType: 'touch', pointerId: 9 });
    pointer('pointermove', 7, 8);

    expect(heard.map((h) => [h.event.phase, h.event.position.x, h.event.position.y, h.event.buttons])).toEqual([
      ['hover', 7, 8, 0],
    ]);
  });

  it('hears a wheel with its delta in dp', () => {
    const child = filled();
    const l = listener('L', 'defer', heard, child);
    document.body.appendChild(l);
    placeAt(l, 0, 0);
    under(child, l);

    document.body.dispatchEvent(
      new WheelEvent('wheel', { bubbles: true, clientX: 5, clientY: 5, deltaY: 3, deltaMode: 1 }),
    );

    expect(heard.map((h) => [h.event.phase, h.event.scrollDelta.x, h.event.scrollDelta.y])).toEqual([
      ['scroll', 0, 48],
    ]);
  });

  it('hears a cancelled press as a cancel, and nothing after it', () => {
    const child = filled();
    const l = listener('L', 'defer', heard, child);
    document.body.appendChild(l);
    placeAt(l, 0, 0);
    under(child, l);

    pointer('pointerdown', 10, 6, { buttons: 1, pointerType: 'touch', pointerId: 4 });
    pointer('pointermove', 15, 9, { buttons: 1, pointerType: 'touch', pointerId: 4 });
    pointer('pointercancel', 15, 9, { pointerType: 'touch', pointerId: 4 });
    pointer('pointermove', 20, 9, { buttons: 1, pointerType: 'touch', pointerId: 4 });

    expect(heard.map((h) => [h.event.phase, h.event.kind])).toEqual([
      ['down', 'touch'],
      ['move', 'touch'],
      ['cancel', 'touch'],
    ]);
  });
});

describe('what the browser counts as hit', () => {
  it('is what draws or answers, and never a layout box that paints nothing', () => {
    const text = document.createElement('span');
    text.textContent = 'words';
    const bordered = empty();
    bordered.style.border = '1px solid rgb(0, 0, 0)';
    const black = empty();
    black.style.backgroundColor = 'rgb(0, 0, 0)';
    const clear = empty();
    clear.style.backgroundColor = 'rgba(0, 0, 0, 0)';
    const scroller = empty();
    scroller.style.overflowY = 'auto';
    const layout = empty();
    // In the document, as everything the browser's hit test returns is: a computed style is read from there.
    document.body.append(text, bordered, black, clear, scroller, layout);

    expect([text, bordered, black, scroller, document.createElement('button')].map(isSolid)).toEqual([
      true,
      true,
      true,
      true,
      true,
    ]);
    expect([layout, clear].map(isSolid)).toEqual([false, false]);
  });
});

describe('a Listener through the real lowering', () => {
  class Page extends StatefulComponent {
    constructor(private readonly node: unknown) {
      super();
    }

    build(): never {
      return this.node as never;
    }
  }

  afterEach(() => resetPointerListener());

  it('is a div marked with its behaviour, whose event reaches the callback of its phase', () => {
    const events: string[] = [];
    const host = document.createElement('div');
    document.body.appendChild(host);
    const node = new Listener(new Box(new BoxStyle({ width: 100, height: 40 })), {
      onPointerDown: (e: HeardPointer) => events.push(`down ${e.position.x},${e.position.y}`),
      onPointerUp: (e: HeardPointer) => events.push(`up ${e.buttons}`),
      behavior: 'opaque',
    });
    new Page(node).mount(host);
    const element = host.querySelector('[data-eq-listener]') as HTMLElement;
    placeAt(element, 10, 20);
    under(element.firstElementChild!, element);

    pointer('pointerdown', 15, 30, { buttons: 1 });
    pointer('pointerup', 15, 30);

    expect(element.getAttribute('data-eq-listener')).toBe('opaque');
    expect(events).toEqual(['down 5,10', 'up 0']);
  });
});
