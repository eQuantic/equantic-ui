/**
 * Lists a pointer sorts (#1535), driven as a mouse and a finger drive them: through the lowering of real
 * `Sortable` and `SortableItem` nodes, so the controller's reports reach the callbacks a component hands
 * them. jsdom lays nothing out, so each item and list is given the box a board would draw, and
 * `elementsFromPoint` answers from those boxes.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { StatefulComponent } from '../core/component';
import { photonTheme } from '../shared/design-system.generated';
import { setPhotonTheme } from '../shared/photon-context';
import { Column, Row, Sortable, SortableItem, Text } from '../shared/vocabulary';
import type { SortDrop } from '../shared/value-types';
import { SortPlacement } from '../shared/components/SortPlacement';
import { resetSortableController } from './sortable';

setPhotonTheme(photonTheme);

const dropped: SortDrop[] = [];
const hovered: (SortDrop | null)[] = [];
let pressed: string[] = [];

/** Two columns of one group, 200 wide, cards 40 tall from y 0; and a third list of another group. */
class Board extends StatefulComponent {
  constructor(
    private readonly nests = false,
    private readonly refuse: string | null = null,
    private readonly group = 'board',
    private readonly doneCards: string[] = [],
  ) {
    super();
  }

  build(): Row {
    const row = new Row(0);
    const column = (id: string, cards: string[]): Sortable => {
      const list = new Column(0);
      for (const card of cards) {
        const item = new SortableItem(new Text(card) as never, card);
        list.add(item as never);
      }
      return new Sortable(list as never, id, (drop: SortDrop) => dropped.push(drop), {
        group: this.group,
        nests: this.nests,
        label: id === 'todo' ? 'To do' : 'Done',
        onHovered: (drop: SortDrop | null) => hovered.push(drop),
        accepts: this.refuse ? (drop: SortDrop) => !drop.item.startsWith(this.refuse!) : null,
      });
    };
    row.add(column('todo', ['a', 'b', 'c', 'locked-1']) as never);
    row.add(column('done', this.doneCards) as never);
    row.add(new Sortable(new Column(0) as never, 'other', (drop: SortDrop) => dropped.push(drop), { group: 'else' }) as never);
    return row as never;
  }
}

const last = <T,>(values: T[]): T | undefined => values[values.length - 1];

type Box = { left: number; top: number; width: number; height: number };
const boxes = new Map<Element, Box>();

function give(element: Element, box: Box): void {
  boxes.set(element, box);
  (element as HTMLElement).getBoundingClientRect = () =>
    ({ ...box, right: box.left + box.width, bottom: box.top + box.height, x: box.left, y: box.top, toJSON: () => box }) as DOMRect;
}

function mount(board: Board): HTMLElement {
  const container = document.createElement('div');
  document.body.appendChild(container);
  board.mount(container);
  const lists = Array.from(container.querySelectorAll('[data-eq-sort]'));
  lists.forEach((list, column) => {
    give(list, { left: column * 200, top: 0, width: 200, height: 400 });
    Array.from(list.querySelectorAll('[data-eq-sort-item]')).forEach((item, row) =>
      give(item, { left: column * 200, top: row * 40, width: 200, height: 40 }));
  });
  // Innermost first, as the browser answers.
  (document as unknown as { elementsFromPoint: (x: number, y: number) => Element[] }).elementsFromPoint = (x, y) =>
    Array.from(boxes.entries())
      .filter(([, b]) => x >= b.left && x < b.left + b.width && y >= b.top && y < b.top + b.height)
      .map(([element]) => element)
      .sort((p, q) => (p.contains(q) ? 1 : q.contains(p) ? -1 : 0));
  return container;
}

const item = (container: HTMLElement, id: string) =>
  container.querySelector(`[data-eq-sort-item="${id}"]`) as HTMLElement;

function pointer(target: EventTarget, type: string, x: number, y: number, pointerType = 'mouse'): PointerEvent {
  const event = new PointerEvent(type, { clientX: x, clientY: y, bubbles: true, cancelable: true, pointerType, button: 0 });
  target.dispatchEvent(event);
  return event;
}

function drag(container: HTMLElement, id: string, path: [number, number][], release = true): void {
  const from = item(container, id);
  const box = boxes.get(from)!;
  pointer(from, 'pointerdown', box.left + 10, box.top + 20);
  for (const [x, y] of path) pointer(document, 'pointermove', x, y);
  if (release) pointer(document, 'pointerup', path[path.length - 1][0], path[path.length - 1][1]);
}

