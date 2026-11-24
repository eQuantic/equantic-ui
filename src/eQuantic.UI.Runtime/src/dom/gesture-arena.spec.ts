/**
 * The gesture arena in the browser (#1563): a pointer that goes down on a `RawGestureDetector` opens an arena whose
 * members are the detector's recognizers and the SDK's own gestures under the point, and the transpiled
 * `GestureRouter` decides it by the rule Photon runs. The browser's hit test is stubbed with the elements a page
 * would stack under the point (`elementsFromPoint`, topmost first), and the pointer is driven as the browser drives
 * it: a `pointerdown` on the element pressed that bubbles to the window, moves and ups on the document, the `click`
 * the browser fires after an up, a `touchmove` beside a finger's moves.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { StatefulComponent } from '../core/component';
import { GestureRecognizer } from '../shared/components/GestureRecognizer';
import { GestureRecognizerFactoryWithHandlers } from '../shared/components/GestureRecognizerFactoryWithHandlers';
import { photonTheme } from '../shared/design-system.generated';
import { setPhotonTheme } from '../shared/photon-context';
import type { PointerEvent as HeardPointer } from '../shared/value-types';
import { Box, BoxStyle, Pressable, RawGestureDetector, TableViewCell, TableViewport, Text } from '../shared/vocabulary';
import { lowerVisualNode, type LoweringContext } from '../shared/lowering';
import { commitTableViewports } from '../shared/table-viewports';
import { TableScroll } from '../shared/components/TableScroll';
import { UniformTableSpans } from '../shared/components/UniformTableSpans';
import type { HtmlNode } from '../core/types';
import { installContextController, resetContextController } from './context-request';
import { installDraggableController, resetDraggableController } from './draggable';
import { installGestureArena, resetGestureArena, type GestureRequest } from './gesture-arena';

setPhotonTheme(photonTheme);

/** A recognizer as an app writes one: it says what it heard, and resolves as it is told to. */
class Probe extends GestureRecognizer {
  /** Called by the build that handed it its callbacks last. */
  onHeld: (() => void) | null = null;

  constructor(
    private readonly log: string[],
    private readonly rules: { deadlineMs?: number; acceptOnMove?: boolean; primaryOnly?: boolean } = {},
  ) {
    super();
  }

  isPointerAllowed(down: HeardPointer): boolean {
    return !this.rules.primaryOnly || (down.buttons & 1) !== 0;
  }

  addAllowedPointer(down: HeardPointer): void {
    this.log.push(`add ${down.position.x},${down.position.y}`);
    if (this.rules.deadlineMs !== undefined) this.startDeadline(this.rules.deadlineMs);
  }

  handleEvent(ev: HeardPointer): void {
    this.log.push(`${ev.phase} ${ev.position.x},${ev.position.y}`);
    if (this.rules.acceptOnMove && ev.phase === 'move') this.resolve('accepted');
  }

  didExceedDeadline(): void {
    this.log.push('deadline');
    this.onHeld?.();
    this.resolve('accepted');
  }

  acceptGesture(): void {
    this.log.push('accept');
  }

  rejectGesture(): void {
    this.log.push('reject');
  }
}

/** A detector element as the lowering marks one, answering the controller with `factories`. */
function detector(factories: unknown[], child: Element, behavior = 'defer'): HTMLElement {
  const element = document.createElement('div');
  element.setAttribute('data-eq-listener', behavior);
  element.setAttribute('data-eq-gestures', '');
  element.addEventListener('eq-gestures', (ev) => {
    (ev as CustomEvent<GestureRequest>).detail.gestures = factories as GestureRequest['gestures'];
  });
  element.appendChild(child);
  return element;
}

function factoryOf(make: () => Probe, initialize: (probe: Probe) => void = () => undefined): unknown {
  return new GestureRecognizerFactoryWithHandlers(make, initialize as (probe: unknown) => void);
}

/** A press, as an enabled `Pressable` lowers, counting its clicks. */
function button(clicks: string[]): HTMLButtonElement {
  const element = document.createElement('button');
  element.className = 'eq-pressable';
  element.textContent = 'Press';
  element.addEventListener('click', () => clicks.push('click'));
  return element;
}

function placeAt(element: Element, left: number, top: number, width = 200, height = 100): void {
  element.getBoundingClientRect = () =>
    ({ left, top, right: left + width, bottom: top + height, width, height, x: left, y: top }) as DOMRect;
}

