/**
 * The web half of the C# `Sortable` and `SortableItem` (#1535): items put in order by a pointer, inside
 * their list and across the lists of a group.
 *
 * The pointer's lifecycle is the gesture core's (`pointer-gesture.ts`), the one `Draggable` and
 * `DragDismiss` run on: this module only says what a sort MEANS. A mouse picks an item up past a few
 * pixels, so a press that does not travel still presses it; a finger after holding it still, so a swipe
 * still scrolls the list. A copy of the item follows the pointer and the item is dimmed in place. Over a
 * list of the same group the place is computed from the items' boxes, offered to the list's guard, and
 * reported while it changes; on release the list it lands in reports the drop. Escape puts it back.
 *
 * The lists and the items are found by the marks the lowering writes (`data-eq-sort*`), and every report
 * is a custom event on the list's element, which the lowering turns into the C# callback. The controller
 * never applies a drop: the list is the app's, and the next build shows where the item went.
 */
import { SortPlacement } from '../shared/components/SortPlacement';
import { SortWords } from '../shared/components/SortWords';
import { SortAnnouncements, SortDrop } from '../shared/value-types';
import { SortableTheme } from '../shared/theme-slots';
import { getPhotonTheme } from '../shared/photon-context';
import { num, tokenValue } from '../shared/css-values';
import { activeItemOf } from './active-item';
import { followPointer, pointerDownDelegate } from './pointer-gesture';
import { holdShortcuts } from './shortcuts';

const delegate = pointerDownDelegate(onPointerDown);
let keyboardInstalled = false;

export function installSortableController(): void {
  delegate.install();
  if (keyboardInstalled || typeof document === 'undefined') return;
  keyboardInstalled = true;
  document.addEventListener('keydown', onKeyDown);
  document.addEventListener('focusin', describe);
}

export function resetSortableController(): void {
  delegate.reset();
  if (typeof document === 'undefined') return;
  keyboardInstalled = false;
  document.removeEventListener('keydown', onKeyDown);
  document.removeEventListener('focusin', describe);
  carrying?.cancel();
  announcer?.remove();
  announcer = null;
  instructions?.remove();
  instructions = null;
}

/** The list an item, or a list nested in an item, belongs to: the nearest one ABOVE it. */
function listOf(element: Element): HTMLElement | null {
  return (element.parentElement?.closest('[data-eq-sort]') as HTMLElement | null) ?? null;
}

/** A list's own items, the ones of a list nested inside them left out. */
function itemsOf(list: HTMLElement): HTMLElement[] {
  return Array.from(list.querySelectorAll<HTMLElement>('[data-eq-sort-item]')).filter(
    (item) => listOf(item) === list,
  );
}

/** What can hold a field the item's own pointer belongs to. */
const FIELD = 'input, textarea, select, [contenteditable="true"], [contenteditable=""]';

