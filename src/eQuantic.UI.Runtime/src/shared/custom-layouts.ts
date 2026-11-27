/**
 * A custom layout in the browser (#1556): the app's delegate, transpiled from C#, runs right after the pass is
 * written, as a `LayoutBuilder`'s room is read then (layout-builders.ts).
 *
 * The server and the browser's first lowering write the node's box with every child in it, each in a wrapper at the
 * box's origin, and the box hidden. The pass's commit reads the room the box is given as a builder's is read
 * (`roomOf`), binds the delegate to the children, and for each `layoutChild` gives the child's wrapper the room the
 * constraints allow (their maximum, definite where it is finite), sets their bounds on the child's box and reads the
 * box. Photon measures the child's box in the same room, so the two agree by the box rule both already keep. Each
 * `positionChild` moves the wrapper; the box takes the delegate's size on an axis sized by content, and is shown.
 *
 * A `ResizeObserver` on the box and on every child's box lays it out again when the room or a child's size changes.
 * Nothing is built: a layout writes styles only.
 */

import { num } from './css-values';
import type { Element as Position } from './runtime-exports';
import { DISPOSABLE, nodeStateFor, type NodeState } from './node-states';
import { roomOf } from './layout-builders';
import { exception } from '../utils/exceptions';
import { equals } from '../utils/equals';
import type { MultiChildLayoutDelegate, LayoutChildren } from './layout-delegates';
import { Size } from './value-types';
import type { BoxConstraints, Point } from './value-types';

/** The box a custom layout's position lays out, and which position that is. */
interface LaidOutBox extends HTMLElement {
  __eqCustomLayout?: CustomLayoutState;
}

/** A length in the browser as the C# value holds it: rounded as the canvas rounds its box, then to single precision. */
const dp = (length: number) => Math.fround(parseFloat(num(length)));
const px = (value: number) => `${num(value)}px`;

/**
 * What a custom layout's position keeps between passes: the delegate the last pass lowered, the ids of its children,
 * the box it lays out and the observer that lays it out again.
 */
export class CustomLayoutState implements NodeState {
  /** Disposed when its position leaves the tree (`node-states.ts`). */
  static readonly $interfaces = DISPOSABLE;

  position: Position | null = null;
  delegate: MultiChildLayoutDelegate | null = null;
  ids: readonly unknown[] = [];
  view: LaidOutBox | null = null;
  observer: ResizeObserver | undefined = undefined;

  /** Lays `view` out from now on, letting go of whatever laid it out before, this position or another. */
  measure(view: LaidOutBox): void {
    const before = view.__eqCustomLayout;
    if (before && before !== this && before.view === view) before.letGo();
    this.letGo();
    this.view = view;
    view.__eqCustomLayout = this;
    if (typeof ResizeObserver === 'undefined') return;
    const observer = new ResizeObserver(() => this.layout());
    observer.observe(view);
    this.observer = observer;
  }

  /** Runs the delegate for the room the box is given now, and places every child where it says. */
  layout(): void {
    const view = this.view;
    const delegate = this.delegate;
    if (!view?.isConnected || !delegate) return;
    const wrappers = Array.from(view.children) as HTMLElement[];
    // A child a rebuild added is watched from its first layout: `observe` keeps one entry per box.
    for (const wrapper of wrappers) if (wrapper.firstElementChild) this.observer?.observe(wrapper.firstElementChild);
    const ids = this.ids;
    for (let i = 0; i < ids.length; i++)
      for (let j = i + 1; j < ids.length; j++)
        if (equals(ids[i], ids[j]))
          throw exception(
            'System.ArgumentException',
            `Two children of a CustomMultiChildLayout carry the id '${String(ids[i])}': a delegate knows each child by one id. (Parameter 'children')`,
          );

    // The room is read with nothing of the layout's own size on the box, which is what a builder's box has.
    view.style.removeProperty('width');
    view.style.removeProperty('height');
    const room = roomOf(view);

    const places: (Point | null)[] = ids.map(() => null);
    const indexOf = (id: unknown) => ids.findIndex((known) => equals(known, id));
    const children: LayoutChildren = {
      has: (id) => indexOf(id) >= 0,
      layout: (id, constraints) => {
        const wrapper = wrappers[indexOf(id)];
        const box = wrapper?.firstElementChild as HTMLElement | null;
        if (!wrapper || !box) return Size.zero;
        bound(wrapper, box, constraints);
        const read = box.getBoundingClientRect();
        return new Size(dp(read.width), dp(read.height));
      },
      position: (id, offset) => {
        places[indexOf(id)] = offset;
      },
    };
    const size = delegate.layoutWith(children, room, ids);

    places.forEach((place, i) => {
      const wrapper = wrappers[i];
      if (!wrapper) return;
      wrapper.style.left = px(place?.x ?? 0);
      wrapper.style.top = px(place?.y ?? 0);
    });
    // On an axis the parent decides the box is the room's, as a builder's; on one sized by content, the delegate's.
    if (!room.hasBoundedWidth) view.style.width = px(size.width);
    if (!room.hasBoundedHeight) view.style.height = px(size.height);
    view.style.visibility = 'visible';
  }

