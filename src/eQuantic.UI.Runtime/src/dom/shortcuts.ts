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
  /**
   * Whether the subtree that declared the binding is on screen NOW; absent means always. The web
   * mounts every arm of an AdaptiveNode and the window's width shows one, so a binding declared in
   * another arm is mounted and invisible — where Photon never lays that arm out at all. Asked at
   * the keypress, because a resize flips it without a render.
   */
  live?: () => boolean;
}

/** Bindings declared by the pass currently being lowered. */
let pending: ShortcutBinding[] = [];
/** The keydowns a binding took (see {@link claimedByShortcut}). */
const claimed = new WeakSet<Event>();
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
      // A LIFO walk: the most recently mounted binding (the dialog on top) wins the chord — among
      // the ones on screen. A binding in a hidden arm sits the chord out, so it reaches the arm the
      // width shows, and when no arm that binds it is shown the browser keeps the key.
      for (let i = active.length - 1; i >= 0; i--) {
        const binding = active[i];
        if (!spellings.includes(binding.chord)) continue;
        if (binding.live && !binding.live()) continue;
        handled = true;
        binding.handler();
        break;
      }
      if (handled) {
        claimed.add(event);
        event.preventDefault();
      }
    },
    true,
  );
}

/**
 * Whether a binding took this keydown. A key a Shortcut took reaches nothing else, which is what the
 * C# twin does (PhotonHost.KeyDown asks the shortcuts first and stops there): the reconciler drops
 * it before any element's own keydown, since this listener runs in the capture phase and theirs
 * after it.
 */
export function claimedByShortcut(event: Event): boolean {
  return claimed.has(event);
}

/** Test seam: drop every binding and let the next pass rebuild them. */
export function resetShortcuts(): void {
  pending = [];
  active = [];
}