/** What the browser stacks under the point, topmost first, down to the root. */
function under(...topmostFirst: Element[]): void {
  document.elementsFromPoint = () => [...topmostFirst, document.body, document.documentElement];
}

interface Pointing {
  buttons?: number;
  button?: number;
  pointerType?: string;
  pointerId?: number;
}

function pointer(on: EventTarget, type: string, x: number, y: number, init: Pointing = {}): PointerEvent {
  const event = new PointerEvent(type, {
    bubbles: true,
    cancelable: true,
    clientX: x,
    clientY: y,
    buttons: init.buttons ?? (type === 'pointerup' || type === 'pointercancel' ? 0 : 1),
    button: init.button ?? 0,
    pointerType: init.pointerType ?? 'mouse',
    pointerId: init.pointerId ?? 1,
  });
  on.dispatchEvent(event);
  return event;
}

/** What the browser fires after an up that pressed `on`. */
function click(on: Element): void {
  on.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
}

/** A finger's `touchmove` beside its pointer events: whether the page kept the browser from panning. */
function touchmove(): boolean {
  const event = new Event('touchmove', { bubbles: true, cancelable: true });
  document.body.dispatchEvent(event);
  return event.defaultPrevented;
}

describe('the gesture arena, in the browser', () => {
  let log: string[];
  let clicks: string[];

  beforeEach(() => {
    log = [];
    clicks = [];
    document.body.innerHTML = '';
    installGestureArena();
    installDraggableController();
    installContextController();
  });

  afterEach(() => {
    // What a gesture cleans up after its up (a swallowed click's listener) runs before the next case.
    if (vi.isFakeTimers()) vi.runOnlyPendingTimers();
    vi.useRealTimers();
    resetGestureArena();
    resetDraggableController();
    resetContextController();
  });

  /** detector > draggable surface (horizontal) > button, the detector's box at (50, 100). */
  function draggableUnderADetector(rules: ConstructorParameters<typeof Probe>[1]) {
    const press = button(clicks);
    const surface = document.createElement('div');
    surface.setAttribute('data-eq-drag', 'x');
    surface.setAttribute('data-eq-drag-min', '-200');
    surface.setAttribute('data-eq-drag-max', '200');
    surface.setAttribute('data-eq-drag-rest', '0');
    const released: number[] = [];
    surface.addEventListener('eq-drag-released', (e) => released.push((e as CustomEvent<number>).detail));
    surface.appendChild(press);
    const d = detector([factoryOf(() => new Probe(log, rules))], surface);
    document.body.appendChild(d);
    placeAt(d, 50, 100);
    under(press, surface, d);
    return { press, surface, released };
  }

  it('gives the pointer to a recognizer that accepts: a draggable that had not armed lets it go, and the button under it is not clicked', () => {
    const { press, surface, released } = draggableUnderADetector({ acceptOnMove: true });

    pointer(press, 'pointerdown', 60, 110);
    pointer(document, 'pointermove', 64, 110);
    pointer(document, 'pointermove', 100, 110);
    pointer(document, 'pointerup', 100, 110);
    click(press);

    expect(log).toEqual(['add 10,10', 'move 14,10', 'accept', 'move 50,10', 'up 50,10']);
    expect(surface.style.getPropertyValue('translate')).toBe('');
    expect(released).toEqual([]);
    expect(clicks).toEqual([]);
  });

  it('gives the pointer to a draggable that arms first, and the recognizer never hears its deadline', () => {
    vi.useFakeTimers();
    const { press, surface, released } = draggableUnderADetector({ deadlineMs: 500 });

    pointer(press, 'pointerdown', 60, 110);
    pointer(document, 'pointermove', 100, 110);
    expect(surface.style.getPropertyValue('translate')).toBe('40px');
    vi.advanceTimersByTime(600);
    pointer(document, 'pointerup', 100, 110);

    expect(log).toEqual(['add 10,10', 'move 50,10', 'reject']);
    expect(released).toEqual([40]);
  });

  it('takes the pointer from every member at a pointercancel, the scroll the browser started', () => {
    const { press, surface, released } = draggableUnderADetector({});

    pointer(press, 'pointerdown', 60, 110, { pointerType: 'touch' });
    pointer(document, 'pointermove', 62, 112, { pointerType: 'touch' });
    pointer(document, 'pointercancel', 62, 112, { pointerType: 'touch' });
    pointer(document, 'pointermove', 120, 112, { pointerType: 'touch' });

    expect(log).toEqual(['add 10,10', 'move 12,12', 'cancel 12,12', 'reject']);
    expect(surface.style.getPropertyValue('translate')).toBe('');
    expect(released).toEqual([]);
  });

  it('gives a tap to the button, the first member at the up, and the recognizer hears its rejection', () => {
    const { press } = draggableUnderADetector({ deadlineMs: 500 });

    pointer(press, 'pointerdown', 60, 110);
    pointer(document, 'pointerup', 60, 110);
    click(press);

    expect(log).toEqual(['add 10,10', 'up 10,10', 'reject']);
    expect(clicks).toEqual(['click']);
  });

  it('leaves the press as if the detector were not there for a pointer its recognizer does not allow', () => {
    const press = button(clicks);
    const d = detector([factoryOf(() => new Probe(log, { primaryOnly: true }))], press);
    document.body.appendChild(d);
    placeAt(d, 0, 0);
    under(press, d);

    pointer(press, 'pointerdown', 10, 10, { buttons: 2, button: 2 });
    pointer(document, 'pointerup', 10, 10);
    click(press);

    expect(log).toEqual([]);
    expect(clicks).toEqual(['click']);
  });

  it("takes a button's press with a finger's hold, and the click its release ends in presses nothing", () => {
    vi.useFakeTimers();
    const press = button(clicks);
    const d = detector([factoryOf(() => new Probe(log, { deadlineMs: 500 }))], press);
    document.body.appendChild(d);
    placeAt(d, 0, 0);
    under(press, d);

    pointer(press, 'pointerdown', 10, 10, { pointerType: 'touch' });
    vi.advanceTimersByTime(499);
    expect(log).toEqual(['add 10,10']);
    vi.advanceTimersByTime(201);
    pointer(document, 'pointerup', 10, 10, { pointerType: 'touch' });
    click(press);

    expect(log).toEqual(['add 10,10', 'deadline', 'accept', 'up 10,10']);
    expect(clicks).toEqual([]);
  });

  it('keeps the browser from panning, selecting and opening a menu only once a recognizer has won the finger', () => {
    vi.useFakeTimers();
    const card = document.createElement('div');
    card.textContent = 'A card';
    const d = detector([factoryOf(() => new Probe(log, { deadlineMs: 500 }))], card);
    document.body.appendChild(d);
    placeAt(d, 0, 0);
    under(card, d);

    pointer(card, 'pointerdown', 10, 10, { pointerType: 'touch' });
    expect(touchmove()).toBe(false);
    vi.advanceTimersByTime(500);
    expect(log).toEqual(['add 10,10', 'deadline', 'accept']);
    expect(touchmove()).toBe(true);
    const menu = new MouseEvent('contextmenu', { bubbles: true, cancelable: true });
    card.dispatchEvent(menu);
    expect(menu.defaultPrevented).toBe(true);
    expect(document.documentElement.style.userSelect).toBe('none');

    pointer(document, 'pointermove', 70, 90, { pointerType: 'touch' });
    pointer(document, 'pointerup', 70, 90, { pointerType: 'touch' });
    expect(log.slice(3)).toEqual(['move 70,90', 'up 70,90']);
    expect(touchmove()).toBe(false);
    expect(document.documentElement.style.userSelect).toBe('');
  });

  it("gives a mouse's press to a recognizer alone under it at once, so the drag that follows selects no text", () => {
    const card = document.createElement('div');
    card.textContent = 'A card';
    const d = detector([factoryOf(() => new Probe(log))], card);
    document.body.appendChild(d);
    placeAt(d, 0, 0);
    under(card, d);

    pointer(card, 'pointerdown', 10, 10);
    const select = new Event('selectstart', { bubbles: true, cancelable: true });
    card.dispatchEvent(select);

    expect(log).toEqual(['add 10,10', 'accept']);
    expect(select.defaultPrevented).toBe(true);
    pointer(document, 'pointerup', 10, 10);
    const after = new Event('selectstart', { bubbles: true, cancelable: true });
    card.dispatchEvent(after);
    expect(after.defaultPrevented).toBe(false);
  });

  it("hears the pointer in its detector's box, past the box's edge", () => {
    const child = document.createElement('div');
    child.style.backgroundColor = 'rgb(16, 32, 48)';
    const d = detector([factoryOf(() => new Probe(log))], child);
    document.body.appendChild(d);
    placeAt(d, 50, 100);
    under(child, d);

    pointer(child, 'pointerdown', 60, 106);
    under(document.body);
    pointer(document, 'pointermove', 260, 106);
    pointer(document, 'pointerup', 260, 106);

    expect(log).toEqual(['add 10,6', 'accept', 'move 210,6', 'up 210,6']);
  });

  it("stops a finger's context hold when a recognizer wins the finger first", () => {
    vi.useFakeTimers();
    const asked: string[] = [];
    const inner = document.createElement('div');
    inner.textContent = 'Row';
    const target = document.createElement('div');
    target.setAttribute('data-eq-context', 'on');
    target.addEventListener('eq-context-requested', () => asked.push('menu'));
    target.appendChild(inner);
    const d = detector([factoryOf(() => new Probe(log, { deadlineMs: 300 }))], target);
    document.body.appendChild(d);
    placeAt(d, 0, 0);
    under(inner, target, d);

    pointer(inner, 'pointerdown', 10, 10, { pointerType: 'touch' });
    vi.advanceTimersByTime(700);
    pointer(document, 'pointerup', 10, 10, { pointerType: 'touch' });

    expect(log).toEqual(['add 10,10', 'deadline', 'accept', 'up 10,10']);
    expect(asked).toEqual([]);
  });

  it('opens no arena where no detector is hit: a draggable arms as it always did', () => {
    const surface = document.createElement('div');
    surface.setAttribute('data-eq-drag', 'x');
    surface.setAttribute('data-eq-drag-min', '-200');
    surface.setAttribute('data-eq-drag-max', '200');
    surface.setAttribute('data-eq-drag-rest', '0');
    const elsewhere = detector([factoryOf(() => new Probe(log, { acceptOnMove: true }))], document.createElement('div'));
    document.body.append(surface, elsewhere);
    under(surface);

    pointer(surface, 'pointerdown', 10, 10);
    pointer(document, 'pointermove', 60, 10);

    expect(surface.style.getPropertyValue('translate')).toBe('50px');
    expect(log).toEqual([]);
    pointer(document, 'pointerup', 60, 10);
  });

  it('drops the recognizers of a detector that left the document: its deadline never passes', () => {
    vi.useFakeTimers();
    const card = document.createElement('div');
    card.textContent = 'A card';
    const d = detector([factoryOf(() => new Probe(log, { deadlineMs: 500 }))], card);
    document.body.appendChild(d);
    placeAt(d, 0, 0);
    under(card, d);

    pointer(card, 'pointerdown', 10, 10, { pointerType: 'touch' });
    d.remove();
    pointer(document, 'pointermove', 11, 10, { pointerType: 'touch' });
    vi.advanceTimersByTime(600);
    pointer(document, 'pointerup', 11, 10, { pointerType: 'touch' });

    expect(log).toEqual(['add 10,10']);
  });
});

