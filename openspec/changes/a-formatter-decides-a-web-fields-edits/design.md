## Context

The second of the masked field's five changes (pull request B, #1975 and #1976, under #1966). The shared design is
`mask-engine-held-to-dotnet`'s `design.md` (archived under `openspec/changes/archive/` once A merges), and this
change ships the web's half of its decisions 5, 6 and 8. It needs A: the vocabulary (`ITextInputFormatter`,
`TextEdit`, `TextEditKind`, `TextCommand`, `TextEntry.Formatter`) and an engine to prove it with. It is proved with a
test formatter, so it does not wait for `MaskedInput`.

What the web has today: `lowerTextEntry` (`lowering.ts`) and its C# twin `WebLoweringVisitor.LowerTextEntry` lower a
`TextEntry` to a real `<input>`; the browser owns the caret, the selection and the IME, and the runtime listens to
`input` only.

## Goals / Non-Goals

**Goals:** every edit of a formatted web field decided by its formatter, caret included; the composition rule; the
formatter's undo, redo and Insert reached from the keys and from the browser's own menus and gestures; an obscured
formatted field that shows its literals over a real password input.

**Non-Goals:** Photon (`a-formatter-decides-a-photon-fields-edits`); the masked field itself
(`field-takes-the-shape-of-its-mask`).

## Decisions

All are the shared design's, cited, with what this change adds to them:

- **The `beforeinput` handler** (shared decision 5): the table of input types, the range from the selection
  (`getTargetRanges()` is empty for an `<input>`, measured), the write through `execCommand('insertText')` under a
  guard, `setSelectionRange`, and `onChanged` called by the runtime, since a cancelled `beforeinput` fires no
  `input`. Where `execCommand` is absent, the field assigns `value` and only the menu and gesture undo is lost.
- **The composition rule** (shared decision 5): state kept at `compositionstart`, the committed text applied once at
  `compositionend`, Firefox's trailing `input` ignored.
- **The commands** (shared decision 8): `keydown` `Ctrl`/`Cmd+Z`, `Ctrl`/`Cmd+Shift+Z`, `Ctrl+Y` and `Insert`, and
  `historyUndo`/`historyRedo`, cancelled and handed to the formatter; `caret-shape: block` while it overwrites
  (Chrome 157 has it, Firefox 159 does not, measured).
- **The overlay of literals over a password input** (shared decision 6, option A): the literal at its index times the
  bullet width measured from the input, the input's `scroll` observed, `aria-hidden` and `pointer-events: none`.
- **Not prose**: a formatted entry carries `spellcheck="false"`, `autocapitalize="none"` and `autocorrect="off"`, in
  the C# lowering and its twin alike, so the parity fixture sees it.

## Risks / Trade-offs

The shared design's, for the web: `execCommand` is deprecated (the fallback above); the browser's Redo menu item
stays disabled after an undo the engine made (measured: no `historyRedo` once an undo was cancelled); an obscured
field's literals stand in bullet cells (accepted, the owner's decision 1).

## Open Questions

None. The owner's decisions are recorded in the shared design.
