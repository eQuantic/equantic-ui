/**
 * The web half of the C# `RawGestureDetector` (#1563): one `GestureRouter` for the document, the transpiled twin of
 * the C# one Photon runs, so the two targets cannot disagree on who wins a pointer.
 *
 * - A pointer that goes down where a detector is hit (the browser's hit test, read by the `HitTestBehavior` rule the
 *   listeners use, `heardAt`) opens an arena. Its members, innermost first: each detector's recognizers, the SDK's
 *   own gestures under the point (the innermost `Pressable`, and each built-in that joins while the down is
 *   dispatched: a drag, a sort, a context hold, a table's pan), and, for a finger or a pen, the browser's own scroll.
 * - The arena opens in a capture-phase `pointerdown` on the document, before every controller's own delegate, and
 *   closes in a bubble-phase `pointerdown` on the window, which runs after every listener of the document, so the
 *   built-ins join an open arena. Not a microtask: one is run after each listener of a user's event.
 * - Moves, ups and cancels reach the recognizers by `pointerId`, in each detector's own box, before any built-in
 *   hears them; deadlines are one `setTimeout` to the router's next one.
 * - The browser's scroll is the one member the runtime cannot hold: it wins by panning, which it announces with
 *   `pointercancel`, a cancel that takes the pointer from every member. It is kept from starting only once a
 *   recognizer has won, by a non-passive `touchmove` prevented from then on; the selection a drag would start and
 *   the context menu are held off then too.
 * - A press that lost has the `click` the browser fires after its up swallowed.
 *
 * A pointer under no detector opens no arena, and a pointer whose recognizers all refused it lets the built-ins go
 * on as they did before: an app with no detector sees nothing change. A recognizer is dropped once its detector is
 * found out of the document, which is checked before the router hears anything, so it never hears a pointer again.
 */
import { GestureRouter } from '../shared/components/GestureRouter';
import type { GestureArenaEntry } from '../shared/components/GestureArenaEntry';
import type { GestureRecognizer } from '../shared/components/GestureRecognizer';
import type { GestureRecognizerFactory } from '../shared/components/GestureRecognizerFactory';
import { modifiersOf } from '../shared/css-values';
import type { GestureDispositionValue, PointerKindValue, PointerPhaseValue } from '../shared/enums.generated';
import { Point, PointerEvent as HeardPointer } from '../shared/value-types';
import { setArenaJoiner, type ArenaMembership } from './arena-membership';
import { clickGuards, type ClickGuard } from './click-guard';
import { holdSelection, releaseSelection } from './pointer-gesture';
import { heardAt } from './pointer-listener';

/** What the lowering marks a detector with. */
const DETECTOR = '[data-eq-gestures]';
/** What an enabled `Pressable` lowers to. */
const PRESSABLE = '.eq-pressable';
/** How long after the up the click of a press that lost is still swallowed (`followPointer`'s window). */
const CLICK_AFTER_UP_MS = 50;

/** How the controller asks a detector for the factories its latest build wrote: the lowering's handler fills it. */
export interface GestureRequest {
  gestures: readonly GestureRecognizerFactory[] | null;
}

/** One of the SDK's gestures in a pointer's arena, as the arena sees it and as its controller drives it. */
class BuiltIn implements ArenaMembership {
  /** What the arena told it, once. */
  outcome: 'accepted' | 'rejected' | null = null;
  private entry: GestureArenaEntry | null = null;
  /** What it asked for while the down was still being dispatched, before it had an entry. */
  private wanted: GestureDispositionValue | null = null;
  /** Whether a recognizer competes for the pointer: only then does its outcome count. */
  private counted = false;
  private closed = false;
  private told = false;

  constructor(
    readonly element: Element,
    private readonly onRejected: () => void,
    private readonly onResolved: () => void,
  ) {}

  acceptGesture(): void {
    if (this.outcome === null) this.outcome = 'accepted';
  }

