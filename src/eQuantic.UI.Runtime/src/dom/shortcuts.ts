/**
 * Spec S8 — the window-level shortcut controller behind the `Shortcut` node.
 *
 * Mounting IS the subscription: every lowering pass re-declares the bindings that are live, so an
 * unmounted subtree's chord simply stops being registered — app code never adds or removes
 * listeners. One capture-phase keydown listener serves the whole page; a matching chord gets
 * `preventDefault()` so ⌘K never reaches the browser's own search.
 */

import { openRecord } from '../shared/lowered-records';

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
  /**
   * The component lowering that declared it (`lowered-records.ts`), set as it is declared: a render
   * that lowers some components alone replaces the bindings they declared and keeps the rest (#1455).
   */
  within?: object | null;
}

/** Bindings declared by the pass currently being lowered. */
let pending: ShortcutBinding[] = [];
/** The keydowns a binding took (see {@link claimedByShortcut}). */
const claimed = new WeakSet<Event>();
/** Bindings the last COMPLETED pass declared — what the listener dispatches to. */
let active: ShortcutBinding[] = [];
let installed = false;

/**
 * Where a binding declared now would be listed. A `Shortcut` asks before its child lowers and
 * declares there afterwards, so a binding is listed before the ones inside it, as Photon's frame
 * lists them, and the LIFO walk reaches the inner one first: a component's own Escape inside a
 * dialog answers before the dialog's, as the Shortcuts nearest the focus do in Flutter.
 */
export function shortcutMark(): number {
  return pending.length;
}

/** Called by `lowerShortcut` for each live binding, at the place it took with {@link shortcutMark}
 * (the end of the list by default). */
export function declareShortcut(binding: ShortcutBinding, at = pending.length): void {
  binding.within = openRecord();
  pending.splice(Math.min(at, pending.length), 0, binding);
  install();
}

/**
 * Which of the components a pass lowered alone declared a binding: one of them, or undefined for a
 * binding none of them declared (#1455).
 */
export type ShortcutDeclarer = (binding: ShortcutBinding) => object | undefined;

/**
 * Promotes the pass's declarations. Called when a lowering pass finishes: bindings from subtrees
 * that were NOT re-lowered drop out, which is how unmounting unsubscribes.
 */
export function commitShortcuts(declaredBy?: ShortcutDeclarer): void {
  if (!declaredBy) {
    active = pending;
    pending = [];
    return;
  }
  // A pass that lowered some components alone (#1455): the bindings EACH of them declared before give
  // way to the ones it declared now, in the place its own held, and every other binding stays. A whole
  // pass lists a component's bindings together, where its subtree lowered, so each component is put
  // back in its own place: two put back at the first one's place would move the bindings between them
  // after both, and the later binding is the one a keystroke reaches first.
  const declared = new Map<object, ShortcutBinding[]>();
  for (const binding of pending) {
    const declarer = declaredBy(binding);
    if (declarer) declared.set(declarer, [...(declared.get(declarer) ?? []), binding]);
  }
  const listed: ShortcutBinding[] = [];
  const placed = new Set<object>();
  for (const binding of active) {
    const declarer = declaredBy(binding);
    if (!declarer) listed.push(binding);
    else if (!placed.has(declarer)) {
      placed.add(declarer);
      listed.push(...(declared.get(declarer) ?? []));
    }
  }
  // A component that had no binding before has no place in the list for one it declares now: only a
  // whole pass can say where it goes, so the caller asked first (`partialCommitIsExact`) and renders
  // whole when it cannot.
  active = listed;
  pending = [];
}

/**
 * Whether a partial commit (`commitShortcuts(declaredBy)`) would list the bindings where a whole pass
 * lists them: it would, unless one of the components lowered again declares bindings now and had none
 * before, since there is no place of its own to put them in.
 */
export function partialCommitIsExact(declaredBy: ShortcutDeclarer): boolean {
  return pending.every((binding) => {
    const declarer = declaredBy(binding);
    return declarer !== undefined && active.some((old) => declaredBy(old) === declarer);
  });
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
      // A LIFO walk: the last binding listed wins the chord, among the ones on screen: of two
      // siblings the later one (the dialog on top), of two nested ones the inner one. A binding in
      // a hidden arm sits the chord out, so it reaches the arm the width shows, and when no arm
      // that binds it is shown the browser keeps the key.
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