function onPointerDown(down: PointerEvent): void {
  if (down.pointerType === 'mouse' && down.button !== 0) return;
  const target = down.target as Element | null;
  const item = target?.closest?.('[data-eq-sort-item]') as HTMLElement | null;
  if (!item || item.hasAttribute('data-eq-sort-off') || target?.closest(FIELD)) return;
  const source = listOf(item);
  if (!source || source.hasAttribute('data-eq-sort-off')) return;

  const group = source.getAttribute('data-eq-sort') ?? '';
  const id = item.getAttribute('data-eq-sort-item') ?? '';
  const from = source.getAttribute('data-eq-sort-id') ?? '';
  const host = document;
  let copy: HTMLElement | null = null;
  let landing: { list: HTMLElement; drop: SortDrop } | null = null;
  let pointer = { x: down.clientX, y: down.clientY };
  let frame = 0;
  let movedAt = performance.now();
  const leans = !prefersReducedMotion();
  // One carry, one look: the item is carried as the list it was picked up from says, over every list of the group.
  const look = lookOf(source);

  const report = (list: HTMLElement, drop: SortDrop | null): void => {
    list.dispatchEvent(new CustomEvent('eq-sort-hovered', { detail: drop }));
  };

  // The guard is asked once per place, not once per move: a tree's walks its subtree.
  let asked: { list: HTMLElement; drop: SortDrop; accepted: boolean } | null = null;
  const scrollers = new Map<HTMLElement, HTMLElement | null>();

  /** Where the item would land under the pointer, offered to the list's guard, and reported when it moves. */
  const place = (): void => {
    const list = listUnder(pointer.x, pointer.y, group, source, item);
    const drop = list ? dropIn(list, id, from, pointer.x, pointer.y, item) : null;
    if (list && drop && !(asked && asked.list === list && same(asked.drop, drop)))
      asked = { list, drop, accepted: accepts(list, drop) };
    const accepted = list !== null && drop !== null && asked !== null && asked.accepted;
    const next = accepted ? { list: list!, drop: drop! } : null;
    if (landing && (!next || next.list !== landing.list)) report(landing.list, null);
    if (next && (!landing || landing.list !== next.list || !same(landing.drop, next.drop))) report(next.list, next.drop);
    landing = next;
  };

  // The scroller under the pointer scrolls near its edge, and the place follows what it brings in.
  const scroll = (): void => {
    frame = requestAnimationFrame(scroll);
    // A pointer that stopped lets the copy stand up again.
    if (copy && leans && performance.now() - movedAt > SortPlacement.tiltRestMs) copy.style.setProperty('rotate', '0deg');
    const over = landing ? landing.list : source;
    if (!scrollers.has(over)) scrollers.set(over, scrollerOf(over));
    const scroller = scrollers.get(over) ?? null;
    if (!scroller) return;
    const box = scroller.getBoundingClientRect();
    const down = SortPlacement.edgeScroll(pointer.y, box.top, box.bottom);
    const across = SortPlacement.edgeScroll(pointer.x, box.left, box.right);
    if (down === 0 && across === 0) return;
    scroller.scrollBy(across, down);
    place();
  };

  const escape = (ev: KeyboardEvent): void => {
    if (ev.key !== 'Escape') return;
    // The drag's alone: a dialog's or a find bar's Escape around the list must not answer it too.
    ev.preventDefault();
    ev.stopImmediatePropagation();
    gesture.cancel();
  };

  // The keys are the carry's while the item is carried: an app's chord sits them out.
  let releaseShortcuts = (): void => undefined;

  const end = (): void => {
    cancelAnimationFrame(frame);
    host.removeEventListener('keydown', escape, true);
    releaseShortcuts();
    copy?.remove();
    copy = null;
    item.style.removeProperty('opacity');
    item.removeAttribute('data-eq-sort-lifted');
    if (landing) report(landing.list, null);
  };

  const gesture = followPointer(down, {
    arms:
      down.pointerType === 'touch'
        ? undefined
        : (ev) => Math.hypot(ev.clientX - down.clientX, ev.clientY - down.clientY) > SortPlacement.pickUpTravel,
    holdMs: down.pointerType === 'touch' ? SortPlacement.holdMs : undefined,
    armed: () => {
      copy = lift(item, look);
      releaseShortcuts = holdShortcuts();
      host.addEventListener('keydown', escape, true);
      frame = requestAnimationFrame(scroll);
      place();
    },
    move: (ev) => {
      const now = performance.now();
      if (copy) {
        copy.style.transform = `translate(${ev.clientX - down.clientX}px, ${ev.clientY - down.clientY}px)`;
        if (leans) {
          const angle = SortPlacement.tilt(ev.clientX - pointer.x, now - movedAt, look.maxLean ?? SortPlacement.tiltMax);
          if (angle !== null) copy.style.setProperty('rotate', `${angle}deg`);
        }
      }
      pointer = { x: ev.clientX, y: ev.clientY };
      movedAt = now;
      place();
    },
    release: () => {
      const landed = landing;
      end();
      landing = null;
      if (landed) landed.list.dispatchEvent(new CustomEvent('eq-sort-dropped', { detail: landed.drop }));
    },
    cancel: () => {
      end();
      landing = null;
    },
  });
}

/** The list's own look of a carried item (#1602), each member its lowering wrote; null for a list with none. */
function ownLookOf(list: HTMLElement): SortableTheme | null {
  // A member it does not say, or says as no number, is the theme's.
  const read = (name: string): number | null => {
    const value = list.getAttribute(name)?.trim();
    const number = value ? Number(value) : Number.NaN;
    return Number.isFinite(number) ? number : null;
  };
  const liftedOpacity = read('data-eq-sort-look-lifted');
  const copyOpacity = read('data-eq-sort-look-copy-opacity');
  const copyElevation = read('data-eq-sort-look-copy-elevation');
  const maxLean = read('data-eq-sort-look-lean');
  return liftedOpacity === null && copyOpacity === null && copyElevation === null && maxLean === null
    ? null
    : new SortableTheme({ liftedOpacity, copyOpacity, copyElevation, maxLean });
}

