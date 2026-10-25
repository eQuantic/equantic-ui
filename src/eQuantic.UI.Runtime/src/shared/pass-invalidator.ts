/**
 * The page a build in flight belongs to, as the function that rebuilds it: the invalidator of the
 * reconciler pass the instance store opened (`enterPass`), or null outside one. A leaf of its own,
 * so the measurer reaches it without importing the store and everything the store commits.
 */
let current: (() => void) | null = null;

export function passInvalidator(): (() => void) | null {
  return current;
}

export function setPassInvalidator(invalidator: (() => void) | null): void {
  current = invalidator;
}
