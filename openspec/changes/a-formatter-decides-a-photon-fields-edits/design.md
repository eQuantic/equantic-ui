## Context

The third of the masked field's five changes (pull request C, #1977 and #1978, under #1966). The shared design is
`mask-engine-held-to-dotnet`'s `design.md`, and this change ships Photon's half of its decisions 5, 6 and 8. It needs
A (the vocabulary and the engine), and runs beside B; it MODIFIES the text-entry requirements B adds for the web, so
that each states both targets once both have shipped, and it is archived after B.

What Photon has today: `PhotonHost` owns editing, and every edit ends in `Commit(entry, value, caret)` through one of
its funnels (`TextInput`, `EditKey`, `SetMarkedText`/`CommitText`, `UpdateEditingValue`); an obscured field draws `•`
for every character; no shell has an undo item or an undo manager.

## Goals / Non-Goals

**Goals:** every funnel asks the formatter; a phone's whole value becomes one edit; the commands from every shell's
keys, macOS's Edit menu and iOS's undo manager; an obscured formatted field drawn with its literals; the phones'
keyboards open a non-prose field with no suggestions.

**Non-Goals:** the web (B); `SemanticNode.Description` (it ships with the masked field, D).

## Decisions

All are the shared design's, cited:

- **The funnels' table and the phone's whole value** (shared decision 5): common prefix and suffix, the deletion's
  direction from the carets, one character typed and more pasted, a composing value kept as sent.
- **The composition rule** (shared decision 5): `SetMarkedText` composes unformatted; `CommitText` is one typed edit.
- **The commands** (shared decision 8): `EditKey` takes the keys on every shell; the macOS shell's main menu gains
  Edit › Undo and Redo; `PhotonKeyboard`'s proxy answers `undoManager` on iOS, so the shake and the three-finger
  gestures arrive; Android's hardware `KEYCODE_INSERT` and `Ctrl+Z` reach `EditKey`.
- **The block caret**: Photon draws its own, the width of the character after it.
- **Obscure** (shared decision 6): `EmitVisitor.Text` draws `•` only where `IsLiteral` says no; copy, cut and the
  secure keyboards stay as they are; an obscured mask of digits is `NumberVariationPassword` on Android.
- **Not prose** (shared decision 5): `TextInputSession` carries it, and iOS (`UITextAutocorrectionType.No`) and Android
  (`TYPE_TEXT_FLAG_NO_SUGGESTIONS`) honour it.

## Risks / Trade-offs

The shared design's, for Photon: a phone keyboard that composes anyway shows its composition raw until it commits;
an obscured Photon field is not hidden from captures on iOS, a gap that predates this change (#1986).

## Open Questions

None. The owner's decisions are recorded in the shared design.