  /** Stops laying the box out. */
  letGo(): void {
    this.observer?.disconnect();
    this.observer = undefined;
    if (this.view?.__eqCustomLayout === this) delete this.view.__eqCustomLayout;
    this.view = null;
  }

  dispose(): void {
    const id = this.position ? String(this.position.id) : null;
    if (id !== null && declared.get(id) === this) declared.delete(id);
    this.letGo();
    this.delegate = null;
    this.ids = [];
    this.position = null;
  }
}

/**
 * Gives a child's wrapper the room `constraints` allow, their maximum, definite where it is finite and the child's
 * own where it is not, and sets their bounds on the child's box: the room and the bounds Photon measures it in.
 */
function bound(wrapper: HTMLElement, box: HTMLElement, constraints: BoxConstraints): void {
  wrapper.style.width = constraints.hasBoundedWidth ? px(constraints.maxWidth) : 'max-content';
  if (constraints.hasBoundedHeight) wrapper.style.height = px(constraints.maxHeight);
  else wrapper.style.removeProperty('height');
  const set = (property: string, value: number, when: boolean) => {
    if (when) box.style.setProperty(property, px(value));
    else box.style.removeProperty(property);
  };
  set('min-width', constraints.minWidth, constraints.minWidth > 0);
  set('max-width', constraints.maxWidth, constraints.hasBoundedWidth);
  set('min-height', constraints.minHeight, constraints.minHeight > 0);
  set('max-height', constraints.maxHeight, constraints.hasBoundedHeight);
}

/** The custom layouts the latest passes declared, by their position's id, until the commit reads them. */
const declared = new Map<string, CustomLayoutState>();

/**
 * The state the custom layout at `position` keeps, declared as a box to lay out after the pass, with the delegate and
 * the ids this pass lowered. Answers the id the lowering stamps on the box as `data-eq-custom-layout`.
 */
export function declareCustomLayout(
  position: Position,
  delegate: MultiChildLayoutDelegate,
  ids: readonly unknown[],
): { id: string; state: CustomLayoutState } {
  const state = nodeStateFor(position, CustomLayoutState);
  state.position = position;
  state.delegate = delegate;
  state.ids = ids;
  const id = String(position.id);
  declared.set(id, state);
  return { id, state };
}

/** Commits AFTER the pass's DOM has been written, a microtask later, as the builders do. */
export function scheduleCustomLayoutCommit(): void {
  if (declared.size === 0) return;
  if (typeof queueMicrotask !== 'function') {
    commitCustomLayouts();
    return;
  }
  queueMicrotask(commitCustomLayouts);
}

/** Lays out every box the passes declared: a box met for the first time is observed, and every one is laid out now. */
export function commitCustomLayouts(): void {
  if (declared.size === 0) return;
  if (typeof document === 'undefined') {
    declared.clear();
    return;
  }
  const pending = new Map(declared);
  declared.clear();
  for (const element of document.querySelectorAll<HTMLElement>('[data-eq-custom-layout]')) {
    const state = pending.get(element.getAttribute('data-eq-custom-layout') ?? '');
    if (!state) continue;
    if (state.view !== element) state.measure(element as LaidOutBox);
    state.layout();
  }
}
