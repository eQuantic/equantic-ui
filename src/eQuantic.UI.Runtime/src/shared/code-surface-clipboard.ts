/**
 * The clipboard a code surface hands its keymap on the web (C# `ITextClipboard`), so Copy and Cut run on
 * whatever key an app binds them to, as VS Code's do.
 *
 * The browser owns the clipboard: a page writes it from a copy event, which `document.execCommand('copy')`
 * fires at once inside a key's handler, and the surface's copy handler puts on it what `write` was given
 * (`pending`). A write that no copy event took throws, so a cut copies first or deletes nothing: the
 * Clipboard API's promise answered after the text was gone, and a refused write deleted text it never
 * copied (Copilot's second round on #1027). A page reads the clipboard only in a paste event (#1028):
 * `read` answers null and notes that it was asked, and the surface leaves the key to the browser, whose
 * paste event brings the text, where the browser pastes on that key.
 */
export class CodeSurfaceClipboard {
  /** What the next copy event writes, while a write waits for it. */
  pending: string | null = null;
  /** Whether the copy event a write fired took the text. */
  written = false;
  /** Whether a command asked to read, which only the browser's paste event can answer. */
  pasteAsked = false;

  read(): string | null {
    this.pasteAsked = true;
    return null;
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