/** The look of an item carried from a list (#1602): the browser's theme's, the list's own over it, and the defaults
 * where neither says, by the arithmetic both targets run. Every member of the answer is set. */
function lookOf(list: HTMLElement): SortableTheme {
  return SortPlacement.look(getPhotonTheme().sortable ?? null, ownLookOf(list));
}

/** The copy's shadow: the theme's for the look's elevation level, in the C# `TokenCss.Shadow` spelling. */
function shadowOf(level: number): string {
  const spec = getPhotonTheme().elevation(level);
  return spec.blur === 0 && spec.offsetY === 0 && spec.spread === 0
    ? 'none'
    : `0 ${num(spec.offsetY)}px ${num(spec.blur)}px ${num(spec.spread)}px ${tokenValue(spec.color)}`;
}

/** The copy that follows the pointer, laid over the item where it stands, and the item dimmed under it, as the
 * look says. */
function lift(item: HTMLElement, look: SortableTheme): HTMLElement {
  const box = item.getBoundingClientRect();
  const copy = item.cloneNode(true) as HTMLElement;
  // A copy is a picture: no id answers twice, and nothing in it is a list or an item of its own.
  for (const element of [copy, ...Array.from(copy.querySelectorAll<HTMLElement>('*'))]) {
    element.removeAttribute('id');
    element.removeAttribute('data-eq-sort');
    element.removeAttribute('data-eq-sort-item');
  }
  copy.setAttribute('aria-hidden', 'true');
  // Laid over the page outside the app's root, so what the item inherits from its ancestors comes with it.
  const inherited = getComputedStyle(item);
  Object.assign(copy.style, {
    font: inherited.font,
    color: inherited.color,
    direction: inherited.direction,
    position: 'fixed',
    left: `${box.left}px`,
    top: `${box.top}px`,
    width: `${box.width}px`,
    height: `${box.height}px`,
    margin: '0',
    pointerEvents: 'none',
    zIndex: '2147483000',
    opacity: num(look.copyOpacity ?? SortPlacement.copyOpacity),
    boxShadow: shadowOf(look.copyElevation ?? SortPlacement.copyElevation),
    cursor: 'grabbing',
    // The lean is the individual `rotate`, apart from the `transform` that places the copy, so easing it
    // never makes the copy trail the pointer.
    transition: 'rotate 0.1s ease-out',
  });
  item.ownerDocument.body.appendChild(copy);
  item.style.opacity = num(look.liftedOpacity ?? SortPlacement.liftedOpacity);
  item.setAttribute('data-eq-sort-lifted', '');
  return copy;
}

/** Whether the person asked for less motion, in which case nothing leans. */
function prefersReducedMotion(): boolean {
  return typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches;
}

/**
 * The list under a point that takes the item: one of its group, or, with no group, its own list alone, as
 * `Sortable.Group` promises; never a list inside the carried item, which would put it inside itself.
 */
function listUnder(x: number, y: number, group: string, source: HTMLElement, carried: HTMLElement): HTMLElement | null {
  if (typeof document.elementsFromPoint !== 'function') return null;
  for (const element of document.elementsFromPoint(x, y)) {
    const list = element.closest('[data-eq-sort]') as HTMLElement | null;
    if (!list || list.hasAttribute('data-eq-sort-off') || carried.contains(list)) continue;
    if (group === '' ? list === source : list.getAttribute('data-eq-sort') === group) return list;
  }
  return null;
}

/**
 * Where an item lands in a list, by the arithmetic every realizer shares (`SortPlacement.Place`): this
 * reads the other items' boxes along the list's axis and hands them over.
 */
function dropIn(list: HTMLElement, id: string, from: string, x: number, y: number, carried: HTMLElement): SortDrop {
  const vertical = list.getAttribute('data-eq-sort-axis') !== 'x';
  const nests = list.getAttribute('data-eq-sort-nests') === '1';
  const to = list.getAttribute('data-eq-sort-id') ?? '';
  const others = itemsOf(list).filter((other) => other !== carried);
  const boxes = others.map((other) => other.getBoundingClientRect());
  const [index, into] = SortPlacement.place(
    boxes.map((box) => (vertical ? box.top : box.left)),
    boxes.map((box) => (vertical ? box.height : box.width)),
    vertical ? y : x,
    nests,
  );
  return into >= 0
    ? new SortDrop(id, from, to, index, { into: others[into].getAttribute('data-eq-sort-item') })
    : new SortDrop(id, from, to, index);
}