  rejectGesture(): void {
    if (this.outcome !== null) return;
    this.outcome = 'rejected';
    this.tell();
  }

  accept(): void {
    this.resolve('accepted');
  }

  reject(): void {
    this.resolve('rejected');
  }

  /** The arena closed: the entry it resolves with, and whether its outcome counts. What it heard inside the close,
   * and what it asked for before it, are settled now. */
  close(entry: GestureArenaEntry | null, counted: boolean): void {
    this.entry = entry;
    this.counted = counted;
    this.closed = true;
    this.tell();
    if (this.wanted !== null) this.resolve(this.wanted);
  }

  private resolve(disposition: GestureDispositionValue): void {
    if (!this.closed) {
      this.wanted ??= disposition;
      return;
    }
    if (!this.counted || this.entry === null) return;
    this.entry.resolve(disposition);
    this.onResolved();
  }

  private tell(): void {
    if (!this.counted || this.told || this.outcome !== 'rejected') return;
    this.told = true;
    this.onRejected();
  }
}

/** One pointer's arena, from its down to its up or its cancel. */
interface Arena {
  readonly pointerId: number;
  readonly down: PointerEvent;
  readonly kind: PointerKindValue;
  /** The detectors hit at the down, innermost and topmost first. */
  readonly detectors: Element[];
  /** The innermost press under the down, or null. */
  readonly press: Element | null;
  /** What the browser would pan for a finger or a pen, or null when it cannot pan there (`touch-action: none`). */
  readonly scroll: Element | null;
  /** The built-ins that joined while the down was dispatched. */
  readonly builtIns: BuiltIn[];
  closed: boolean;
  /** Whether a recognizer took the pointer. */
  contested: boolean;
  /** Whether a recognizer won it. */
  won: boolean;
  /** The recognizers that joined it at its close. */
  recognizers: GestureRecognizer[];
  /** The document whose selection it holds, which it gives back once at its end, or null. */
  heldOn: Document | null;
  /** The guard on the click of the press that lost, or null: the arena controller's (#1889). */
  unswallow: ClickGuard | null;
}

let installed = false;
let router: GestureRouter | null = null;
const arenas = new Map<number, Arena>();
/** The guards on the clicks of presses that lost: the controller's, so its reset lets go of them (#1889). */
const guards = clickGuards();
/** The detectors the router keeps recognizers for, so one that left the document is forgotten. */
const known = new Set<Element>();
let deadline: { timer: ReturnType<typeof setTimeout>; due: number } | null = null;
/** The document the non-passive `touchmove` is on while a finger's arena is open. */
let touchHost: Document | null = null;

export function installGestureArena(): void {
  if (installed || typeof document === 'undefined') return;
  installed = true;
  document.addEventListener('pointerdown', onDown, true);
  document.addEventListener('pointermove', onMove, true);
  document.addEventListener('pointerup', onUp, true);
  document.addEventListener('pointercancel', onCancel, true);
  document.addEventListener('contextmenu', onMenu, true);
  window.addEventListener('pointerdown', onDispatched);
  setArenaJoiner(join);
}

/** Test seam: removes the controller's listeners and forgets every arena and recognizer. */
export function resetGestureArena(): void {
  if (typeof document === 'undefined') return;
  installed = false;
  for (const arena of [...arenas.values()]) {
    end(arena);
  }
  // No click guard outlives a reset, an ended arena's included: what the arena swallows stops now, not after the
  // timer of its up, which used to run after a spec's page was torn down (#1889).
  guards.release();
  arenas.clear();
  known.clear();
  router = null;
  if (deadline) clearTimeout(deadline.timer);
  deadline = null;
  setArenaJoiner(null);
  document.removeEventListener('pointerdown', onDown, true);
  document.removeEventListener('pointermove', onMove, true);
  document.removeEventListener('pointerup', onUp, true);
  document.removeEventListener('pointercancel', onCancel, true);
  document.removeEventListener('contextmenu', onMenu, true);
  window.removeEventListener('pointerdown', onDispatched);
}