describe("a table's pan in the arena", () => {
  afterEach(() => resetGestureArena());

  /** The lowered tree as DOM, as the reconciler would write it. */
  function toDom(node: HtmlNode | string): Node {
    if (typeof node === 'string') return document.createTextNode(node);
    const element = document.createElement(node.tag);
    for (const [name, value] of Object.entries(node.attributes ?? {})) element.setAttribute(name, String(value));
    for (const child of (node.children ?? []) as (HtmlNode | string)[])
      if (child !== null && child !== undefined && (typeof child === 'string' || 'tag' in child)) element.appendChild(toDom(child));
    return element;
  }

  /** A table of 28-point rows inside a detector, its box at the window's origin, 900 by 600. */
  function tableUnderADetector(log: string[], rules: ConstructorParameters<typeof Probe>[1]) {
    document.body.innerHTML = '';
    window.matchMedia = ((query: string) => ({ matches: query.includes('reduce'), media: query })) as never;
    installGestureArena();
    const scroll = new TableScroll();
    const node = new TableViewport(new UniformTableSpans(1_000, 28), new UniformTableSpans(40, 96), scroll, { pinnedRows: 1 });
    for (let row = 0; row < 4; row++) node.add(new TableViewCell(row, 0, new Text(`${row}`)));
    const view = toDom(lowerVisualNode(node as never, { textPrimary: photonTheme.textPrimary } as LoweringContext)) as HTMLElement;
    Object.defineProperty(view, 'clientWidth', { value: 900, configurable: true });
    Object.defineProperty(view, 'clientHeight', { value: 600, configurable: true });
    view.getBoundingClientRect = () => ({ top: 0, left: 0, right: 900, bottom: 600, width: 900, height: 600, x: 0, y: 0 }) as DOMRect;
    const d = detector([factoryOf(() => new Probe(log, rules))], view, 'opaque');
    placeAt(d, 0, 0, 900, 600);
    document.body.appendChild(d);
    commitTableViewports();
    under(view, d);
    return { scroll, view };
  }

  it('drops its candidate pan when a recognizer takes the finger first', () => {
    const log: string[] = [];
    const { scroll, view } = tableUnderADetector(log, { acceptOnMove: true });

    pointer(view, 'pointerdown', 400, 400, { pointerType: 'touch', pointerId: 7 });
    pointer(view, 'pointermove', 400, 395, { pointerType: 'touch', pointerId: 7 });
    pointer(view, 'pointermove', 400, 340, { pointerType: 'touch', pointerId: 7 });
    pointer(view, 'pointerup', 400, 340, { pointerType: 'touch', pointerId: 7 });

    expect(log).toEqual(['add 400,400', 'move 400,395', 'accept', 'move 400,340', 'up 400,340']);
    expect(scroll.offset.down).toBe(0);
  });

  it('takes the finger from a recognizer when it pans past its slop first', () => {
    const log: string[] = [];
    const { scroll, view } = tableUnderADetector(log, { deadlineMs: 500 });

    pointer(view, 'pointerdown', 400, 400, { pointerType: 'touch', pointerId: 7 });
    pointer(view, 'pointermove', 400, 395, { pointerType: 'touch', pointerId: 7 });
    pointer(view, 'pointermove', 400, 340, { pointerType: 'touch', pointerId: 7 });
    pointer(view, 'pointerup', 400, 340, { pointerType: 'touch', pointerId: 7 });

    expect(log).toEqual(['add 400,400', 'move 400,395', 'move 400,340', 'reject']);
    expect(scroll.offset.down).toBe(60);
  });
});