/** The list's guard, asked through its element: the lowering answers in the event's detail. */
function accepts(list: HTMLElement, drop: SortDrop): boolean {
  const question = { drop, accepted: true };
  list.dispatchEvent(new CustomEvent('eq-sort-accepts', { detail: question }));
  return question.accepted;
}

function same(a: SortDrop, b: SortDrop): boolean {
  return a.to === b.to && a.index === b.index && a.into === b.into;
}

/** The nearest element at or above a list that scrolls. */
function scrollerOf(list: HTMLElement): HTMLElement | null {
  for (let at: HTMLElement | null = list; at; at = at.parentElement) {
    const style = getComputedStyle(at);
    const scrollsDown = /(auto|scroll)/.test(style.overflowY) && at.scrollHeight > at.clientHeight;
    const scrollsAcross = /(auto|scroll)/.test(style.overflowX) && at.scrollWidth > at.clientWidth;
    if (scrollsDown || scrollsAcross) return at;
  }
  return null;
}

// ---- The keyboard (#1536) ------------------------------------------------------------------------------
//
// With the focus on an item or inside it, or on a composite whose active item it is (a tree's host, #1533),
// Space picks it up; the arrows along the list's axis move it one place (in a list that nests, through the
// places inside the items too), the cross arrows to the neighbouring list of its group; Space or Enter drops
// it; Escape, Tab or a press puts it back. Every place is offered to the list's guard and reported as a pointer's are, and each
// step is said through one polite live region, in the words of the list the item was picked up from (#1564): the node's
// `Announcements`, which the lowering writes on the list's element, the SDK's in the reader's language unless the app
// gave its own. A list with none says nothing.

/** The words a list was given, built back from its element, or null for a list given none. */
function wordsOf(list: HTMLElement | null): SortAnnouncements | null {
  const say = (step: string): string | null => list?.getAttribute(`data-eq-sort-say-${step}`) ?? null;
  const pickedUp = say('picked-up');
  if (pickedUp === null) return null;
  return new SortAnnouncements(
    say('instructions') ?? '',
    pickedUp,
    say('moved') ?? '',
    say('moved-into') ?? '',
    say('refused') ?? '',
    say('dropped') ?? '',
    say('dropped-into') ?? '',
    say('put-back') ?? '',
  );
}

/** A step's words with its holes filled, by the rule written once (C# twin: `SortWords.Fill`, which Photon runs). */
function fill(text: string, ...holes: (string | number)[]): string {
  return SortWords.fill(text, holes.map(String));
}

let announcer: HTMLElement | null = null;
let instructions: HTMLElement | null = null;

/** Said once through the one live region, visually hidden (`eq-announcement`, as a spreadsheet's). Words left
 * empty say nothing. */
function announce(text: string): void {
  if (text === '') return;
  if (!announcer || !announcer.isConnected) {
    announcer = document.createElement('div');
    announcer.className = 'eq-announcement';
    announcer.setAttribute('role', 'status');
    announcer.setAttribute('aria-live', 'polite');
    announcer.setAttribute('aria-atomic', 'true');
    announcer.setAttribute('data-eq-sort-announcer', '');
    document.body.appendChild(announcer);
  }
  announcer.textContent = text;
}

/** The focused control of an item says how to sort it: its description names the keys, in its list's words. */
function describe(ev: FocusEvent): void {
  const focused = ev.target as HTMLElement | null;
  const item = (activeItemOf(focused) ?? focused)?.closest?.('[data-eq-sort-item]');
  if (!focused || !item || item.hasAttribute('data-eq-sort-off') || focused.closest(FIELD)) return;
  const said = wordsOf(listOf(item))?.instructions ?? '';
  if (said === '') return;
  if (!instructions || !instructions.isConnected) {
    instructions = document.createElement('div');
    instructions.id = 'eq-sort-instructions';
    instructions.hidden = true;
    document.body.appendChild(instructions);
  }
  instructions.textContent = said;
  const described = (focused.getAttribute('aria-describedby') ?? '').split(' ').filter(Boolean);
  if (!described.includes(instructions.id))
    focused.setAttribute('aria-describedby', [...described, instructions.id].join(' '));
}

/** What a reader hears an item called: the name it was given for the steps (#1538), its own accessible name,
 * or its words. */
