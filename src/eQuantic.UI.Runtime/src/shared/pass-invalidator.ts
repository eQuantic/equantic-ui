/**
 * The page a build in flight belongs to, as the function that builds it again: the invalidator of the
 * pass its render opened (`enterPass`, element-roots.ts), or null outside one. A leaf of its own, so
 * the measurer reaches it without importing the pass and everything the pass commits.
 */
let current: (() => void) | null = null;

export function passInvalidator(): (() => void) | null {
  return current;
}

export function setPassInvalidator(invalidator: (() => void) | null): void {
  current = invalidator;
}