function theRouter(): GestureRouter {
  router ??= new GestureRouter();
  return router;
}

function onDown(event: PointerEvent): void {
  const stale = arenas.get(event.pointerId);
  // An up the browser never delivered: that pointer's arena is cancelled before a new one opens.
  if (stale) finish(stale, event, 'cancel');
  forgetLeft();
  const detectors = heardAt(event.clientX, event.clientY).filter((element) => element.matches(DETECTOR));
  if (detectors.length === 0) return;
  const target = event.target instanceof Element ? event.target : null;
  const kind = kindOf(event);
  const arena: Arena = {
    pointerId: event.pointerId,
    down: event,
    kind,
    detectors,
    press: target?.closest(PRESSABLE) ?? null,
    scroll: kind === 'mouse' ? null : pannedBy(target),
    builtIns: [],
    closed: false,
    contested: false,
    won: false,
    recognizers: [],
    heldOn: null,
    unswallow: null,
  };
  arenas.set(event.pointerId, arena);
  // From the press, because a browser may decide at the touch's start whether its moves can be cancelled.
  if (kind !== 'mouse' && touchHost === null && target) {
    touchHost = target.ownerDocument;
    touchHost.addEventListener('touchmove', holdTouch, { passive: false });
  }
  // A listener that stopped the down's propagation keeps it from the window: the arena closes after the task.
  setTimeout(() => close(arena), 0);
}

/** The down reached the window: every listener of the document has heard it, and the arena closes. */
function onDispatched(event: PointerEvent): void {
  const arena = arenas.get(event.pointerId);
  if (arena && arena.down === event) close(arena);
}

function onMove(event: PointerEvent): void {
  const arena = arenas.get(event.pointerId);
  if (!arena) return;
  close(arena);
  if (!arena.contested) return;
  route(arena, event, 'move');
  settle();
}

function onUp(event: PointerEvent): void {
  const arena = arenas.get(event.pointerId);
  if (arena) finish(arena, event, 'up');
}

function onCancel(event: PointerEvent): void {
  const arena = arenas.get(event.pointerId);
  if (arena) finish(arena, event, 'cancel');
}

/** Once a recognizer won a pointer, the drag that follows opens no menu. */
function onMenu(event: Event): void {
  if (anyWon(false)) event.preventDefault();
}

/** Once a recognizer won a finger, the browser does not pan under it. */
function holdTouch(event: Event): void {
  if (event.cancelable && anyWon(true)) event.preventDefault();
}

function anyWon(touchOnly: boolean): boolean {
  for (const arena of arenas.values()) if (arena.won && (!touchOnly || arena.kind !== 'mouse')) return true;
  return false;
}

/** The pointer ended: the recognizers hear its up or its cancel, the arena is swept or cancelled, and it closes. */
function finish(arena: Arena, event: PointerEvent, phase: 'up' | 'cancel'): void {
  close(arena);
  if (arena.contested) route(arena, event, phase);
  else theRouter().route(heard(phase, event, null));
  end(arena);
  settle();
}

/**
 * The arena closes: each detector hands its recognizers what its latest build wrote, and the members join in hit
 * order, the innermost first, ties topmost first.
 */
function close(arena: Arena): void {
  if (arena.closed || arenas.get(arena.pointerId) !== arena) return;
  arena.closed = true;
  const slots: Slot[] = [];
  const recognizers = new Set<GestureRecognizer>();
  for (const detector of arena.detectors) {
    const kept = recognizersOf(detector);
    for (const recognizer of kept) recognizers.add(recognizer);
    slots.push({ element: detector, members: kept });
  }
  const builtIns = [...arena.builtIns];
  if (arena.press) builtIns.push(new BuiltIn(arena.press, () => swallowClick(arena), settle));
  if (arena.scroll) builtIns.push(new BuiltIn(arena.scroll, () => undefined, settle));
  for (const builtIn of builtIns) place(slots, { element: builtIn.element, members: [builtIn] });
  const members = slots.flatMap((slot) => slot.members);
  const entries = theRouter().down(heard('down', arena.down, null), members, inBox('down', arena.down));
  arena.contested = members.some((member, i) => recognizers.has(member as GestureRecognizer) && entries[i] !== null);
  arena.recognizers = members.filter((member, i): member is GestureRecognizer =>
    recognizers.has(member as GestureRecognizer) && entries[i] !== null);
  members.forEach((member, i) => {
    if (member instanceof BuiltIn) member.close(entries[i] ?? null, arena.contested);
  });
  // The press and the scroll took part in the arena; the press's loss is read at the up.
  arena.builtIns.splice(0, arena.builtIns.length, ...builtIns);
  settle();
}

