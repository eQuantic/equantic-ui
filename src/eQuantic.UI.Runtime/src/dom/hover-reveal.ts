/**
 * WCAG 1.4.13 for the hover/focus-revealed panel (the Tooltip mechanism): content that appears on
 * hover or focus must be DISMISSABLE without moving the pointer or the focus — Escape hides it.
 *
 * The reveal itself is pure CSS (`.eq-hoverreveal:hover` / `:has(:focus-visible)`), so there is no
 * component state to flip; suppression is a CLASS the generated stylesheet turns into `opacity: 0
 * !important`, and it lasts exactly as long as the trigger condition that revealed the panel:
 * leaving the host (pointer out, focus out) clears it, so the NEXT hover or focus reveals again —
 * which is the WCAG contract, not a quirk. Esc is a "not now", never a "never again".
 *
 * One capture-phase keydown serves the page (the focus-trap controller's shape). It never consumes
 * the event: a dialog's own Escape must still close the dialog — hiding a tooltip is not an answer
 * to Escape, it is a side effect of it.
 */

const HOST = '.eq-hoverreveal';
const SUPPRESSED = 'eq-reveal-suppressed';

let installed = false;

/** The hosts whose panel is currently revealed — hovered, or holding keyboard focus. `:hover`
 * answers only in a real browser; the focus half answers everywhere, tests included. */
function revealedHosts(): Element[] {
  const hosts: Element[] = [];
  for (const host of document.querySelectorAll(HOST)) {
    let revealed = false;
    try {
      revealed = host.matches(':hover');
    } catch {
      // Selector support varies off-browser; the focus check below still answers.
    }
    if (!revealed && document.activeElement && host.contains(document.activeElement))
      revealed = true;
    if (revealed) hosts.push(host);
  }
  return hosts;
}

function onKeyDown(event: KeyboardEvent): void {
  if (event.key !== 'Escape') return;
  for (const host of revealedHosts()) {
    host.classList.add(SUPPRESSED);
    // The suppression ends when the CONDITION that revealed the panel ends — one listener per
    // suppression, self-removing, so a host suppressed twice never stacks dead listeners.
    const clear = () => {
      host.classList.remove(SUPPRESSED);
      host.removeEventListener('mouseleave', clear);
      host.removeEventListener('focusout', clear);
    };
    host.addEventListener('mouseleave', clear);
    host.addEventListener('focusout', clear);
  }
}

/** Installs the page-wide Escape listener once — called from the commit path, like the traps. */
export function installHoverRevealSuppression(): void {
  if (installed || typeof document === 'undefined') return;
  installed = true;
  document.addEventListener('keydown', onKeyDown, true);
}

/** Test seam: uninstall and forget, so each spec starts clean. */
export function resetHoverRevealSuppression(): void {
  if (installed) document.removeEventListener('keydown', onKeyDown, true);
  installed = false;
}
