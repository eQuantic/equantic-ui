/**
 * The web half of the C# `ContextTarget` (#1531): a person asks for the menu of something, and the innermost
 * target around what they asked about hears it as `eq-context-requested`, which the lowering turns into the C#
 * callback with a `ContextRequest`.
 *
 * - A right-click, or a Control-click on a Mac: the browser's `contextmenu`, answered at the pointer's point in
 *   the target's box. Where a target answers, the browser's own menu does not open; everywhere else it does, and
 *   a field inside a target keeps its own (its copy and paste are the field's).
 * - The menu key or Shift+F10: answered at the keydown, with no point, for the target around the focus. The
 *   browser's `contextmenu` that would follow is held off, and a browser that fires none for Shift+F10 (Safari)
 *   is answered all the same.
 * - A finger held still for `HOLD_MS`: counted here, since iOS Safari fires no `contextmenu` for a long press
 *   and Android Chrome does, so a touch's `contextmenu` over a target is held off and both phones ask once. A
 *   hold that lifted a sortable item is the lift's, and asks for nothing.
 */
import { ContextRequest, Point } from '../shared/value-types';

/** `ContextTarget.HoldMs`: how long a finger holds a target still to ask for its menu (cross-pinned with C#). */
export const HOLD_MS = 500;
/** `Touch.PressCancelSlop`: the travel past which a finger is scrolling, not holding (cross-pinned with C#). */
const SLOP = 12;
/** What keeps its own menu inside a target: a field's copy and paste are the field's. */
const FIELD = 'input, textarea, select, [contenteditable="true"], [contenteditable=""]';

let installed = false;
/** When the keyboard last asked, so the browser's `contextmenu` that follows its keydown is not a second ask. */
let keyboardAskedAt = -Infinity;
/** The finger being watched for a hold, and the way to stop watching it. */
let watching: { stop(): void } | null = null;

export function installContextController(): void {
  if (installed || typeof document === 'undefined') return;
  installed = true;
  document.addEventListener('contextmenu', onContextMenu);
  document.addEventListener('keydown', onKeyDown);
  document.addEventListener('pointerdown', onPointerDown);
}

/** Test seam: removes the controller's listeners. */
export function resetContextController(): void {
  if (typeof document === 'undefined') return;
  installed = false;
  keyboardAskedAt = -Infinity;
  watching?.stop();
  watching = null;
  document.removeEventListener('contextmenu', onContextMenu);
  document.removeEventListener('keydown', onKeyDown);
  document.removeEventListener('pointerdown', onPointerDown);
}

/**
 * The innermost target around `element`, or null when there is none or it is disabled: a disabled target answers
 * nothing, and no target around it answers in its place.
 */
function targetOf(element: Element | null): HTMLElement | null {
  const target = element?.closest?.('[data-eq-context]') as HTMLElement | null;
  return target && target.getAttribute('data-eq-context') === 'on' ? target : null;
}

/** Tells the target where it was asked, in its own box, or with no point for the keyboard. */
function ask(target: HTMLElement, at: Point | null): void {
  target.dispatchEvent(new CustomEvent('eq-context-requested', { detail: new ContextRequest(at) }));
}

function pointIn(target: HTMLElement, x: number, y: number): Point {
  const box = target.getBoundingClientRect();
  return new Point(x - box.left, y - box.top);
}

function onContextMenu(ev: MouseEvent): void {
  const target = targetOf(ev.target as Element | null);
  if (!target || (ev.target as Element).closest?.(FIELD)) return;
  ev.preventDefault();
  // A finger's long press is the hold's to answer, and the keyboard's was answered at its keydown.
  if ((ev as PointerEvent).pointerType === 'touch') return;
  if (performance.now() - keyboardAskedAt < 500) return;
  const byPointer = ev.button === 2 || ev.ctrlKey;
  ask(target, byPointer ? pointIn(target, ev.clientX, ev.clientY) : null);
}

function onKeyDown(ev: KeyboardEvent): void {
  const menuKey = ev.key === 'ContextMenu' && !ev.shiftKey && !ev.ctrlKey && !ev.metaKey && !ev.altKey;
  const shiftF10 = ev.key === 'F10' && ev.shiftKey && !ev.ctrlKey && !ev.metaKey && !ev.altKey;
  if ((!menuKey && !shiftF10) || ev.defaultPrevented) return;
  const target = targetOf(document.activeElement);
  if (!target || document.activeElement?.closest(FIELD)) return;
  ev.preventDefault();
  keyboardAskedAt = performance.now();
  ask(target, null);
}

function onPointerDown(down: PointerEvent): void {
  if (down.pointerType !== 'touch') return;
  const target = targetOf(down.target as Element | null);
  if (!target || (down.target as Element).closest?.(FIELD)) return;
  watching?.stop();
  const host = document;
  const pressed = down.target as Element;
  const startX = down.clientX;
  const startY = down.clientY;
  const timer = setTimeout(() => {
    stop();
    // A hold that lifted a sortable item is the lift's (`dom/sortable.ts`, 250 ms).
    if (pressed.closest?.('[data-eq-sort-lifted]')) return;
    ask(target, pointIn(target, startX, startY));
    // The press the hold began is not a tap: the click its release ends in is the menu's.
    host.addEventListener('click', swallow, { capture: true, once: true });
    setTimeout(() => host.removeEventListener('click', swallow, { capture: true }), 600);
  }, HOLD_MS);
  const move = (ev: Event): void => {
    const pointer = ev as PointerEvent;
    if (pointer.pointerId !== down.pointerId) return;
    if (Math.hypot(pointer.clientX - startX, pointer.clientY - startY) > SLOP) stop();
  };
  const end = (ev: Event): void => {
    if ((ev as PointerEvent).pointerId === down.pointerId) stop();
  };
  const stop = (): void => {
    clearTimeout(timer);
    host.removeEventListener('pointermove', move);
    host.removeEventListener('pointerup', end);
    host.removeEventListener('pointercancel', end);
    if (watching === watch) watching = null;
  };
  const watch = { stop };
  watching = watch;
  host.addEventListener('pointermove', move);
  host.addEventListener('pointerup', end);
  host.addEventListener('pointercancel', end);
}

function swallow(ev: Event): void {
  ev.stopPropagation();
  ev.preventDefault();
}