describe('a RawGestureDetector through the real lowering', () => {
  afterEach(() => resetGestureArena());

  it('is a div marked as a listener and a detector, whose recognizer outlives the builds of a hold and runs what the latest one wrote', async () => {
    const log: string[] = [];
    let created = 0;
    class Page extends StatefulComponent {
      _build = 0;

      build(): never {
        const build = ++this._build;
        const factory = new GestureRecognizerFactoryWithHandlers(
          () => {
            created++;
            return new Probe(log, { deadlineMs: 120 });
          },
          (probe: unknown) => {
            (probe as Probe).onHeld = () => log.push(`held, as build ${build} wrote`);
          },
        );
        return new RawGestureDetector(
          new Pressable(new Box(new BoxStyle({ width: 100, height: 40 })), () => log.push('pressed')),
          [factory as never],
          { behavior: 'opaque' },
        ) as never;
      }

      rebuild(): void {
        this.setState(() => undefined);
      }
    }
    const nextFrame = () => new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));
    const host = document.createElement('div');
    document.body.appendChild(host);
    const page = new Page();
    page.mount(host);
    const element = host.querySelector('[data-eq-gestures]') as HTMLElement;
    const press = element.querySelector('.eq-pressable') as HTMLElement;
    placeAt(element, 10, 20);
    under(press.firstElementChild ?? press, press, element);

    pointer(press, 'pointerdown', 15, 30, { pointerType: 'touch' });
    for (let i = 0; i < 3; i++) {
      page.rebuild();
      await nextFrame();
    }
    await new Promise((resolve) => setTimeout(resolve, 200));
    pointer(document, 'pointerup', 15, 30, { pointerType: 'touch' });
    click(press);

    expect(element.getAttribute('data-eq-listener')).toBe('opaque');
    expect(created).toBe(1);
    expect(page._build).toBe(4);
    expect(log).toEqual(['add 5,10', 'deadline', 'held, as build 4 wrote', 'accept', 'up 5,10']);
  });
});
