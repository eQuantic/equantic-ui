/**
 * The element a URL fragment names, or nothing.
 *
 * Its own tiny module because BOTH halves of the bookmark story need it and they live in different
 * subtrees: the router scrolls to it after a navigation, and the anchor-offset pass re-scrolls to it
 * once the chrome has been measured. Two copies of a decode is two places to forget the guard.
 *
 * A fragment is USER INPUT — typed, pasted, crawled — so a malformed percent-escape like `#%` is not
 * exotic, and `decodeURIComponent` throws on it. Thrown from the router that would reject a
 * navigation mid-flight; thrown from the offset pass it would break the microtask every render
 * schedules. A fragment nobody can decode names no element, which is the same answer as a fragment
 * naming an element that is not there. Same shape as the consent cookie's guard.
 */
export function bookmarkTarget(hash: string, doc: Document): HTMLElement | null {
  if (hash.length <= 1) return null;
  let id: string;
  try {
    id = decodeURIComponent(hash.slice(1));
  } catch {
    return null;
  }
  return id ? doc.getElementById(id) : null;
}