function nameOf(item: HTMLElement): string {
  const named =
    item.getAttribute('data-eq-sort-name') ??
    item.getAttribute('aria-label') ??
    item.querySelector('[aria-label]')?.getAttribute('aria-label');
  return (named ?? item.textContent ?? '').replace(/\s+/g, ' ').trim();
}

function labelOf(list: HTMLElement): string {
  return list.getAttribute('data-eq-sort-label') ?? list.getAttribute('data-eq-sort-id') ?? '';
}

/** The lists an item may be carried to: its group's, in document order, or its own list alone. */
function listsFor(group: string, source: HTMLElement, carried: HTMLElement): HTMLElement[] {
  if (group === '') return [source];
  return Array.from(document.querySelectorAll<HTMLElement>('[data-eq-sort]')).filter(
    (list) =>
      list.getAttribute('data-eq-sort') === group && !list.hasAttribute('data-eq-sort-off') && !carried.contains(list),
  );
}

/** An item carried by the keyboard, and the way to put it back. */
let carrying: { cancel(): void } | null = null;

function onKeyDown(ev: KeyboardEvent): void {
  if (carrying || ev.key !== ' ' || ev.ctrlKey || ev.metaKey || ev.altKey || ev.shiftKey) return;
  const target = ev.target as Element | null;
  if (target?.closest?.(FIELD)) return;
  // A composite keeps the focus on its one stop and names its active item: the item is that one.
  const active = activeItemOf(target);
  const item = (active ?? target)?.closest?.('[data-eq-sort-item]') as HTMLElement | null;
  if (!item || item.hasAttribute('data-eq-sort-off')) return;
  const source = listOf(item);
  if (!source || source.hasAttribute('data-eq-sort-off')) return;
  // Space is the sort's here, not the button's: the press it would make on its release is held off too.
  ev.preventDefault();
  ev.stopImmediatePropagation();
  carryByKeyboard(item, source, active !== null);
}

/** The item a place lands inside, among a list's items with the carried one left out. */
function intoItem(list: HTMLElement, carried: HTMLElement, into: number): HTMLElement | null {
  return into < 0 ? null : (itemsOf(list).filter((other) => other !== carried)[into] ?? null);
}

