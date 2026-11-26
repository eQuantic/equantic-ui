/**
 * A `LayoutBuilder`'s room in the browser (#1555): its box is measured after the pass is written, and
 * the child is built for what was measured.
 *
 * The browser lays out after the runtime writes the DOM, so a builder cannot run inside that layout as
 * Photon's runs inside its own. It runs right after it, against the same room. The first lowering
 * writes the box empty, as the server does. The pass's commit measures each builder's box AS IF IT
 * HELD NOTHING: `contain: size` is set on it for two reads and taken off. Under size containment an
 * axis the parent decides keeps its size, and an axis sized by content takes the box's
 * `contain-intrinsic-size`, so the two reads give it two intrinsic sizes and an axis that follows them
 * is sized by content, which is read as unbounded. One that does not is the parent's, zero included:
 * exactly the room Photon projects from its own constraints (`FillingRoom`). Content therefore never
 * feeds back into the constraints.
 *
 * When the measured room changes, the nearest component above the builder is marked to build. The
 * builder is that component's closure, so the render that follows patches that component, and its
 * lowering finds the room on the position, runs the builder and shows its child in the slot.
 */

import { BoxConstraints } from './value-types';
import { num } from './css-values';
import { elementEngine } from './element-engine';
import type { Element as Position } from './runtime-exports';
import type { VisualNodeValue } from './nodes';
import { DISPOSABLE, nodeStateFor, type NodeState } from './node-states';
import { passInvalidator } from './pass-invalidator';

/** The box a builder's position measures, and which position that is. */
interface MeasuredBox extends HTMLElement {
  __eqLayoutBuilder?: LayoutBuilderState;
}

/** What the builder last answered: for which node, in which room. */
interface Built {
  readonly node: object;
  readonly room: BoxConstraints;
  readonly child: VisualNodeValue;
}

/**
 * What a builder's position keeps between passes: the room last measured, what the builder answered
 * for it, the box it measures and its observer. Measuring another box starts the observer again.
 */
export class LayoutBuilderState implements NodeState {
  /** Disposed when its position leaves the tree (`node-states.ts`). */
  static readonly $interfaces = DISPOSABLE;

  /** The room the box was last measured at; null until the first measurement. */
  room: BoxConstraints | null = null;
  position: Position | null = null;
  /** What renders the page whose pass lowered the builder: the build a builder with no component above asks for. */
  renderPage: (() => void) | null = null;
  view: MeasuredBox | null = null;
  observer: ResizeObserver | undefined = undefined;
  private built: Built | null = null;

  /**
   * The child for `node` in the room last measured: the one built before when the node and the room are
   * the same, so a pass that lowers the builder again in the same room runs nothing (C# `BuiltFor`).
   */
  childFor(
    node: object,
    builder: (room: BoxConstraints) => VisualNodeValue,
  ): VisualNodeValue | null {
    const room = this.room;
    if (!room) return null;
    const last = this.built;
    if (last && last.node === node && last.room.equals(room)) return last.child;
    const child = builder(room);
    this.built = { node, room, child };
    return child;
  }

  /** Measures `view`, letting go of whatever measured it before, this position or another. */
  measure(view: MeasuredBox): void {
    const before = view.__eqLayoutBuilder;
    if (before && before !== this && before.view === view) before.letGo();
    this.letGo();
    this.view = view;
    view.__eqLayoutBuilder = this;
    if (typeof ResizeObserver === 'undefined') return;
    const observer = new ResizeObserver(() => this.remeasure());
    observer.observe(view);
    this.observer = observer;
  }

  /** Reads the room the box is given now, and asks for a build when it is not the one the child was built for. */
  remeasure(): void {
    const view = this.view;
    if (!view?.isConnected) return;
    const room = roomOf(view);
    if (this.room?.equals(room)) return;
    this.room = room;
    buildAbove(this.position, this.renderPage);
  }

  /** Stops measuring the box. */
  letGo(): void {
    this.observer?.disconnect();
    this.observer = undefined;
    if (this.view?.__eqLayoutBuilder === this) delete this.view.__eqLayoutBuilder;
    this.view = null;
  }