/** A member and the element it is on. */
interface Slot {
  element: Element;
  members: readonly object[];
}

/** Puts `slot` before the first slot around it and after every slot inside it: innermost first. */
function place(slots: Slot[], slot: Slot): void {
  let at = slots.length;
  for (let i = 0; i < slots.length; i++) {
    const other = slots[i].element;
    if (other !== slot.element && other.contains(slot.element)) {
      at = i;
      break;
    }
  }
  for (let i = slots.length - 1; i >= 0; i--) {
    if (slot.element.contains(slots[i].element)) {
      at = Math.max(at, i + 1);
      break;
    }
  }
  slots.splice(at, 0, slot);
}

/** A built-in joins the arena of `down`'s pointer while the down is dispatched. */
function join(down: PointerEvent, element: Element, onRejected: () => void): ArenaMembership | null {
  const arena = arenas.get(down.pointerId);
  if (!arena || arena.closed || arena.down !== down) return null;
  const member = new BuiltIn(element, onRejected, settle);
  arena.builtIns.push(member);
  return member;
}

/** The detector's recognizers, kept by the router and handed what the detector's latest build wrote. */
function recognizersOf(detector: Element): GestureRecognizer[] {
  const request: GestureRequest = { gestures: null };
  detector.dispatchEvent(new CustomEvent<GestureRequest>('eq-gestures', { detail: request }));
  // A detector the client has not hydrated yet answers nothing, and recognizes nothing.
  if (request.gestures === null) return [];
  known.add(detector);
  return [...theRouter().recognizers(detector, request.gestures as GestureRecognizerFactory[])];
}

/** Every recognizer of the arena's detectors hears `event`, each with what its detector's latest build wrote. */
function route(arena: Arena, event: PointerEvent, phase: PointerPhaseValue): void {
  forgetLeft();
  for (const detector of arena.detectors) if (detector.isConnected) recognizersOf(detector);
  theRouter().route(heard(phase, event, null), inBox(phase, event));
}

/** A detector out of the document lets its recognizers go. */
function forgetLeft(): void {
  for (const detector of [...known]) {
    if (detector.isConnected) continue;
    known.delete(detector);
    router?.forget(detector);
  }
}

/**
 * After the router or a built-in moved: the deadlines asked for start counting, those due pass, a recognizer that
 * won is noted, and the next deadline is waited for.
 */
function settle(): void {
  if (!router) return;
  router.tick(performance.now());
  noteWinners();
  wait();
}

function wait(): void {
  if (deadline) clearTimeout(deadline.timer);
  deadline = null;
  const next = router?.nextDeadlineMs;
  if (next === null || next === undefined) return;
  const due = next;
  deadline = { timer: setTimeout(() => passed(due), Math.max(0, due - performance.now())), due };
}

/** A deadline is due: the recognizers that wait for it hear it with what their latest build wrote. */
function passed(due: number): void {
  deadline = null;
  // A detector that left the document while nothing else happened lets its recognizers go before any deadline passes.
  forgetLeft();
  if (!router) return;
  for (const arena of arenas.values()) {
    if (!arena.contested) continue;
    for (const detector of arena.detectors) if (detector.isConnected) recognizersOf(detector);
  }
  // A timer never fires early, and one faked by a test moves no clock: the deadline it waited for has passed.
  router.tick(Math.max(performance.now(), due));
  noteWinners();
  wait();
}

