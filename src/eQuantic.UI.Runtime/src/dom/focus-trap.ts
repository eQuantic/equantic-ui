/**
 * §10 — focus management for modal overlay layers (Dialog · BottomSheet · Drawer).
 *
 * The contract: `role=dialog` + `aria-modal` (emitted by both realizers), **Tab cycles inside**,
 * nothing behind is reachable, **initial focus** lands inside when the layer opens, and closing
 * **restores focus to the invoker — always, on every close path**. When the invoker is gone (the row
 * a dialog deleted is what opened it), the focus goes to the control that now stands at its place
 * among the controls Tab reaches, as WAI-ARIA's dialog pattern asks for a logical element (#1811).
 *
 * Traps are discovered from the DOM (`[data-eq-trap]`) after every reconciler pass, not declared
 * with a path: the marker attribute is byte-identical in the SSR and client lowerings, so
 * hydration compares equal and no path plumbing has to agree across the two producers. Identity
 * is the ELEMENT — a layer that survives a pass keeps its trap and its recorded invoker; a layer
 * that stops being marked (closed, unmounted, or reparented for its exit animation) is a close.
 *
 * One keydown serves the page, heard on its way back UP, plus a capture-phase `focusin` guard that
 * pulls focus back when it escapes by any route a Tab cycle cannot see — a click on the page behind,
 * a return from browser chrome, a programmatic focus. The keydown bubbles because the focused control
 * hears Tab first: a Tab it consumed (a code editor indenting) is not the trap's to cycle, which is the
 * question Photon's host asks its code target before it moves the focus. Heard in the capture phase,
 * before the editor, the trap took the Tab of a code editor at either end of a dialog, and the line
 * was never indented (#598).
 */

/** What CAN take focus inside a trap. `[data-eq-trap]` itself is excluded: it is the fallback. */
const FOCUSABLE =
  'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), ' +
  'textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

/** The layers trapped as of the last committed pass, in DOM order (topmost last). */
let active: Element[] = [];
/** Where focus was when each layer opened, and its place among the controls Tab reached then:
 * restored on its close, whatever caused the close. */
const invokers = new WeakMap<Element, { invoker: Element; place: number }>();
/**
 * The invoker the last close gave the focus back to, while it holds it: when it leaves the page in a
 * later pass (the confirm closed first, and the row went when the server answered), the control now
 * at its place takes the focus, as it would have had the row left in the closing pass.
 */
let returned: { invoker: Element; place: number } | null = null;
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

/**
 * The controls Tab reaches beneath the layers in `above`: those of the topmost marked layer that is
 * not one of them, or the page's when none is, never a control inside another marked layer. The
 * order is the document's, which is the order Tab walks and the order Photon lists its stops in.
 */
function focusablesBeneath(marked: Element[], above: readonly Element[]): HTMLElement[] {
  const holder = [...marked].reverse().find((layer) => !above.includes(layer)) ?? null;
  const scope: ParentNode = holder ?? document;
  return Array.from(scope.querySelectorAll<HTMLElement>(FOCUSABLE)).filter(
    (el) =>
      el.closest('[data-eq-trap]') === holder &&
      el.closest('[hidden], [aria-hidden="true"], [data-eq-exiting]') === null,
  );
}

function restore(layer: Element): void {
  const recorded = invokers.get(layer);
  invokers.delete(layer);
  if (!recorded) return;
  if (recorded.invoker.isConnected) {
    (recorded.invoker as HTMLElement).focus?.();
    returned = recorded;
    return;
  }
  focusPlace(recorded.place);
}

/** The invoker is gone (a row that removed itself is what opened the dialog): the control now at its
 * place takes the focus, the next row's, or the last one when it was the last. */
function focusPlace(place: number): void {
  if (place < 0) return;
  const beneath = focusablesBeneath(active, []);
  beneath[Math.min(place, beneath.length - 1)]?.focus();
}

/** The invoker given the focus back that left in a later pass, with nothing else focused since: the
 * control at its place takes it. Any other move of the focus ends the watch. */
function followReturned(): void {
  if (!returned) return;
  const focused = document.activeElement;
  if (focused === returned.invoker && returned.invoker.isConnected) return;
  const watched = returned;
  returned = null;
  if (!watched.invoker.isConnected && (focused === null || focused === document.body)) focusPlace(watched.place);
}

function onKeyDown(event: KeyboardEvent): void {
  if (event.key !== 'Tab' || event.defaultPrevented) return;
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
  window.addEventListener('keydown', onKeyDown);
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
      const place = focusablesBeneath(marked, opened).indexOf(invoker as HTMLElement);
      invokers.set(layer, { invoker, place });
    }
  }

  active = marked;
  if (opened.length > 0) install();

  // Restore before focusing into anything new: closing one dialog to open another must not leave
  // focus on the old invoker. Of layers that closed together, the bottommost's invoker is the one
  // still standing, as Photon gives back the outermost's: the others are only forgotten.
  followReturned();
  if (closed.length > 0) returned = null;
  for (const layer of closed.slice(1)) invokers.delete(layer);
  if (closed.length > 0) restore(closed[0]);
  const layer = topmost();
  if (layer && opened.includes(layer)) {
    // After the next frame, for the same reason the mount hook waits: a layer transitioning out of
    // `visibility:hidden` is not focusable until the style lands, and focus() on it does nothing.
    if (typeof requestAnimationFrame === 'function') requestAnimationFrame(() => focusInto(layer));
    else focusInto(layer);
  }
}

let commitScheduled = false;

/**
 * Reconciles the traps once the pass is WRITTEN: what calls this at the end of a pass runs before the
 * render manager writes the tree the pass produced, so a commit on the spot read the DOM the pass
 * before it had written. A dialog a render opened then took no focus and recorded no opener until some
 * later pass came, and one that came none never did (#1812). The microtask after the write is the
 * moment the in-view observers attach at, for the same reason.
 */
export function scheduleFocusTrapCommit(): void {
  if (typeof document === 'undefined' || commitScheduled) return;
  commitScheduled = true;
  const commit = () => {
    commitScheduled = false;
    commitFocusTraps();
  };
  if (typeof queueMicrotask === 'function') queueMicrotask(commit);
  else void Promise.resolve().then(commit);
}

/** Test seam: the layers a Tab would currently be trapped inside (topmost last). */
export function activeFocusTraps(): readonly Element[] {
  return active;
}

/** Test seam: forget every trap (jsdom re-mounts between specs). */
export function resetFocusTraps(): void {
  active = [];
  returned = null;
}
