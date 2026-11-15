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
import { SortDrop } from '../shared/value-types';
import { followPointer, pointerDownDelegate } from './pointer-gesture';

/** The travel that picks an item up with a mouse: the Space board's own threshold. */
const MOUSE_TRAVEL = 5;
/** How long a finger holds an item still to pick it up. */
const HOLD_MS = 250;
/** How near a scroller's edge the pointer scrolls it, and its fastest step per frame. */
const EDGE = 32;
const STEP = 14;

const delegate = pointerDownDelegate(onPointerDown);

export function installSortableController(): void {
  delegate.install();
}

export function resetSortableController(): void {
  delegate.reset();
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

  const report = (list: HTMLElement, drop: SortDrop | null): void => {
    list.dispatchEvent(new CustomEvent('eq-sort-hovered', { detail: drop }));
  };

  /** Where the item would land under the pointer, offered to the list's guard, and reported when it moves. */
  const place = (): void => {
    const list = listUnder(pointer.x, pointer.y, group);
    const drop = list ? dropIn(list, id, from, pointer.x, pointer.y, item) : null;
    const accepted = list !== null && drop !== null && accepts(list, drop);
    const next = accepted ? { list: list!, drop: drop! } : null;
    if (landing && (!next || next.list !== landing.list)) report(landing.list, null);
    if (next && (!landing || landing.list !== next.list || !same(landing.drop, next.drop))) report(next.list, next.drop);
    landing = next;
  };

  // The scroller under the pointer scrolls near its edge, and the place follows what it brings in.
  const scroll = (): void => {
    frame = requestAnimationFrame(scroll);
    const scroller = landing ? scrollerOf(landing.list) : scrollerOf(source);
    if (!scroller) return;
    const box = scroller.getBoundingClientRect();
    const near = (at: number, start: number, end: number): number =>
      at < start + EDGE ? -Math.ceil(((start + EDGE - at) / EDGE) * STEP)
        : at > end - EDGE ? Math.ceil(((at - (end - EDGE)) / EDGE) * STEP)
          : 0;
    const down = near(pointer.y, box.top, box.bottom);
    const across = near(pointer.x, box.left, box.right);
    if (down === 0 && across === 0) return;
    scroller.scrollBy(across, down);
    place();
  };

  const escape = (ev: KeyboardEvent): void => {
    if (ev.key !== 'Escape') return;
    ev.preventDefault();
    gesture.cancel();
  };

  const end = (): void => {
    cancelAnimationFrame(frame);
    host.removeEventListener('keydown', escape, true);
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
        : (ev) => Math.hypot(ev.clientX - down.clientX, ev.clientY - down.clientY) > MOUSE_TRAVEL,
    holdMs: down.pointerType === 'touch' ? HOLD_MS : undefined,
    armed: () => {
      copy = lift(item);
      host.addEventListener('keydown', escape, true);
      frame = requestAnimationFrame(scroll);
      place();
    },
    move: (ev) => {
      pointer = { x: ev.clientX, y: ev.clientY };
      if (copy) copy.style.transform = `translate(${ev.clientX - down.clientX}px, ${ev.clientY - down.clientY}px)`;
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

/** The copy that follows the pointer, laid over the item where it stands, and the item dimmed under it. */
function lift(item: HTMLElement): HTMLElement {
  const box = item.getBoundingClientRect();
  const copy = item.cloneNode(true) as HTMLElement;
  // A copy is a picture: no id answers twice, and nothing in it is a list or an item of its own.
  for (const element of [copy, ...Array.from(copy.querySelectorAll<HTMLElement>('*'))]) {
    element.removeAttribute('id');
    element.removeAttribute('data-eq-sort');
    element.removeAttribute('data-eq-sort-item');
  }
  copy.setAttribute('aria-hidden', 'true');
  Object.assign(copy.style, {
    position: 'fixed',
    left: `${box.left}px`,
    top: `${box.top}px`,
    width: `${box.width}px`,
    height: `${box.height}px`,
    margin: '0',
    pointerEvents: 'none',
    zIndex: '2147483000',
    opacity: '0.92',
    boxShadow: '0 12px 28px rgba(0, 0, 0, 0.28)',
    cursor: 'grabbing',
  });
  item.ownerDocument.body.appendChild(copy);
  item.style.opacity = '0.4';
  item.setAttribute('data-eq-sort-lifted', '');
  return copy;
}

/** The list of the group under a point, if one takes items. */
function listUnder(x: number, y: number, group: string): HTMLElement | null {
  if (typeof document.elementsFromPoint !== 'function') return null;
  for (const element of document.elementsFromPoint(x, y)) {
    const list = element.closest('[data-eq-sort]') as HTMLElement | null;
    if (list && list.getAttribute('data-eq-sort') === group && !list.hasAttribute('data-eq-sort-off')) return list;
  }
  return null;
}

/**
 * Where an item lands in a list: before the first OTHER item whose middle the pointer has not passed along
 * the list's axis, so the index counts the list without it; in a list that nests, into the item whose
 * middle half the pointer is over.
 */
function dropIn(list: HTMLElement, id: string, from: string, x: number, y: number, carried: HTMLElement): SortDrop {
  const vertical = list.getAttribute('data-eq-sort-axis') !== 'x';
  const nests = list.getAttribute('data-eq-sort-nests') === '1';
  const to = list.getAttribute('data-eq-sort-id') ?? '';
  const others = itemsOf(list).filter((other) => other !== carried);
  const at = vertical ? y : x;
  for (let index = 0; index < others.length; index++) {
    const box = others[index].getBoundingClientRect();
    const start = vertical ? box.top : box.left;
    const size = vertical ? box.height : box.width;
    if (nests && at >= start + size / 4 && at <= start + (size * 3) / 4)
      return new SortDrop(id, from, to, index, { into: others[index].getAttribute('data-eq-sort-item') });
    if (at < start + size / 2) return new SortDrop(id, from, to, index);
  }
  return new SortDrop(id, from, to, others.length);
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
