import { readClipboardText } from './devices/text-clipboard';

/**
 * The clipboard a code surface hands its keymap on the web (C# `ITextClipboard`), so Copy and Cut run on
 * whatever key an app binds them to, as VS Code's do.
 *
 * The browser owns the clipboard: a page writes it from a copy event, which `document.execCommand('copy')`
 * fires at once inside a key's handler, and the surface's copy handler puts on it what `write` was given
 * (`pending`). A write that no copy event took throws, so a cut copies first or deletes nothing: the
 * Clipboard API's promise answered after the text was gone, and a refused write deleted text it never
 * copied (Copilot's second round on #1027). A read answers later, as C#'s `ReadAsync` does (#1028): on a
 * key the browser pastes on, the surface leaves the key to the browser and the read waits for its paste
 * event, which brings the text with no prompt; on any other key it reads through the Clipboard API, as VS
 * Code's web build does, which may first ask the person's permission.
 */
export class CodeSurfaceClipboard {
  /** What the next copy event writes, while a write waits for it. */
  pending: string | null = null;
  /** Whether the copy event a write fired took the text. */
  written = false;
  /** Whether the key being handled is one the browser pastes on, which the surface says before the keymap
   *  runs. */
  browserPastes = false;
  /** Whether a command asked to read on such a key, so the key goes on to the browser's paste event. */
  pasteAsked = false;
  /** The read that waits for the browser's paste event. */
  private waiting: ((text: string | null) => void) | null = null;

  readAsync(): Promise<string | null> {
    if (!this.browserPastes) return readClipboardText();
    this.pasteAsked = true;
    // A read still waiting found no paste event: the browser took the key for nothing.
    this.answer(null);
    return new Promise((resolve) => {
      this.waiting = resolve;
    });
  }

  /** Hands a read that waits for the browser's paste event what that event brought; false when none waits,
   *  and the paste is then the surface's own (a menu's Paste, a key no command claimed). */
  answer(text: string | null): boolean {
    const waiting = this.waiting;
    if (waiting === null) return false;
    this.waiting = null;
    waiting(text);
    return true;
  }

  write(text: string): void {
    this.pending = text;
    this.written = false;
    try {
      // Where there is no execCommand, no copy event comes, and the write is refused like any other.
      if (typeof document !== 'undefined' && typeof document.execCommand === 'function') document.execCommand('copy');
    } finally {
      this.pending = null;
    }
    if (!this.written) throw new ClipboardRefused();
  }
}

/** A write the browser's copy event did not take: nothing reached the clipboard. */
export class ClipboardRefused extends Error {
  constructor() {
    super('The browser took nothing to the clipboard.');
    this.name = 'ClipboardRefused';
  }
}

/** Whether the browser pastes on this key: V with ⌘ or Ctrl (Shift too, as plain text), and off a Mac
 *  Shift+Insert. */
export function browserPastesOn(event: KeyboardEvent, convention: 'apple' | 'standard'): boolean {
  const key = event.key.length === 1 ? event.key.toLowerCase() : event.key;
  if ((event.metaKey || event.ctrlKey) && !event.altKey && key === 'v') return true;
  return convention === 'standard' && key === 'Insert' && event.shiftKey && !event.ctrlKey && !event.altKey;
}
