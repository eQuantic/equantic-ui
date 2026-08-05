/**
 * The web realization of C# `ITextClipboard` AS A SERVICE — what a page's own "Copy" button asks
 * for. Inside a text field the browser needs no help; a button copying a snippet does, and the
 * async Clipboard API is the only path a secure context offers. `read` stays null — reading the
 * clipboard prompts the user on the web, and a text field paste never comes through this service.
 */
export class WebTextClipboard {
  read(): string | null {
    return null;
  }

  write(text: string): void {
    void navigator.clipboard?.writeText(text).catch(() => {
      /* denied or unfocused — the copy simply does not happen */
    });
  }
}
