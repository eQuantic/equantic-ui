/**
 * The web realization of C# `ITextClipboard` AS A SERVICE — what a page's own "Copy" and "Paste" buttons
 * ask for. Inside a text field the browser needs no help; a button does, and the async Clipboard API is the
 * only path a secure context offers. A read may first ask the person's permission, and answers null where
 * they refused it or the page has no Clipboard API (#1028).
 */
export class WebTextClipboard {
  readAsync(): Promise<string | null> {
    return readClipboardText();
  }

  write(text: string): void {
    void navigator.clipboard?.writeText(text).catch(() => {
      /* denied or unfocused — the copy simply does not happen */
    });
  }
}

/**
 * What the clipboard holds as text, through `navigator.clipboard.readText()`, which may first ask the
 * person's permission: null where they refused it, the page is not focused or has no Clipboard API, as
 * Flutter's `Clipboard.getData` answers null for no text (#1028).
 */
export function readClipboardText(): Promise<string | null> {
  const clipboard = typeof navigator === 'undefined' ? undefined : navigator.clipboard;
  if (typeof clipboard?.readText !== 'function') return Promise.resolve(null);
  return clipboard.readText().then(
    (text) => text,
    () => null,
  );
}
