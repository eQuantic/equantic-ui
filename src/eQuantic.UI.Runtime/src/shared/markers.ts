/**
 * DOM markers the two halves of the runtime use to find each other.
 *
 * A marker is written by one half — the lowering, or the C# realizer during SSR — and read by
 * another, usually a browser-side reader that has to measure something the lowering could not know.
 * Nothing type-checks that pairing: an emitter writing `data-eq-pinned` and a reader querying
 * `[data-eq-sticky]` is a program that compiles, passes its tests and silently does nothing.
 *
 * That happened. The `Sticky` → `Pinned` rename moved both emitters and missed the reader, and the
 * anchor offset published `0px` on a real site for a whole release — every fragment link landing
 * behind the header, which is the exact bug the offset exists to prevent. The suite stayed green
 * because the reader's spec built its own fixture with the old attribute, so it tested the reader
 * against markup nothing emits.
 *
 * Names live here so a rename cannot reach one side and not the other. This module imports nothing:
 * `core/*` must never pull in `shared/lowering`, and a leaf constant is safe for anyone to read.
 * The C# side cannot import it, so `MarkerParityTests` pins these strings against the realizer.
 */

/** Marks scroll-anchored chrome (the `Pinned` node) so the anchor offset can measure it. */
export const PINNED_MARKER = 'data-eq-pinned';
