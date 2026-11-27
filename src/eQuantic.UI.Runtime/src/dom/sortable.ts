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
 *
 * In a list whose items have depths (`data-eq-sort-indent`, #1594) an item carries the deeper items right after it,
 * and a place between two items says the depth the item takes there: the pointer asks for one per indent it travelled
 * across, the keyboard's cross arrows step it, and `SortPlacement.depths` gives the order the guard is asked in.
 *
 * What is horizontal reads in the READING order, where each list sits (#1569), read when the pointer or the key
 * arrives and never earlier (`reading-direction.ts` says why): a horizontal list under `rtl` hands
 * `SortPlacement.place` each item's start measured from the right and the pointer the same, so the starts ascend in
 * reading order as `Place` assumes; a vertical list's depth travel and the keyboard's cross arrows count toward the
 * reading end as deeper; the keyboard's arrows along a horizontal list swap. The carried copy's position, its lean and
 * the edge scroll stay physical on purpose: they follow the finger on the screen.
 */
import { SortPlacement } from '../shared/components/SortPlacement';
import { SortWords } from '../shared/components/SortWords';
import { SortAnnouncements, SortDrop } from '../shared/value-types';
import { SortableTheme } from '../shared/theme-slots';
import { getPhotonTheme } from '../shared/photon-context';
import { num, tokenValue } from '../shared/css-values';
import { activeItemOf } from './active-item';
import { followPointer, pointerDownDelegate } from './pointer-gesture';
import { readsRightToLeft } from './reading-direction';
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

/** How far across the axis one depth of a list's items is indented, or 0 for a list without depths (#1594). */
function indentOf(list: HTMLElement): number {
  const indent = Number(list.getAttribute('data-eq-sort-indent') ?? '0');
  return SortPlacement.hasDepths(indent) ? indent : 0;
}

/** An item's depth in a list whose items have depths; 0 for one that says none. */
function depthOf(item: HTMLElement): number {
  const depth = Number(item.getAttribute('data-eq-sort-depth') ?? '0');
  return Number.isInteger(depth) && depth > 0 ? depth : 0;
}

/** The items of a list a carried one is placed among: the carried one left out, and in a list with depths the deeper
 * items right after it too, its subtree, which it carries (#1594). C# twin: `PhotonHost.ItemsOf`. */
function othersOf(list: HTMLElement, carried: HTMLElement): HTMLElement[] {
  const items = itemsOf(list);
  const at = items.indexOf(carried);
  if (at < 0) return items;
  let end = at + 1;
  if (indentOf(list) > 0) {
    const depth = depthOf(carried);
    while (end < items.length && depthOf(items[end]) > depth) end++;
  }
  return [...items.slice(0, at), ...items.slice(end)];
}

/** The drop a place means, among `others`: the item it lands inside named, and its depth when it has one. */
function dropFor(
  list: HTMLElement,
  id: string,
  from: string,
  others: HTMLElement[],
  index: number,
  into: number,
  depth: number | null,
): SortDrop {
  const to = list.getAttribute('data-eq-sort-id') ?? '';
  const config: { into?: string | null; depth?: number | null } = {};
  if (into >= 0) config.into = others[into].getAttribute('data-eq-sort-item');
  if (depth !== null) config.depth = depth;
  return new SortDrop(id, from, to, index, config);
}

/**
 * The drops a spot of a list may mean, in the order the list's guard is asked (#1594): the spot itself, or in a list
 * with depths, at a spot between two items, one for each depth `SortPlacement.depths` gives from the one `wants`.
 */
function placesAt(
  list: HTMLElement,
  id: string,
  from: string,
  others: HTMLElement[],
  index: number,
  into: number,
  wants: number,
): SortDrop[] {
  if (into >= 0 || indentOf(list) === 0) return [dropFor(list, id, from, others, index, into, null)];
  const above = index > 0 ? depthOf(others[index - 1]) : null;
  const below = index < others.length ? depthOf(others[index]) : null;
  return SortPlacement.depths(wants, above, below).map((depth) => dropFor(list, id, from, others, index, -1, depth));
}

/** The first of the drops the list's guard accepts, or null when it refuses them all. */
function firstAccepted(list: HTMLElement, drops: SortDrop[]): SortDrop | null {
  for (const drop of drops) if (accepts(list, drop)) return drop;
  return null;
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

  // The guard is asked once per place and depth asked for, not once per move: a tree's walks its subtree. A place is
  // its list, its index and item inside, the items around it (rows that open above shift what an index means) and the
  // depth asked for.
  let asked: { list: HTMLElement; key: string; drop: SortDrop | null } | null = null;
  const scrollers = new Map<HTMLElement, HTMLElement | null>();
  const ownDepth = depthOf(item);

  /** Where the item would land under the pointer, offered to the list's guard, and reported when it moves. */
  const place = (): void => {
    const list = listUnder(pointer.x, pointer.y, group, source, item);
    let next: { list: HTMLElement; drop: SortDrop } | null = null;
    if (list) {
      const vertical = list.getAttribute('data-eq-sort-axis') !== 'x';
      // The depth the pointer asks for, from its travel across since it picked the item up (#1594).
      // Toward the reading end of the list it is over, which is leftwards where that list reads right to left.
      const travelled = vertical ? (readsRightToLeft(list) ? -1 : 1) * (pointer.x - down.clientX) : pointer.y - down.clientY;
      const wants = SortPlacement.wanted(ownDepth, travelled, indentOf(list));
      const others = othersOf(list, item);
      const [index, into] = spotIn(list, others, pointer.x, pointer.y);
      const around = [others[index - 1], others[index]].map((at) => at?.getAttribute('data-eq-sort-item') ?? '');
      const key = JSON.stringify([index, into, wants, ...around]);
      if (!(asked && asked.list === list && asked.key === key))
        asked = { list, key, drop: firstAccepted(list, placesAt(list, id, from, others, index, into, wants)) };
      next = asked.drop ? { list, drop: asked.drop } : null;
    }
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
    element: item,
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
  }, delegate.guards);
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
 * Where an item lands among a list's `others`, by the arithmetic every realizer shares (`SortPlacement.Place`):
 * this reads their boxes along the list's axis and hands them over. The index it lands at, and the item it lands
 * inside or -1.
 */
function spotIn(list: HTMLElement, others: HTMLElement[], x: number, y: number): [number, number] {
  const vertical = list.getAttribute('data-eq-sort-axis') !== 'x';
  const nests = list.getAttribute('data-eq-sort-nests') === '1';
  const boxes = others.map((other) => other.getBoundingClientRect());
  // `Place` reads starts that ascend in reading order. A horizontal list under rtl reads from its right edge, so
  // each start is the distance from the right and the pointer is measured the same way; the extents are the same.
  const reversed = !vertical && readsRightToLeft(list);
  const [index, into] = SortPlacement.place(
    boxes.map((box) => (vertical ? box.top : reversed ? -box.right : box.left)),
    boxes.map((box) => (vertical ? box.height : box.width)),
    vertical ? y : reversed ? -x : x,
    nests,
  );
  return [index, into];
}

/** The list's guard, asked through its element: the lowering answers in the event's detail. */
function accepts(list: HTMLElement, drop: SortDrop): boolean {
  const question = { drop, accepted: true };
  list.dispatchEvent(new CustomEvent('eq-sort-accepts', { detail: question }));
  return question.accepted;
}

function same(a: SortDrop, b: SortDrop): boolean {
  return a.to === b.to && a.index === b.index && a.into === b.into && a.depth === b.depth;
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
// places inside the items too), the cross arrows to the neighbouring list of its group, in a list whose items have
// depths stepping the depth first while the place offers another (#1594); Space or Enter drops
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

/** The item a place lands inside, among the items a carried one is placed among. */
function intoItem(list: HTMLElement, carried: HTMLElement, into: number): HTMLElement | null {
  return into < 0 ? null : (othersOf(list, carried)[into] ?? null);
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
  // In a list whose items have depths, the depth the place holds, and the one the keyboard last asked for, which the
  // item keeps as it moves along, as a pointer keeps its travel across (#1594).
  let depth: number | null = null;
  let wants = depthOf(item);

  const othersIn = (target: HTMLElement): HTMLElement[] => othersOf(target, item);
  const countIn = (target: HTMLElement): number => othersIn(target).length + 1;
  /** The drop a spot means, offered to the guard: in a list with depths, at a spot between items, each depth from
   * the one asked for. Null when the list refuses them all. */
  const offer = (target: HTMLElement, at: number, inside: number, wanted = wants): SortDrop | null =>
    firstAccepted(target, placesAt(target, id, from, othersIn(target), at, inside, wanted));
  /** What a reader hears for a place: its position, the count and the list, or in a list with depths the position and
   * the count among the items at its depth under the same holder and the holder's name (#1594); or the item it is
   * inside. */
  const sayPlace = (moved: string | undefined, inside: string | undefined): string => {
    const holder = intoItem(list, item, into);
    if (holder) return say(inside, name, nameOf(holder));
    if (depth === null) return say(moved, name, index + 1, countIn(list), labelOf(list));
    const others = othersIn(list);
    const [at, position, count] = SortPlacement.siblings(others.map(depthOf), index, depth);
    return say(moved, name, position, count, at >= 0 ? nameOf(others[at]) : labelOf(list));
  };

  // The keyboard's carry dims the item as the list it was picked up from says, and draws no copy.
  item.style.opacity = num(lookOf(source).liftedOpacity ?? SortPlacement.liftedOpacity);
  item.setAttribute('data-eq-sort-lifted', '');
  // Its own place, which no guard is asked about: in a list with depths its own depth is always one the place allows.
  const picked = placesAt(list, id, from, othersIn(list), index, -1, wants)[0];
  depth = picked.depth;
  list.dispatchEvent(new CustomEvent('eq-sort-hovered', { detail: picked }));
  announce(sayPlace(words?.pickedUp, words?.pickedUp));

  const moveTo = (target: HTMLElement, at: number, inside = -1, wanted = wants): void => {
    const clamped = SortPlacement.step(at, 0, countIn(target));
    const drop = offer(target, clamped, inside, wanted);
    if (!drop) {
      announce(say(words?.refused, name));
      return;
    }
    if (target !== list) list.dispatchEvent(new CustomEvent('eq-sort-hovered', { detail: null }));
    list = target;
    index = clamped;
    into = inside;
    depth = drop.depth;
    list.dispatchEvent(new CustomEvent('eq-sort-hovered', { detail: drop }));
    announce(sayPlace(words?.moved, words?.movedInto));
  };

  /** One depth deeper or shallower at the place the item holds, in a list whose items have depths (C# twin:
   * `SortPlacement.KeyDepth`, which Photon runs too), passing over the depths the list refuses (#1594). Answers whether
   * the place offered a depth that way: the cross arrow moves to the neighbouring list only when it offered none. */
  const stepDepth = (step: number): boolean => {
    if (depth === null || into >= 0) return false;
    const others = othersIn(list);
    const above = index > 0 ? depthOf(others[index - 1]) : null;
    const below = index < others.length ? depthOf(others[index]) : null;
    let next: number = depth;
    for (;;) {
      const further = SortPlacement.keyDepth(next, step, above, below);
      if (further === next) break;
      next = further;
      if (accepts(list, dropFor(list, id, from, others, index, -1, next))) {
        wants = next;
        moveTo(list, index, -1, next);
        return true;
      }
    }
    if (next === depth) return false;
    announce(say(words?.refused, name));
    return true;
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
      if (offer(list, at, inside)) break;
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
    const landed = dropFor(list, id, from, othersIn(list), index, into, depth);
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
    // The horizontal arrows are toward the reading end and toward its start, so they swap where the list reads
    // right to left: along a horizontal list (next and previous) and across a vertical one (deeper and shallower).
    const [toEnd, toStart] = readsRightToLeft(list) ? ['ArrowLeft', 'ArrowRight'] : ['ArrowRight', 'ArrowLeft'];
    const next = vertical ? 'ArrowDown' : toEnd;
    const previous = vertical ? 'ArrowUp' : toStart;
    const across = vertical ? toEnd : 'ArrowDown';
    const back = vertical ? toStart : 'ArrowUp';
    switch (ev.key) {
      case next:
        stepAlong(1);
        break;
      case previous:
        stepAlong(-1);
        break;
      case across:
      case back: {
        const step = ev.key === across ? 1 : -1;
        if (stepDepth(step)) break;
        const target = neighbour(step);
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