  dispose(): void {
    // A position that leaves before the pass's commit reads it is not measured: the commit would
    // observe a box for a state nothing disposes again.
    const id = this.position ? String(this.position.id) : null;
    if (id !== null && declared.get(id) === this) declared.delete(id);
    this.letGo();
    this.room = null;
    this.built = null;
    this.position = null;
    this.renderPage = null;
  }
}

/**
 * The room `view` is given, read with nothing in it: size containment makes the box as large as its
 * parent decides on each axis, and as large as its `contain-intrinsic-size` on an axis sized by
 * content. Read at two intrinsic sizes, an axis that moves with them is unbounded, and one that does
 * not is the parent's, zero included: a box its parent decides at zero is told zero, as Photon tells
 * it. Rounded as the canvas rounds its box, then to single precision, since every bound of the C#
 * value is a `float`.
 */
export function roomOf(view: HTMLElement): BoxConstraints {
  const style = view.style;
  const before = {
    contain: style.getPropertyValue('contain'),
    intrinsic: style.getPropertyValue('contain-intrinsic-size'),
  };
  style.setProperty('contain', 'size');
  const at = (intrinsic: string) => {
    style.setProperty('contain-intrinsic-size', intrinsic);
    return view.getBoundingClientRect();
  };
  const none = at('0px 0px');
  const some = at('1px 1px');
  restore(style, 'contain', before.contain);
  restore(style, 'contain-intrinsic-size', before.intrinsic);
  const extent = (length: number, moved: boolean) =>
    moved ? Infinity : Math.fround(parseFloat(num(length)));
  return new BoxConstraints(
    0,
    extent(none.width, some.width !== none.width),
    0,
    extent(none.height, some.height !== none.height),
  );
}

/** Puts back what an inline property was before a read changed it. */
function restore(style: CSSStyleDeclaration, property: string, value: string): void {
  if (value) style.setProperty(property, value);
  else style.removeProperty(property);
}

/**
 * Marks the nearest component above `position` to build: the one whose build holds the builder. A
 * builder no component holds stands in the page's own tree, whose page renders it again.
 */
function buildAbove(position: Position | null, renderPage: (() => void) | null): void {
  const { ComponentElement } = elementEngine();
  for (let at = position?.parent ?? null; at; at = at.parent) {
    if (at instanceof ComponentElement) {
      at.markNeedsBuild();
      return;
    }
  }
  renderPage?.();
}

/** The builders the latest passes declared, by their position's id, until the commit reads them. */
const declared = new Map<string, LayoutBuilderState>();

/**
 * The state the builder at `position` keeps, declared as a box to measure after the pass. Answers the
 * id the lowering stamps on the box as `data-eq-layout-builder`. Called once per pass that lowers it.
 */
export function declareLayoutBuilder(position: Position): {
  id: string;
  state: LayoutBuilderState;
} {
  const state = nodeStateFor(position, LayoutBuilderState);
  state.position = position;
  state.renderPage = passInvalidator();
  const id = String(position.id);
  declared.set(id, state);
  return { id, state };
}

/** Commits AFTER the pass's DOM has been written, a microtask later, as the canvases do. */
export function scheduleLayoutBuilderCommit(): void {
  if (declared.size === 0) return;
  if (typeof queueMicrotask !== 'function') {
    commitLayoutBuilders();
    return;
  }
  queueMicrotask(commitLayoutBuilders);
}

/**
 * Measures every box the passes declared. A box keeps its observer across passes; one met for the
 * first time is observed, and every one is measured now, which is how the first room arrives.
 */
export function commitLayoutBuilders(): void {
  if (declared.size === 0) return;
  if (typeof document === 'undefined') {
    declared.clear();
    return;
  }

  const pending = new Map(declared);
  declared.clear();
  for (const element of document.querySelectorAll<HTMLElement>('[data-eq-layout-builder]')) {
    const state = pending.get(element.getAttribute('data-eq-layout-builder') ?? '');
    if (!state) continue;
    if (state.view !== element) state.measure(element as MeasuredBox);
    state.remeasure();
  }
}
