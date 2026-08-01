/**
 * Spec S8 — the window-level shortcut controller behind the `Shortcut` node.
 *
 * Mounting IS the subscription: every lowering pass re-declares the bindings that are live, so an
 * unmounted subtree's chord simply stops being registered — app code never adds or removes
 * listeners. One capture-phase keydown listener serves the whole page; a matching chord gets
 * `preventDefault()` so ⌘K never reaches the browser's own search.
 */

export interface ShortcutBinding {
  /** The wire chord (`command+shift+k`) — the C# WebRealizer.ChordId twin. */
  chord: string;
  handler: () => void;
}

/** Bindings declared by the pass currently being lowered. */
let pending: ShortcutBinding[] = [];
/** Bindings the last COMPLETED pass declared — what the listener dispatches to. */
let active: ShortcutBinding[] = [];
let installed = false;

/** Called by `lowerShortcut` for each live binding. */
export function declareShortcut(binding: ShortcutBinding): void {
  pending.push(binding);
  install();
}

/**
 * Promotes the pass's declarations. Called when a lowering pass finishes: bindings from subtrees
 * that were NOT re-lowered drop out, which is how unmounting unsubscribes.
 */
export function commitShortcuts(): void {
  active = pending;
  pending = [];
}

/** Test seam: the bindings a keydown would currently reach. */
export function activeShortcuts(): readonly ShortcutBinding[] {
  return active;
}

/** The chord a KeyboardEvent represents, in the C# ChordId order. `command` is ⌘ OR Ctrl — the
 * platform command key, so one authored chord is right everywhere. */
export function chordOf(event: KeyboardEvent): string {
  const parts: string[] = [];
  if (event.metaKey || event.ctrlKey) parts.push('command');
  if (event.ctrlKey) parts.push('control');
  if (event.altKey) parts.push('alt');
  if (event.shiftKey) parts.push('shift');
  parts.push((event.key ?? '').toLowerCase());
  return parts.join('+');
}

/** Every chord spelling the event satisfies — `command` matches ⌘ and Ctrl, and a Ctrl press also
 * satisfies a literal `control` binding, so both authoring styles fire. */
function candidates(event: KeyboardEvent): string[] {
  const key = (event.key ?? '').toLowerCase();
  const tail = (event.altKey ? 'alt+' : '') + (event.shiftKey ? 'shift+' : '') + key;
  const out: string[] = [];
  if (event.metaKey || event.ctrlKey) out.push(`command+${tail}`);
  if (event.ctrlKey) out.push(`control+${tail}`);
  if (!event.metaKey && !event.ctrlKey) out.push(tail);
  return out;
}

function install(): void {
  if (installed || typeof window === 'undefined') return;
  installed = true;
  window.addEventListener(
    'keydown',
    (event: KeyboardEvent) => {
      if (active.length === 0) return;
      const spellings = candidates(event);
      let handled = false;
      // A LIFO walk: the most recently mounted binding (the dialog on top) wins the chord.
      for (let i = active.length - 1; i >= 0; i--) {
        if (!spellings.includes(active[i].chord)) continue;
        handled = true;
        active[i].handler();
        break;
      }
      if (handled) event.preventDefault();
    },
    true,
  );
}

/** Test seam: drop every binding and let the next pass rebuild them. */
export function resetShortcuts(): void {
  pending = [];
  active = [];
}