describe('lists a pointer sorts', () => {
  beforeEach(() => {
    vi.stubGlobal('requestAnimationFrame', () => 0);
    vi.stubGlobal('cancelAnimationFrame', () => undefined);
  });

  afterEach(async () => {
    // A drag's swallowed click is waited for for 50 ms: the next case's first click is not its.
    await new Promise((resolve) => setTimeout(resolve, 60));
    resetSortableController();
    vi.unstubAllGlobals();
    document.body.innerHTML = '';
    boxes.clear();
    dropped.length = 0;
    hovered.length = 0;
    pressed = [];
  });

  it('moves an item down its own list, counting the list without it', () => {
    const container = mount(new Board());
    // From a (0..40) to below b's middle (60) and above c's (100).
    drag(container, 'a', [[20, 30], [20, 70], [20, 90]]);
    expect(dropped).toHaveLength(1);
    expect([dropped[0].item, dropped[0].from, dropped[0].to, dropped[0].index]).toEqual(['a', 'todo', 'todo', 1]);
    expect(item(container, 'a').style.opacity).toBe('');
    expect(document.querySelectorAll('[aria-hidden="true"][style*="fixed"]')).toHaveLength(0);
  });

  it('moves an item to an empty list of its group, and offers no place to a list of another', () => {
    const container = mount(new Board());
    drag(container, 'b', [[220, 60], [450, 30]]);
    expect(dropped).toHaveLength(0);
    drag(container, 'b', [[220, 60], [250, 30]]);
    expect([dropped[0].item, dropped[0].from, dropped[0].to, dropped[0].index]).toEqual(['b', 'todo', 'done', 0]);
  });

  it('reports the place while carrying, then null, and dims the item under a copy that follows', () => {
    const container = mount(new Board());
    const from = item(container, 'c');
    pointer(from, 'pointerdown', 10, 100);
    pointer(document, 'pointermove', 20, 100);
    expect(from.style.opacity).toBe('0.4');
    const copy = document.body.lastElementChild as HTMLElement;
    expect(copy.style.position).toBe('fixed');
    expect(copy.getAttribute('aria-hidden')).toBe('true');
    expect(copy.querySelector('[data-eq-sort-item]')).toBeNull();
    pointer(document, 'pointermove', 20, 10);
    expect(last(hovered)?.index).toBe(0);
    pointer(document, 'pointerup', 20, 10);
    expect(last(hovered)).toBeNull();
    expect(dropped[0].index).toBe(0);
  });

  it('asks the guard, and drops nothing on a refused place', () => {
    const container = mount(new Board(false, 'locked'));
    drag(container, 'locked-1', [[220, 130], [250, 20]]);
    expect(dropped).toHaveLength(0);
    expect(hovered.filter((drop) => drop !== null)).toHaveLength(0);
  });

  it('nests an item into another over its middle half', () => {
    const container = mount(new Board(true));
    // Over b's middle (40..80, middle half 50..70).
    drag(container, 'c', [[20, 90], [20, 60]]);
    expect(dropped[0].into).toBe('b');
    expect(dropped[0].index).toBe(1);
  });

  it('takes only its own items when it has no group', () => {
    const container = mount(new Board(false, null, ''));
    drag(container, 'b', [[220, 60], [250, 30]]);
    expect(dropped).toHaveLength(0);
    drag(container, 'b', [[20, 70], [20, 130]]);
    expect([dropped[0].from, dropped[0].to]).toEqual(['todo', 'todo']);
  });

  it("puts the item back on Escape, and the Escape is the drag's alone", () => {
    const container = mount(new Board());
    let page = 0;
    const listener = (): void => {
      page++;
    };
    document.addEventListener('keydown', listener);
    drag(container, 'a', [[20, 30], [250, 20]], false);
    expect(last(hovered)?.to).toBe('done');
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }));
    pointer(document, 'pointerup', 250, 20);
    expect(dropped).toHaveLength(0);
    expect(last(hovered)).toBeNull();
    expect(item(container, 'a').style.opacity).toBe('');
    document.removeEventListener('keydown', listener);
    expect(page).toBe(0);
  });

  it('leaves a press that does not travel to the item, and swallows the click a drag ends in', () => {
    const container = mount(new Board());
    const a = item(container, 'a');
    a.addEventListener('click', () => pressed.push('a'));
    pointer(a, 'pointerdown', 10, 20);
    pointer(document, 'pointermove', 12, 21);
    pointer(document, 'pointerup', 12, 21);
    a.click();
    expect(pressed).toEqual(['a']);
    drag(container, 'a', [[20, 30], [20, 90]]);
    a.click();
    expect(pressed).toEqual(['a']);
  });

  it('waits for a finger to hold still, and lets a swipe scroll', () => {
    vi.useFakeTimers();
    try {
      const container = mount(new Board());
      const b = item(container, 'b');
      pointer(b, 'pointerdown', 10, 60, 'touch');
      pointer(document, 'pointermove', 10, 90, 'touch');
      vi.advanceTimersByTime(300);
      expect(b.style.opacity).toBe('');

      pointer(b, 'pointerdown', 10, 60, 'touch');
      vi.advanceTimersByTime(300);
      expect(b.style.opacity).toBe('0.4');
      pointer(document, 'pointermove', 250, 20, 'touch');
      pointer(document, 'pointerup', 250, 20, 'touch');
      expect(dropped[0].to).toBe('done');
    } finally {
      vi.useRealTimers();
    }
  });

  it("leans the copy against the pointer's speed across, up to 12 degrees, as the Space's board does", () => {
    expect(SortPlacement.tilt(10, 10)).toBe(-8);
    expect(SortPlacement.tilt(-40, 10)).toBe(12);
    expect(SortPlacement.tilt(0, 16)).toBe(0);
    expect(SortPlacement.tilt(5, 0)).toBeNull();

    let now = 1000;
    const clock = vi.spyOn(performance, 'now').mockImplementation(() => now);
    try {
      const container = mount(new Board());
      const a = item(container, 'a');
      pointer(a, 'pointerdown', 10, 20);
      now += 16;
      pointer(document, 'pointermove', 20, 20);
      const copy = document.body.lastElementChild as HTMLElement;
      now += 10;
      pointer(document, 'pointermove', 30, 20);
      expect(copy.style.getPropertyValue('rotate')).toBe('-8deg');
      pointer(document, 'pointerup', 30, 20);
    } finally {
      clock.mockRestore();
    }
  });

  describe('by the keyboard', () => {
    const press = (key: string): KeyboardEvent => {
      const event = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true });
      (document.activeElement ?? document.body).dispatchEvent(event);
      return event;
    };
    const said = (): string => document.querySelector('[data-eq-sort-announcer]')?.textContent ?? '';

    it('picks a card up with Space, moves it down and across, and drops it, saying each step', () => {
      const container = mount(new Board(false, null, 'board', ['d']));
      const a = item(container, 'a');
      expect(a.getAttribute('tabindex')).toBe('0');
      a.focus();
      expect(a.getAttribute('aria-describedby')).toContain('eq-sort-instructions');

      const lift = press(' ');
      expect(lift.defaultPrevented).toBe(true);
      expect(said()).toBe('Picked up a, position 1 of 4 in To do.');
      press('ArrowDown');
      expect(said()).toBe('a is at position 2 of 4 in To do.');
      expect(last(hovered)?.index).toBe(1);
      press('ArrowRight');
      expect(said()).toBe('a is at position 2 of 2 in Done.');
      press(' ');
      expect(dropped).toHaveLength(1);
      expect([dropped[0].item, dropped[0].from, dropped[0].to, dropped[0].index]).toEqual(['a', 'todo', 'done', 1]);
      expect(said()).toBe('Dropped a at position 2 of 2 in Done.');
      expect(a.style.opacity).toBe('');
    });

    it('puts the card back on Escape, and on Tab', () => {
      const container = mount(new Board());
      item(container, 'a').focus();
      press(' ');
      press('ArrowDown');
      press('Escape');
      expect(dropped).toHaveLength(0);
      expect(last(hovered)).toBeNull();
      expect(said()).toBe('Put a back where it was.');

      press(' ');
      const tab = press('Tab');
      expect(tab.defaultPrevented).toBe(false);
      expect(dropped).toHaveLength(0);
    });

    it('takes no refused place, and says so', () => {
      const container = mount(new Board(false, 'locked'));
      item(container, 'locked-1').focus();
      press(' ');
      press('ArrowRight');
      expect(said()).toBe('locked-1 cannot go there.');
      press('Enter');
      expect(dropped[0].to).toBe('todo');
    });

    it('leaves Space in a field, and a chord, to the page', () => {
      const container = mount(new Board());
      item(container, 'a').focus();
      const chord = new KeyboardEvent('keydown', { key: ' ', shiftKey: true, bubbles: true, cancelable: true });
      item(container, 'a').dispatchEvent(chord);
      expect(chord.defaultPrevented).toBe(false);
      expect(item(container, 'a').style.opacity).toBe('');
    });
  });
});
