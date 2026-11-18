/**
 * The web half of the C# `Listener` (#1562): every pointer event is hit-tested against the listeners under it, and
 * each one that hears it is told as `eq-pointer`, which the lowering turns into the C# callback.
 *
 * - The browser's own hit test says what is under the point (`elementsFromPoint`), and `listenerHits` decides,
 *   by each listener's behaviour, which of them hear: the rule Photon runs too, over the same cases.
 * - A pointer that went down on listeners is followed to its up or its cancel, by `pointerId`, wherever it goes.
 * - A mouse or a pen moving with nothing followed hovers the listeners under it; a finger never hovers.
 * - A wheel or a trackpad scrolls the listeners under it, its delta in dp toward the content's end.
 * - Every event carries its `timeStamp`, the milliseconds the browser saw it at on the document's monotonic clock
 *   (#1563), so a recognizer measures a velocity by the difference of two.
 *
 * It only listens: the document's handlers are passive and capture-phase, prevent nothing and stop nothing, so a
 * button inside a listener still presses and a scroll view still scrolls.
 */
import { modifiersOf } from '../shared/css-values';
import type { HitTestBehaviorValue, PointerKindValue, PointerPhaseValue } from '../shared/enums.generated';
import { Point, PointerEvent as HeardPointer } from '../shared/value-types';
import { listenerHits, type UnderEntry } from './listener-hits';

const LISTENER = '[data-eq-listener]';
/**
 * What answers the pointer whatever it paints: a control, a field, an editable, an element Tab reaches. An element
 * taken out of the Tab order (`tabindex="-1"`, a modal layer, a roving item) answers only as what it is: the
 * pressable among them carries `eq-pressable`, and a layer that holds nothing answers nothing, as on Photon.
 */
const ANSWERS =
  'button, a[href], input, textarea, select, [contenteditable=""], [contenteditable="true"], ' +
  '[tabindex]:not([tabindex="-1"]), .eq-pressable';
/** What draws by being what it is: an image, a vector (and what it holds), a canvas, a video, a frame. */
const DRAWS = 'img, picture, svg, svg *, canvas, video, iframe';
/** `ScrollView.WheelLine`'s web twin: how many dp a wheel's line is (table-viewports.ts measures the same). */
const WHEEL_LINE = 16;

let installed = false;
/** The listeners each pressed pointer went down on, and what its down held. */
const followed = new Map<number, { listeners: Element[]; buttons: number; kind: PointerKindValue }>();
/** The mouse's pointer, which a wheel (that carries none) is reported as. */
let mousePointer = 1;

export function installPointerListener(): void {
  if (installed || typeof document === 'undefined') return;
  installed = true;
  const passive = { capture: true, passive: true } as const;
  document.addEventListener('pointerdown', onDown, passive);
  document.addEventListener('pointermove', onMove, passive);
  document.addEventListener('pointerup', onUp, passive);
  document.addEventListener('pointercancel', onCancel, passive);
  document.addEventListener('wheel', onWheel, passive);
}

/** Test seam: removes the controller's listeners and forgets every followed pointer. */
export function resetPointerListener(): void {
  if (typeof document === 'undefined') return;
  installed = false;
  followed.clear();
  mousePointer = 1;
  const capture = { capture: true } as const;
  document.removeEventListener('pointerdown', onDown, capture);
  document.removeEventListener('pointermove', onMove, capture);
  document.removeEventListener('pointerup', onUp, capture);
  document.removeEventListener('pointercancel', onCancel, capture);
  document.removeEventListener('wheel', onWheel, capture);
}

function onDown(event: PointerEvent): void {
  if (event.pointerType === 'mouse') mousePointer = event.pointerId;
  const listeners = heardAt(event.clientX, event.clientY);
  followed.delete(event.pointerId);
  if (listeners.length === 0) return;
  const kind = kindOf(event);
  followed.set(event.pointerId, { listeners, buttons: event.buttons, kind });
  tell(listeners, 'down', event, kind, event.buttons, ORIGIN);
}

function onMove(event: PointerEvent): void {
  const pressed = followed.get(event.pointerId);
  if (pressed) {
    tell(pressed.listeners, 'move', event, pressed.kind, event.buttons, ORIGIN);
    return;
  }
  // A hover is a pointer passing over with nothing pressed: a finger never hovers, and a press that went down
  // outside every listener is followed by none of them.
  if (event.pointerType === 'touch' || event.buttons !== 0) return;
  const listeners = heardAt(event.clientX, event.clientY);
  if (listeners.length > 0) tell(listeners, 'hover', event, kindOf(event), 0, ORIGIN);
}

function onUp(event: PointerEvent): void {
  const pressed = followed.get(event.pointerId);
  if (!pressed) return;
  followed.delete(event.pointerId);
  tell(pressed.listeners, 'up', event, pressed.kind, event.buttons, ORIGIN);
}

function onCancel(event: PointerEvent): void {
  const pressed = followed.get(event.pointerId);
  if (!pressed) return;
  followed.delete(event.pointerId);
  tell(pressed.listeners, 'cancel', event, pressed.kind, 0, ORIGIN);
}

