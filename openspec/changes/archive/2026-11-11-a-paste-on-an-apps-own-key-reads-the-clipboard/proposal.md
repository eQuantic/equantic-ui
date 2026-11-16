# Proposal

Closes #1028, a Bug under #601.

## Why

An app binds Paste to a key of its own (`CodeEditor.KeyBindings`, #1010). On Photon the key pastes, since the shell hands the keymap the platform's clipboard. On the web it pasted nothing: a page reads the clipboard only later, in the paste event the browser fires on its own paste keys (⌘V, Ctrl+V, Shift+Insert), or through `navigator.clipboard.readText()`, which is asynchronous and may first ask the person's permission. `ITextClipboard` read synchronously, so the code surface's clipboard answered null on any other key, and the key was claimed for nothing.

Edgar's decision on the issue: the contract reads asynchronously, as Flutter's `Clipboard.getData` does.

## What Changes

- `ITextClipboard.Read()` becomes `Task<string?> ReadAsync()`. `Write` stays synchronous: the web writes inside the key's own copy event. Photon's shells (macOS, iOS, Android, Windows) and the server's absent clipboard answer at once with `Task.FromResult`.
- The code editor's `CodeCommand.Paste`, the sheet's ⌘V and Photon's text field paste what the clipboard hands over when it arrives. A paste that lands after its key tells the views: the code editor raises `CodeEditorController.Pasted`, which the `CodeEditor` component answers by drawing again and telling the app the new text, and the sheet refreshes its views.
- On the web, the code surface's clipboard waits for the browser's paste event on a key the browser pastes on, which brings the text with no prompt, and reads through `navigator.clipboard.readText()` on any other key, as VS Code's web build does. A read the browser refused pastes nothing. The browser's own Paste service (`WebTextClipboard`) reads the same way, so a page's own Paste button can use it.
- A developer who implements `ITextClipboard` writes `ReadAsync` instead of `Read`; one who reads it awaits it. That is a break of the contract surface, written in the wiki's Upgrading page.

## Impact

The vocabulary (`eQuantic.UI.Primitives`), the code and sheet engines (`eQuantic.UI.Code`, `eQuantic.UI.Sheet`) and their transpiled twins in the runtime, the `CodeEditor` component, the web runtime's code surface and clipboard service, Photon's host and its four shells, and the server's absent capabilities. Public surface: `ITextClipboard.Read()` is removed and `ReadAsync()` added, `CodeEditorController.Pasted` added, and the Windows shell's `WindowsClipboard` follows the contract. The developer surface does not move.
