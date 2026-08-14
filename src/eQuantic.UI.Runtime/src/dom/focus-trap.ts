/**
 * §10 — focus management for modal overlay layers (Dialog · BottomSheet · Drawer).
 *
 * The contract: `role=dialog` + `aria-modal` (emitted by both realizers), **Tab cycles inside**,
 * nothing behind is reachable, **initial focus** lands inside when the layer opens, and closing
 * **restores focus to the invoker — always, on every close path**.
 *
 * Traps are discovered from the DOM (`[data-eq-trap]`) after every reconciler pass, not declared
 * with a path: the marker attribute is byte-identical in the SSR and client lowerings, so
 * hydration compares equal and no path plumbing has to agree across the two producers. Identity
 * is the ELEMENT — a layer that survives a pass keeps its trap and its recorded invoker; a layer
 * that stops being marked (closed, unmounted, or reparented for its exit animation) is a close.
 *
 * One capture-phase keydown serves the page (the `Shortcut` controller's shape), plus a `focusin`
 * guard that pulls focus back when it escapes by any route a Tab cycle cannot see — a click on the
 * page behind, a return from browser chrome, a programmatic focus.
 */

/** What CAN take focus inside a trap. `[data-eq-trap]` itself is excluded: it is the fallback. */
const FOCUSABLE =
  'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), ' +
  'textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

/** The layers trapped as of the last committed pass, in DOM order (topmost last). */
let active: Element[] = [];
/** Where focus was when each layer opened — restored on its close, whatever caused the close. */
const invokers = new WeakMap<Element, Element>();
let installed = false;

/** The layer that owns the keyboard right now: the last marked layer in the document. */
function topmost(): Element | null {
  return active.length > 0 ? active[active.length - 1] : null;
}

/**
 * Deliberately NOT a geometry test: `offsetParent`/`getClientRects` would force a layout flush on
 * every Tab, and they answer "no" for everything outside a real browser, which would quietly turn
 * every trap into the empty-layer case in tests. Explicitly hidden subtrees are excluded by
 * attribute instead; a CLOSED layer never reaches here at all, because closing removes the marker.
 */
function focusablesIn(layer: Element): HTMLElement[] {
  return Array.from(layer.querySelectorAll<HTMLElement>(FOCUSABLE)).filter(
    (el) => el.closest('[hidden], [aria-hidden="true"], [data-eq-exiting]') === null,
  );
}

/** Initial focus (§10): the marked PREFERRED target first — a confirm dialog marks its SAFE
 * action, so Enter pressed on reflex cancels instead of destroying — then the first focusable,
 * then the layer itself (it carries tabindex=-1 for exactly this reason). An explicit Autofocus
 * inside WINS over all of it: it already moved focus by the time this runs, and stealing it back
 * would defeat the field the app asked for. */
function focusInto(layer: Element): void {
  if (layer.contains(document.activeElement)) return;
  const preferred = layer.querySelector<HTMLElement>('[data-eq-initial-focus]');
  const first = focusablesIn(layer)[0];
  (preferred ?? first ?? (layer as HTMLElement)).focus();
}

function restore(layer: Element): void {
  const invoker = invokers.get(layer);
  invokers.delete(layer);
  if (!invoker) return;
  // The invoker may itself be gone (a row that removed itself is what opened the dialog).
  if (!invoker.isConnected) return;
  (invoker as HTMLElement).focus?.();
}

function onKeyDown(event: KeyboardEvent): void {
  if (event.key !== 'Tab') return;
  const layer = topmost();
  if (!layer) return;

  const focusables = focusablesIn(layer);
  if (focusables.length === 0) {
    // Nothing to cycle: the layer holds the focus rather than letting Tab walk the page behind.
    event.preventDefault();
    (layer as HTMLElement).focus();
    return;
  }

  const first = focusables[0];
  const last = focusables[focusables.length - 1];
  const current = document.activeElement;

  if (!layer.contains(current)) {
    event.preventDefault();
    (event.shiftKey ? last : first).focus();
    return;
  }
  if (!event.shiftKey && current === last) {
    event.preventDefault();
    first.focus();
    return;
  }
  if (event.shiftKey && current === first) {
    event.preventDefault();
    last.focus();
  }
}

function onFocusIn(event: FocusEvent): void {
  const layer = topmost();
  if (!layer) return;
  const target = event.target as Element | null;
  if (target && !layer.contains(target)) focusInto(layer);
}

function install(): void {
  if (installed || typeof window === 'undefined') return;
  installed = true;
  window.addEventListener('keydown', onKeyDown, true);
  window.addEventListener('focusin', onFocusIn, true);
}

/**
 * Reconciles the trap set with the DOM — called right after a reconciler pass commits (the moment
 * `commitShortcuts` promotes its bindings). Newly marked layers record their invoker and take
 * focus; layers that stopped being marked restore it.
 */
export function commitFocusTraps(): void {
  if (typeof document === 'undefined') return;
  const marked = Array.from(document.querySelectorAll('[data-eq-trap]'));
  const opened = marked.filter((layer) => !active.includes(layer));
  const closed = active.filter((layer) => !marked.includes(layer));

  // The invoker is whatever had focus when the layer appeared — read BEFORE anything moves focus,
  // which is why the recording happens here and not inside focusInto.
  for (const layer of opened) {
    const invoker = document.activeElement;
    if (invoker && invoker !== document.body && !layer.contains(invoker)) {
      invokers.set(layer, invoker);
    }
  }

  active = marked;
  if (opened.length > 0) install();

  // Restore before focusing into anything new: closing one dialog to open another must not leave
  // focus on the old invoker.
  for (const layer of closed) restore(layer);
  const layer = topmost();
  if (layer && opened.includes(layer)) {
    // After the next frame, for the same reason the mount hook waits: a layer transitioning out of
    // `visibility:hidden` is not focusable until the style lands, and focus() on it does nothing.
    if (typeof requestAnimationFrame === 'function') requestAnimationFrame(() => focusInto(layer));
    else focusInto(layer);
  }
}

/** Test seam: the layers a Tab would currently be trapped inside (topmost last). */
export function activeFocusTraps(): readonly Element[] {
  return active;
}

/** Test seam: forget every trap (jsdom re-mounts between specs). */
export function resetFocusTraps(): void {
  active = [];
}
