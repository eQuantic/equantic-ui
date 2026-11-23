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
import { SortAnnouncements, type SortDrop } from '../shared/value-types';
import { SdkStrings } from '../shared/components/SdkStrings';
import { SortPlacement } from '../shared/components/SortPlacement';
import { SortWords } from '../shared/components/SortWords';
import { AppThemeExtensions, SortableTheme } from '../shared/theme-slots';
import { num, tokenValue } from '../shared/css-values';
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
    private readonly guard: ((drop: SortDrop) => boolean) | null = null,
    // The words a list is given, the SDK's as `UI.Sortable` gives them unless a test gives its own (#1564).
    private readonly words: SortAnnouncements | null = SdkStrings.sorting,
    // The done column's own words, when a test gives it words of its own.
    private readonly doneWords: SortAnnouncements | null | undefined = undefined,
    // The todo column's own look of a carried card (#1602).
    private readonly style: SortableTheme | null = null,
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
      const words = id === 'done' && this.doneWords !== undefined ? this.doneWords : this.words;
      return new Sortable(list as never, id, (drop: SortDrop) => dropped.push(drop), words, id === 'todo' ? this.style : null, {
        group: this.group,
        nests: this.nests,
        label: id === 'todo' ? 'To do' : 'Done',
        onHovered: (drop: SortDrop | null) => hovered.push(drop),
        accepts: this.guard ?? (this.refuse ? (drop: SortDrop) => !drop.item.startsWith(this.refuse!) : null),
      });
    };
    row.add(column('todo', ['a', 'b', 'c', 'locked-1']) as never);
    row.add(column('done', this.doneCards) as never);
    row.add(
      new Sortable(new Column(0) as never, 'other', (drop: SortDrop) => dropped.push(drop), this.words, null, {
        group: 'else',
      }) as never,
    );
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

