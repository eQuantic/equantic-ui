/**
 * Where the keyboard is inside a composite whose focus stays on its one stop (a tree, a grid): the element its
 * `aria-activedescendant` names, when the composite holds it; otherwise nothing. The keyboard's requests that
 * belong to an item (its menu, #1532; its sort, #1533) start from here when the focus is on the composite.
 */
export function activeItemOf(focused: Element | null): Element | null {
  const id = focused?.getAttribute?.('aria-activedescendant');
  if (!id) return null;
  const item = document.getElementById(id);
  return item && focused!.contains(item) ? item : null;
}