function carryByKeyboard(item: HTMLElement, source: HTMLElement, fromComposite: boolean): void {
  const host = document;
  const id = item.getAttribute('data-eq-sort-item') ?? '';
  const from = source.getAttribute('data-eq-sort-id') ?? '';
  const group = source.getAttribute('data-eq-sort') ?? '';
  const name = nameOf(item);
  // One carry, one voice: every step is said in the words of the list the item was picked up from.
  const words = wordsOf(source);
  const say = (text: string | undefined, ...holes: (string | number)[]): string =>
    words && text ? fill(text, ...holes) : '';
  let list = source;
  let index = itemsOf(source).indexOf(item);
  let into = -1;

  const countIn = (target: HTMLElement): number => itemsOf(target).filter((other) => other !== item).length + 1;
  const dropAt = (target: HTMLElement, at: number, inside = -1): SortDrop => {
    const to = target.getAttribute('data-eq-sort-id') ?? '';
    const holder = intoItem(target, item, inside);
    return holder
      ? new SortDrop(id, from, to, at, { into: holder.getAttribute('data-eq-sort-item') })
      : new SortDrop(id, from, to, at);
  };
  /** What a reader hears for a place: its position, or the item it is inside. */
  const sayPlace = (moved: string | undefined, inside: string | undefined): string => {
    const holder = intoItem(list, item, into);
    return holder
      ? say(inside, name, nameOf(holder))
      : say(moved, name, index + 1, countIn(list), labelOf(list));
  };

  // The keyboard's carry dims the item as the list it was picked up from says, and draws no copy.
  item.style.opacity = num(lookOf(source).liftedOpacity ?? SortPlacement.liftedOpacity);
  item.setAttribute('data-eq-sort-lifted', '');
  list.dispatchEvent(new CustomEvent('eq-sort-hovered', { detail: dropAt(list, index) }));
  announce(say(words?.pickedUp, name, index + 1, countIn(list), labelOf(list)));

  const moveTo = (target: HTMLElement, at: number, inside = -1): void => {
    const clamped = SortPlacement.step(at, 0, countIn(target));
    const drop = dropAt(target, clamped, inside);
    if (!accepts(target, drop)) {
      announce(say(words?.refused, name));
      return;
    }
    if (target !== list) list.dispatchEvent(new CustomEvent('eq-sort-hovered', { detail: null }));
    list = target;
    index = clamped;
    into = inside;
    list.dispatchEvent(new CustomEvent('eq-sort-hovered', { detail: drop }));
    announce(sayPlace(words?.moved, words?.movedInto));
  };

  /** One place along the list, through the places inside the items when the list nests (C# twin:
   * `SortPlacement.KeyStep`, which Photon runs too), passing over the places the list refuses, as a pointer
   * passes over them: a refused folder never stops the keyboard. At the end it says the place it holds. */
  const stepAlong = (step: number): void => {
    const nests = list.getAttribute('data-eq-sort-nests') === '1';
    const others = countIn(list) - 1;
    let at = index;
    let inside = into;
    for (;;) {
      const [nextAt, nextInside] = SortPlacement.keyStep(at, inside, step, others, nests);
      if (nextAt === at && nextInside === inside) break;
      at = nextAt;
      inside = nextInside;
      if (accepts(list, dropAt(list, at, inside))) break;
    }
    moveTo(list, at, inside);
  };

  const neighbour = (step: number): HTMLElement | null => {
    const lists = listsFor(group, source, item);
    return lists[lists.indexOf(list) + step] ?? null;
  };

  // The release of the Space that picked the item up, or dropped it, presses nothing.
  let holdSpace = true;
  const keyup = (ev: KeyboardEvent): void => {
    if (ev.key !== ' ' || !holdSpace) return;
    ev.preventDefault();
    if (!carrying) {
      holdSpace = false;
      host.removeEventListener('keyup', keyup, true);
    }
  };

  // The keys are the carry's while the item is carried: an app's chord (a tree's Enter) sits them out.
  const releaseShortcuts = holdShortcuts();

  const end = (): void => {
    releaseShortcuts();
    host.removeEventListener('keydown', key, true);
    host.removeEventListener('pointerdown', cancel, true);
    item.style.removeProperty('opacity');
    item.removeAttribute('data-eq-sort-lifted');
    list.dispatchEvent(new CustomEvent('eq-sort-hovered', { detail: null }));
    carrying = null;
  };

  function cancel(): void {
    end();
    announce(say(words?.putBack, name));
  }

  const drop = (): void => {
    const landed = dropAt(list, index, into);
    const at = list;
    const said = sayPlace(words?.dropped, words?.droppedInto);
    end();
    at.dispatchEvent(new CustomEvent('eq-sort-dropped', { detail: landed }));
    announce(said);
    // A composite keeps the focus on its one stop, and follows its moved item itself.
    if (fromComposite) return;
    // The app builds the item where it went, perhaps as a new element: the keyboard follows it there.
    requestAnimationFrame(() =>
      requestAnimationFrame(() => {
        const moved = Array.from(document.querySelectorAll<HTMLElement>('[data-eq-sort-item]')).find(
          (element) => element.getAttribute('data-eq-sort-item') === id,
        );
        const focusable = moved?.matches('[tabindex], button, a, input, select, textarea')
          ? moved
          : moved?.querySelector<HTMLElement>('[tabindex], button, a, input, select, textarea');
        focusable?.focus({ preventScroll: false });
      }),
    );
  };

  function key(ev: KeyboardEvent): void {
    const vertical = list.getAttribute('data-eq-sort-axis') !== 'x';
    const next = vertical ? 'ArrowDown' : 'ArrowRight';
    const previous = vertical ? 'ArrowUp' : 'ArrowLeft';
    const across = vertical ? 'ArrowRight' : 'ArrowDown';
    const back = vertical ? 'ArrowLeft' : 'ArrowUp';
    switch (ev.key) {
      case next:
        stepAlong(1);
        break;
      case previous:
        stepAlong(-1);
        break;
      case across:
      case back: {
        const target = neighbour(ev.key === across ? 1 : -1);
        if (target) moveTo(target, index);
        break;
      }
      case ' ':
      case 'Enter':
        drop();
        break;
      case 'Escape':
        cancel();
        break;
      case 'Tab':
        // The focus is leaving: the item goes back, and Tab goes on.
        cancel();
        return;
      default:
        return;
    }
    ev.preventDefault();
    ev.stopImmediatePropagation();
  }

  carrying = { cancel };
  host.addEventListener('keydown', key, true);
  host.addEventListener('keyup', keyup, true);
  host.addEventListener('pointerdown', cancel, true);
}