/**
 * A recognizer won a pointer when its arena is decided, no built-in won it, and one of its recognizers still follows
 * the pointer, which a recognizer that lost stops doing: from then on the browser's pan, the selection and the menu
 * are held off for that pointer. An arena every member left is decided with no winner, and holds nothing.
 */
function noteWinners(): void {
  for (const arena of arenas.values()) {
    if (!arena.closed || !arena.contested || arena.won || theRouter().arena.isOpen(arena.pointerId)) continue;
    if (arena.builtIns.some((builtIn) => builtIn.outcome === 'accepted')) continue;
    if (!arena.recognizers.some((recognizer) => recognizer.tracks(arena.pointerId))) continue;
    arena.won = true;
    arena.heldOn = arena.down.target instanceof Node ? arena.down.target.ownerDocument ?? document : document;
    holdSelection(arena.heldOn);
  }
}

/** The arena is over: what it held is given back. */
function end(arena: Arena): void {
  if (arenas.get(arena.pointerId) === arena) arenas.delete(arena.pointerId);
  const heldOn = arena.heldOn;
  arena.heldOn = null;
  if (heldOn) releaseSelection(heldOn);
  arena.unswallow?.expireAfter(CLICK_AFTER_UP_MS);
  arena.unswallow = null;
  if (touchHost && ![...arenas.values()].some((other) => other.kind !== 'mouse')) {
    touchHost.removeEventListener('touchmove', holdTouch);
    touchHost = null;
  }
}

/** The press lost: the click its release ends in presses nothing. */
function swallowClick(arena: Arena): void {
  const press = arena.press;
  const host = press?.ownerDocument;
  if (!press || !host || arena.unswallow) return;
  // The guard's time starts at the arena's end (`end`): the press can lose before its up.
  arena.unswallow = guards.arm(host, undefined, (event) => event.target instanceof Node && press.contains(event.target));
  // Lost after the pointer ended is not possible: the arena is decided at the latest at the up.
}

/**
 * What a finger's pan at `target` would scroll: the nearest scroll container around it, or the page. Null when the
 * page says no pan starts there (`touch-action: none` from the target up to that container): the browser never
 * takes such a pointer away.
 */
function pannedBy(target: Element | null): Element | null {
  if (!target) return null;
  const view = target.ownerDocument.defaultView;
  for (let at: Element | null = target; at; at = at.parentElement) {
    const style = view?.getComputedStyle(at);
    if (!style) break;
    if (style.touchAction === 'none') return null;
    if (scrolls(style.overflowY) || scrolls(style.overflowX)) return at;
  }
  return target.ownerDocument.scrollingElement ?? target.ownerDocument.documentElement;
}

function scrolls(overflow: string): boolean {
  return overflow === 'auto' || overflow === 'scroll';
}

/** The event as the router hears it: in the document's coordinates, or in `box`'s. */
function heard(phase: PointerPhaseValue, event: PointerEvent, box: Element | null): HeardPointer {
  const origin = box ? box.getBoundingClientRect() : null;
  return new HeardPointer(
    phase,
    new Point(event.clientX - (origin?.left ?? 0), event.clientY - (origin?.top ?? 0)),
    kindOf(event),
    phase === 'cancel' ? 0 : event.buttons,
    modifiersOf(event),
    event.pointerId,
    new Point(0, 0),
    event.timeStamp,
  );
}

/** The event in the box of the detector a recognizer is kept for. */
function inBox(phase: PointerPhaseValue, event: PointerEvent): (position: unknown) => HeardPointer {
  return (position: unknown) => heard(phase, event, position instanceof Element ? position : null);
}

function kindOf(event: PointerEvent): PointerKindValue {
  return event.pointerType === 'touch' ? 'touch' : event.pointerType === 'pen' ? 'stylus' : 'mouse';
}