function onWheel(event: WheelEvent): void {
  const listeners = heardAt(event.clientX, event.clientY);
  if (listeners.length === 0) return;
  const unit = event.deltaMode === 1 ? WHEEL_LINE : event.deltaMode === 2 ? window.innerHeight : 1;
  const delta = new Point(event.deltaX * unit, event.deltaY * unit);
  const pointer = { pointerId: mousePointer, clientX: event.clientX, clientY: event.clientY } as const;
  tell(listeners, 'scroll', { ...pointer, ...modifierKeys(event), timeStamp: event.timeStamp }, 'mouse', 0, delta);
}

const ORIGIN = new Point(0, 0);

interface Pointed {
  pointerId: number;
  clientX: number;
  clientY: number;
  shiftKey?: boolean;
  altKey?: boolean;
  metaKey?: boolean;
  ctrlKey?: boolean;
  /** When the browser saw it, in milliseconds since the document's time origin: monotonic. */
  timeStamp: number;
}

function modifierKeys(event: WheelEvent): Pick<Pointed, 'shiftKey' | 'altKey' | 'metaKey' | 'ctrlKey'> {
  return { shiftKey: event.shiftKey, altKey: event.altKey, metaKey: event.metaKey, ctrlKey: event.ctrlKey };
}

/** Tells each listener still in the document, at the point in its own box. */
function tell(
  listeners: readonly Element[],
  phase: PointerPhaseValue,
  event: Pointed,
  kind: PointerKindValue,
  buttons: number,
  scrollDelta: Point,
): void {
  const modifiers = modifiersOf(event);
  for (const listener of listeners) {
    if (!listener.isConnected) continue;
    const box = listener.getBoundingClientRect();
    const heard = new HeardPointer(
      phase,
      new Point(event.clientX - box.left, event.clientY - box.top),
      kind,
      buttons,
      modifiers,
      event.pointerId,
      scrollDelta,
      event.timeStamp,
    );
    listener.dispatchEvent(new CustomEvent('eq-pointer', { detail: heard }));
  }
}

function kindOf(event: PointerEvent): PointerKindValue {
  return event.pointerType === 'touch' ? 'touch' : event.pointerType === 'pen' ? 'stylus' : 'mouse';
}

/**
 * The listeners that hear a pointer at (`x`, `y`), the latest painted first: the browser's hit test, read in paint
 * order (the bottom first), each element marked a listener with its behaviour or a solid when it draws or answers.
 */
export function heardAt(x: number, y: number): Element[] {
  if (typeof document.elementsFromPoint !== 'function' || !document.querySelector(LISTENER)) return [];
  const painted = document.elementsFromPoint(x, y).reverse();
  const under: UnderEntry[] = [];
  for (let i = 0; i < painted.length; i++) {
    const element = painted[i];
    let end = i + 1;
    while (end < painted.length && element.contains(painted[end])) end++;
    const listener = element.matches(LISTENER);
    under.push({
      start: i,
      end,
      solid: !listener && isSolid(element),
      behavior: listener ? behaviorOf(element) : null,
    });
  }
  return listenerHits(under).map((index) => painted[index]);
}

function behaviorOf(element: Element): HitTestBehaviorValue {
  const mark = element.getAttribute('data-eq-listener');
  return mark === 'opaque' ? 'opaque' : mark === 'translucent' ? 'translucent' : 'deferToChild';
}

/**
 * Whether `element` is hit, as Flutter counts a hit: it draws (a fill, a background image, a visible border, a
 * shadow, its own text, an image, a vector, a canvas) or it answers (a control, a field, a scroll container). A
 * layout's box that paints nothing, a column's gap or a padding, is not. A fill that paints nothing draws nothing:
 * an explicit transparent fill cannot be told from none, which is why `opaque` exists.
 */
export function isSolid(element: Element): boolean {
  if (element.matches(ANSWERS) || element.matches(DRAWS)) return true;
  for (const node of Array.from(element.childNodes)) {
    if (node.nodeType === 3 && (node.textContent ?? '').trim() !== '') return true;
  }
  const style = getComputedStyle(element);
  if (scrolls(style.overflowX) || scrolls(style.overflowY)) return true;
  if (visible(style.backgroundColor) || (style.backgroundImage && style.backgroundImage !== 'none')) return true;
  if (style.boxShadow && style.boxShadow !== 'none') return true;
  return borders(style);
}

function scrolls(overflow: string): boolean {
  return overflow === 'auto' || overflow === 'scroll';
}

function borders(style: CSSStyleDeclaration): boolean {
  for (const side of ['Top', 'Right', 'Bottom', 'Left'] as const) {
    const width = parseFloat(style.getPropertyValue(`border-${side.toLowerCase()}-width`));
    const kind = style.getPropertyValue(`border-${side.toLowerCase()}-style`);
    const color = style.getPropertyValue(`border-${side.toLowerCase()}-color`);
    if (width > 0 && kind !== '' && kind !== 'none' && kind !== 'hidden' && visible(color)) return true;
  }
  return false;
}

/** Whether a computed colour paints anything: not empty, not `transparent`, and not at zero alpha. */
function visible(color: string | null | undefined): boolean {
  if (!color || color === 'transparent') return false;
  const channels = /^rgba?\(([^)]*)\)$/.exec(color.trim());
  if (!channels) return true;
  // `rgb(r, g, b)` has no alpha; `rgba(r, g, b, a)` and `rgb(r g b / a)` carry it last.
  const parts = channels[1].split(/[\s,/]+/).filter((part) => part !== '');
  if (parts.length < 4) return true;
  const alpha = parts[3];
  return (alpha.endsWith('%') ? parseFloat(alpha) / 100 : parseFloat(alpha)) > 0;
}