function mount(board: StatefulComponent): HTMLElement {
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

describe('SortPlacement.keyStep, the twin both targets run (#1533)', () => {
  it('steps through the places inside the items of a list that nests, kept inside it', () => {
    // Two other items (a, b): before a, inside a, before b, inside b, after b.
    expect(SortPlacement.keyStep(0, -1, 1, 2, true)).toEqual([0, 0]);
    expect(SortPlacement.keyStep(0, 0, 1, 2, true)).toEqual([1, -1]);
    expect(SortPlacement.keyStep(1, 1, 1, 2, true)).toEqual([2, -1]);
    expect(SortPlacement.keyStep(2, -1, 1, 2, true)).toEqual([2, -1]);
    expect(SortPlacement.keyStep(2, -1, -1, 2, true)).toEqual([1, 1]);
    expect(SortPlacement.keyStep(0, -1, -1, 2, true)).toEqual([0, -1]);
  });

  it('moves one index in a list that does not nest', () => {
    expect(SortPlacement.keyStep(0, -1, 1, 2, false)).toEqual([1, -1]);
    expect(SortPlacement.keyStep(2, -1, 1, 2, false)).toEqual([2, -1]);
  });
});

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

  it('leaves no selection after a drag of an item made of text (#1598)', () => {
    const container = mount(new Board());
    const from = item(container, 'a');
    const box = boxes.get(from)!;
    pointer(from, 'pointerdown', box.left + 10, box.top + 20);
    // The selection a mouse press on the item's words starts, which jsdom does not make itself.
    document.getSelection()!.selectAllChildren(from);
    const path: [number, number][] = [
      [20, 30],
      [20, 70],
      [20, 90],
    ];
    for (const [x, y] of path) pointer(document, 'pointermove', x, y);
    expect(document.getSelection()!.toString()).toBe('');
    pointer(document, 'pointerup', 20, 90);
    expect(dropped).toHaveLength(1);
    expect(document.getSelection()!.toString()).toBe('');
    expect(document.documentElement.style.userSelect).toBe('');
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

  describe('the look of a carried card (#1602)', () => {
    afterEach(() => setPhotonTheme(photonTheme));

    /** The theme's shadow for a level, in the spelling the controller writes. */
    const shadowFor = (level: number): string => {
      const spec = photonTheme.elevation(level);
      return `0 ${num(spec.offsetY)}px ${num(spec.blur)}px ${num(spec.spread)}px ${tokenValue(spec.color)}`;
    };

    /** Carries card a fast to the right, leaving the pointer down, and answers the copy. */
    const carryFast = (container: HTMLElement): HTMLElement => {
      pointer(item(container, 'a'), 'pointerdown', 10, 20);
      pointer(document, 'pointermove', 20, 20);
      pointer(document, 'pointermove', 120, 20);
      return document.body.lastElementChild as HTMLElement;
    };

    it("takes the defaults where neither the theme nor the list says, the theme's level 4 shadow included", () => {
      const container = mount(new Board());
      const copy = carryFast(container);
      expect(item(container, 'a').style.opacity).toBe('0.4');
      expect(copy.style.opacity).toBe('0.92');
      expect(copy.getAttribute('style')).toContain(`box-shadow: ${shadowFor(4)}`);
      pointer(document, 'pointerup', 120, 20);
    });

    it("is the theme's, with the list's own laid over it member by member", () => {
      setPhotonTheme(
        // Every slot before the sortable one left as the theme has it.
        AppThemeExtensions.with(photonTheme, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, new SortableTheme({
          liftedOpacity: 0.25,
          copyOpacity: 0.7,
          copyElevation: 5,
          maxLean: 6,
        })),
      );
      const container = mount(new Board(false, null, 'board', [], null, SdkStrings.sorting, undefined,
        new SortableTheme({ copyOpacity: 0.5 })));
      const copy = carryFast(container);
      expect(item(container, 'a').style.opacity).toBe('0.25');
      expect(copy.style.opacity).toBe('0.5');
      expect(copy.getAttribute('style')).toContain(`box-shadow: ${shadowFor(5)}`);
      // Fast to the right leans the copy back, no further than the theme's most.
      expect(copy.style.getPropertyValue('rotate')).toBe('-6deg');
      pointer(document, 'pointerup', 120, 20);
    });

    it("takes the theme's member where a list's element says no number", () => {
      const container = mount(new Board(false, null, 'board', [], null, SdkStrings.sorting, undefined,
        new SortableTheme({ liftedOpacity: 0.25 })));
      container.querySelector('[data-eq-sort-id="todo"]')!.setAttribute('data-eq-sort-look-lifted', ' ');
      carryFast(container);
      expect(item(container, 'a').style.opacity).toBe('0.4');
      pointer(document, 'pointerup', 120, 20);
    });

    it("keeps the copy upright where the list's look leans no further", () => {
      const container = mount(new Board(false, null, 'board', [], null, SdkStrings.sorting, undefined,
        new SortableTheme({ maxLean: 0 })));
      const copy = carryFast(container);
      expect(copy.style.getPropertyValue('rotate')).toBe('0deg');
      pointer(document, 'pointerup', 120, 20);
    });
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
    expect(SortPlacement.tilt(10, 10, SortPlacement.tiltMax)).toBe(-8);
    expect(SortPlacement.tilt(-40, 10, SortPlacement.tiltMax)).toBe(12);
    expect(SortPlacement.tilt(0, 16, SortPlacement.tiltMax)).toBe(0);
    expect(SortPlacement.tilt(5, 0, SortPlacement.tiltMax)).toBeNull();

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

    it('steps through the places inside the items in a list that nests, and says them (#1533)', () => {
      const container = mount(new Board(true));
      item(container, 'c').focus();
      press(' ');
      expect(said()).toBe('Picked up c, position 3 of 4 in To do.');
      // Before c's own place come, going up: inside b, then before b.
      press('ArrowUp');
      expect(said()).toBe('c is inside b.');
      expect(last(hovered)?.into).toBe('b');
      press('ArrowUp');
      expect(said()).toBe('c is at position 2 of 4 in To do.');
      expect(last(hovered)?.into ?? null).toBeNull();
      press('ArrowDown');
      press(' ');
      expect([dropped[0].item, dropped[0].index, dropped[0].into]).toEqual(['c', 1, 'b']);
      expect(said()).toBe('Dropped c inside b.');
    });

    it('passes over a place the list refuses, as a pointer does (#1533)', () => {
      const container = mount(new Board(false, null, 'board', [], (drop: SortDrop) => drop.index !== 1));
      item(container, 'c').focus();
      press(' ');
      press('ArrowUp');
      expect(said()).toBe('c is at position 1 of 4 in To do.');
      expect(last(hovered)?.index).toBe(0);
      // At the top, the place it holds is said again rather than refused.
      press('ArrowUp');
      expect(said()).toBe('c is at position 1 of 4 in To do.');
    });

    it("starts from a composite's active item, and leaves the focus on the composite (#1533)", () => {
      vi.stubGlobal('requestAnimationFrame', (frame: FrameRequestCallback) => {
        frame(0);
        return 0;
      });
      const container = mount(new Board());
      const list = container.querySelector('[data-eq-sort-id="todo"]') as HTMLElement;
      // A composite's one stop, naming its active item, as a tree's host does.
      list.setAttribute('tabindex', '0');
      item(container, 'b').id = 'todo-b';
      list.setAttribute('aria-activedescendant', 'todo-b');
      list.focus();

      press(' ');
      expect(said()).toBe('Picked up b, position 2 of 4 in To do.');
      press('ArrowDown');
      press(' ');
      expect([dropped[0].item, dropped[0].index]).toEqual(['b', 2]);
      expect(document.activeElement).toBe(list);
    });

    it("says each step, and describes the item, in the list's own words when the app gives them (#1564)", () => {
      const words = new SortAnnouncements(
        'Use Space.',
        'Took {0} ({1}/{2}, {3}).',
        '{0} now {1}/{2}.',
        '{0} in {1}.',
        'Not there.',
        'Left {0} at {1}.',
        'Left {0} in {1}.',
        'Back.',
      );
      const doneWords = new SortAnnouncements('Other.', 'Other.', 'Other.', 'Other.', 'Other.', 'Other.', 'Other.', 'Other.');
      const container = mount(new Board(false, null, 'board', ['d'], null, words, doneWords));
      const a = item(container, 'a');
      a.focus();
      const described = document.getElementById(a.getAttribute('aria-describedby')!.split(' ').pop()!);
      expect(described?.textContent).toBe('Use Space.');
      press(' ');
      expect(said()).toBe('Took a (1/4, To do).');
      press('ArrowDown');
      expect(said()).toBe('a now 2/4.');
      // Another list of the group: the carry still speaks in the words of the list it started from.
      press('ArrowRight');
      expect(said()).toBe('a now 2/2.');
      press(' ');
      expect(said()).toBe('Left a at 2.');
      a.focus();
      press(' ');
      press('Escape');
      expect(said()).toBe('Back.');
    });

    it('says nothing, and describes nothing, for a list given no words (#1564)', () => {
      const container = mount(new Board(false, null, 'board', [], null, null));
      const a = item(container, 'a');
      a.focus();
      expect(a.getAttribute('aria-describedby') ?? '').not.toContain('eq-sort-instructions');
      press(' ');
      press('ArrowDown');
      press(' ');
      expect(dropped[0].index).toBe(1);
      expect(said()).toBe('');
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

/** The holes of a step's words, filled by the rule both targets run (C# twin: `SortWords.Fill`, the same cases in
 * `SortWordsTests`). */
describe('SortWords.fill (#1564)', () => {
  it.each([
    ['Picked up {0}, position {1} of {2} in {3}.', ['a', '1', '3', 'To do'], 'Picked up a, position 1 of 3 in To do.'],
    ['{1} before {0}', ['a', 'b'], 'b before a'],
    ['Left out: {0}.', ['a', 'b', 'c'], 'Left out: a.'],
    ['Past the holes: {4}.', ['a'], 'Past the holes: {4}.'],
    ['A stray { brace } and {x}.', ['a'], 'A stray { brace } and {x}.'],
    ['Unclosed {0', ['a'], 'Unclosed {0'],
    ['{0} is {1}', ['{1}.txt', 'here'], '{1}.txt is here'],
    ['{}{0}{10}', ['a'], '{}a{10}'],
  ])('fills %j', (text, holes, expected) => {
    expect(SortWords.fill(text, holes)).toBe(expected);
  });
});

/**
 * A list whose items have depths (#1594), an outline of one group with a second list beside it: src (0), App.cs (1),
 * Program.cs (1), docs (0) and README.md (0), 40 tall from y 0, one depth 16 across; and `next`, holding z. The C# twin
 * of the arithmetic is `SortPlacementDepthTests`, and Photon carries the same outline in `SortableHostTests`.
 */
class Outline extends StatefulComponent {
  constructor(private readonly guard: ((drop: SortDrop) => boolean) | null = null) {
    super();
  }

  build(): Row {
    const row = new Row(0);
    const list = new Column(0);
    for (const [name, depth] of [['src', 0], ['App.cs', 1], ['Program.cs', 1], ['docs', 0], ['README.md', 0]] as const)
      list.add(new SortableItem(new Text(name) as never, name, { depth }) as never);
    row.add(
      new Sortable(list as never, 'files', (drop: SortDrop) => dropped.push(drop), SdkStrings.sorting, null, {
        group: 'outline',
        nests: true,
        indent: 16,
        label: 'Files',
        onHovered: (drop: SortDrop | null) => hovered.push(drop),
        accepts: this.guard,
      }) as never,
    );
    const next = new Column(0);
    next.add(new SortableItem(new Text('z') as never, 'z') as never);
    row.add(
      new Sortable(next as never, 'next', (drop: SortDrop) => dropped.push(drop), SdkStrings.sorting, null, {
        group: 'outline',
        label: 'Next',
        onHovered: (drop: SortDrop | null) => hovered.push(drop),
      }) as never,
    );
    return row as never;
  }
}

describe('a list whose items have depths (#1594)', () => {
  beforeEach(() => {
    vi.stubGlobal('requestAnimationFrame', () => 0);
    vi.stubGlobal('cancelAnimationFrame', () => undefined);
  });

  afterEach(async () => {
    await new Promise((resolve) => setTimeout(resolve, 60));
    resetSortableController();
    vi.unstubAllGlobals();
    document.body.innerHTML = '';
    boxes.clear();
    dropped.length = 0;
    hovered.length = 0;
  });

  const place = (drop: SortDrop | undefined) => [drop?.index, drop?.into, drop?.depth];

  it('writes the indent on the list and each depth on its item, nothing for the top', () => {
    const container = mount(new Outline());
    expect(container.querySelector('[data-eq-sort-id="files"]')?.getAttribute('data-eq-sort-indent')).toBe('16');
    expect(item(container, 'App.cs').getAttribute('data-eq-sort-depth')).toBe('1');
    expect(item(container, 'docs').hasAttribute('data-eq-sort-depth')).toBe(false);
    expect(container.querySelector('[data-eq-sort-id="next"]')?.hasAttribute('data-eq-sort-indent')).toBe(false);
  });

  it("keeps the item's own depth until the pointer travels an indent across, then takes it deeper", () => {
    const container = mount(new Outline());
    // README.md (160..200, pressed at x 10) to docs' top quarter, before docs: after Program.cs (1), before docs (0).
    drag(container, 'README.md', [[10, 170], [10, 125]], false);
    expect(place(last(hovered)!)).toEqual([3, null, 0]);
    pointer(document, 'pointermove', 26, 125);
    expect(place(last(hovered)!)).toEqual([3, null, 1]);
    pointer(document, 'pointermove', 42, 125);
    expect(place(last(hovered)!)).toEqual([3, null, 2]);
    // As deep as one past Program.cs, no further.
    pointer(document, 'pointermove', 90, 125);
    expect(place(last(hovered)!)).toEqual([3, null, 2]);
    pointer(document, 'pointermove', 26, 125);
    pointer(document, 'pointerup', 26, 125);
    expect(place(dropped[0])).toEqual([3, null, 1]);
  });

  it('gives a refused depth way to the nearest the place allows, shallower first', () => {
    const container = mount(new Outline((drop) => drop.depth !== 1));
    drag(container, 'README.md', [[10, 170], [26, 125]]);
    expect(place(dropped[0])).toEqual([3, null, 0]);
  });

  it('carries the subtree with the item: no place among its own children', () => {
    const container = mount(new Outline());
    // src (0..40) over Program.cs (80..120): its children are no places, so this is src's own place, before docs.
    drag(container, 'src', [[10, 30], [10, 90]]);
    expect(hovered.filter(Boolean).every((drop) => drop!.index === 0)).toBe(true);
    expect(place(dropped[0])).toEqual([0, null, 0]);
  });

  it('reports no depth for a place inside an item', () => {
    const container = mount(new Outline());
    drag(container, 'README.md', [[10, 170], [26, 140]]);
    expect(place(dropped[0])).toEqual([3, 'docs', null]);
  });

  describe('by the keyboard', () => {
    const press = (key: string): void => {
      (document.activeElement ?? document.body).dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }));
    };
    const said = (): string => document.querySelector('[data-eq-sort-announcer]')?.textContent ?? '';

    it('steps the depth with the cross arrows and says the level, among its siblings under their holder', () => {
      const container = mount(new Outline());
      item(container, 'README.md').focus();
      press(' ');
      expect(said()).toBe('Picked up README.md, position 3 of 3 in Files.');
      // After docs, inside docs, before docs.
      press('ArrowUp');
      press('ArrowUp');
      expect(said()).toBe('README.md is at position 2 of 3 in Files.');
      press('ArrowRight');
      expect(said()).toBe('README.md is at position 3 of 3 in src.');
      expect(place(last(hovered)!)).toEqual([3, null, 1]);
      press('ArrowRight');
      expect(said()).toBe('README.md is at position 1 of 1 in Program.cs.');
      expect(place(last(hovered)!)).toEqual([3, null, 2]);
      press('ArrowLeft');
      press('Enter');
      expect(place(dropped[0])).toEqual([3, null, 1]);
      expect(said()).toBe('Dropped README.md at position 3 of 3 in src.');
    });

    it('keeps the depth it chose as the item moves along, within each place', () => {
      const container = mount(new Outline());
      item(container, 'README.md').focus();
      press(' ');
      press('ArrowUp');
      press('ArrowUp');
      press('ArrowRight');
      press('ArrowRight');
      // Up into Program.cs, then before it, where the place allows depths 1 and 2: the 2 it chose, not its own 0 kept
      // in range, which would be 1.
      press('ArrowUp');
      press('ArrowUp');
      expect(place(last(hovered)!)).toEqual([2, null, 2]);
    });

    it('passes over a refused depth, and says so when none is left that way', () => {
      const container = mount(new Outline((drop) => drop.depth !== 1));
      item(container, 'README.md').focus();
      press(' ');
      press('ArrowUp');
      press('ArrowUp');
      press('ArrowRight');
      expect(place(last(hovered)!)).toEqual([3, null, 2]);
      press('ArrowLeft');
      expect(place(last(hovered)!)).toEqual([3, null, 0]);
      press('Escape');

      const refusing = mount(new Outline((drop) => drop.depth !== 2));
      item(refusing, 'README.md').focus();
      press(' ');
      press('ArrowUp');
      press('ArrowUp');
      press('ArrowRight');
      press('ArrowRight');
      expect(said()).toBe('README.md cannot go there.');
    });

    it('moves to the next list of its group once the place offers no other depth', () => {
      const container = mount(new Outline());
      item(container, 'src').focus();
      press(' ');
      // src's own place, first: before docs, at the top only.
      press('ArrowRight');
      expect(last(hovered)?.to).toBe('next');
    });
  });
});
