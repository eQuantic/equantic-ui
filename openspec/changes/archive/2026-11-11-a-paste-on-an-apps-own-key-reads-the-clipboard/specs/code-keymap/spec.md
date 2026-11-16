## MODIFIED Requirements

### Requirement: The clipboard on the web

On the web, `CodeCommand.Copy` and `CodeCommand.Cut` SHALL write through the browser's own copy event, which
the key's handler fires (`document.execCommand('copy')`), on whatever key they are bound to, as VS Code's
do, and the command bound to the key SHALL decide, never the key. A cut SHALL delete only what that event
took to the clipboard, and a write the browser refused SHALL leave the key to the browser. A paste SHALL
read the clipboard after its key, through `ITextClipboard.ReadAsync`: on a key the browser pastes on, the
key goes on to the browser and the paste waits for its paste event, which brings the text, and on any other
key an app binds to `CodeCommand.Paste` it reads through `navigator.clipboard.readText()`, which may first
ask the person's permission. The text SHALL be pasted when it arrives, drawn, and heard by the app as an
edit, and a read the browser refused SHALL paste nothing.

#### Scenario: Copy on a key of the app's own

- **WHEN** an app binds ⌘D to `CodeCommand.Copy` on the web, selects the whole document, and presses ⌘D
- **THEN** the browser's copy event takes the text to the clipboard, and the editor claims the key

#### Scenario: Copy on the browser's cut key

- **WHEN** an app binds ⌘X to `CodeCommand.Copy` on the web and presses it over a selection
- **THEN** the text is copied, and nothing is deleted

#### Scenario: A cut the browser takes no copy for

- **WHEN** a key bound to `CodeCommand.Cut` is pressed on the web and the browser fires no copy event
- **THEN** nothing is deleted, and the editor does not claim the key

#### Scenario: Paste on ⌘V

- **WHEN** ⌘V is pressed on the web over the whole document selected, and the browser's paste event brings
  `pasted`
- **THEN** the editor does not claim the key, the document is `pasted`, nothing is asked of the Clipboard API,
  and the app hears `pasted`

#### Scenario: Paste on a key of the app's own

- **WHEN** an app binds ⌘J to `CodeCommand.Paste` on the web, and ⌘J is pressed at the end of `pasted` while
  `navigator.clipboard.readText()` answers ` and read`
- **THEN** the editor claims the key, the document is `pasted and read` once the read answers, and the app
  hears it

#### Scenario: A read the browser refused

- **WHEN** a key bound to `CodeCommand.Paste` is pressed on the web and `navigator.clipboard.readText()` is
  refused
- **THEN** the editor claims the key, and the document is as it was

#### Scenario: A clipboard that answers later

- **WHEN** `CodeCommand.Paste` runs at the `n` of `one` with a clipboard whose read has not answered yet, and it
  then answers `x`
- **THEN** the document is `one` until it answers and `oxne` after, and `CodeEditorController.Pasted` is raised
  once, after the text is in
