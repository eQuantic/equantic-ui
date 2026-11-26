/**
 * How one of the SDK's own gestures takes part in a pointer's gesture arena on the web (#1563), without importing
 * the arena: the gesture controllers (`pointer-gesture.ts`, `context-request.ts`, the table viewport) depend on this
 * small module, and `gesture-arena.ts`, which carries the arena and its router, fills it in when the first
 * `RawGestureDetector` is lowered. So a page with no detector loads no arena, and every built-in answers
 * `joinGestureArena` with null and goes on exactly as it did before the arena existed.
 */

/** A built-in's place in a pointer's arena: it asks for the pointer when it arms, or leaves when it gives up. */
export interface ArenaMembership {
  /** The gesture armed: it asks for the pointer, and wins it unless another member won it first. */
  accept(): void;
  /** The gesture gave up on its own (a hold that travelled, a pan the view refused): it leaves the arena. */
  reject(): void;
}

/** What a built-in joins with: the pointer's down, the element the gesture is on, and what to do when it lost. */
export type ArenaJoiner = (down: PointerEvent, element: Element, onRejected: () => void) => ArenaMembership | null;

let joiner: ArenaJoiner | null = null;

/**
 * Enters the gesture of `element` in the arena of `down`'s pointer, while the down is still being dispatched. The
 * answer is null when that pointer has no arena (no detector is under it, or none is in the document): the gesture
 * then owns its pointer as it always did. `onRejected` runs once if another member wins the pointer; the gesture
 * drops what it armed there.
 */
export function joinGestureArena(down: PointerEvent, element: Element, onRejected: () => void): ArenaMembership | null {
  return joiner ? joiner(down, element, onRejected) : null;
}

/** Called by the arena's controller when it installs, and with null when a test resets it. */
export function setArenaJoiner(next: ArenaJoiner | null): void {
  joiner = next;
}
