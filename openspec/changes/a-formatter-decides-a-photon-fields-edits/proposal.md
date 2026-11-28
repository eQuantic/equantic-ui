## Why

This change ships pull request C of #1966 (under #898), through sub-issues #1977 and #1978. Photon's host must
ask a field's formatter at every edit it funnels, a phone's whole new value turned into one edit. Its shells must
open the right keyboards, and reach the formatter's undo, redo and overwrite from their own keys, menu and gestures.

## What Changes

For a developer, a `TextInput(…, formatter: …)` now behaves on Photon, as it does on the web after B:

- **Edits:** every edit goes through the formatter, on macOS, Windows, iOS and Android. A composition is never
  formatted while composed.
- **Phones:** iOS and Android open a non-prose field with no suggestions, and an obscured field with its secure
  keyboard.
- **Undo, redo and overwrite:**
  - macOS gains Edit › Undo and Redo.
  - iOS's shake and three-finger gestures reach the formatter through an undo manager.
  - The keys, `Insert` included, reach it on every shell.
- **Obscure:** an obscured formatted field draws `•` for what was entered and its literals as they are.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `text-entry`: the web requirements of `a-formatter-decides-a-web-fields-edits` are stated for both targets, and
  a phone's whole new value reaches a formatter as one edit.
- `phone-keyboard`: a formatted field asks the phone's keyboard for no suggestions.

## Impact

- **Parts reached:** Native.Components (`PhotonHost`, `TextInputSession`, `EmitVisitor.Text`), the macOS, iOS,
  Android and Windows shells, the native tests, and the wiki's Photon text input page.
- **Public surface moves:** one `[EQX0001]` addition in Native.Components' `PublicAPI.Unshipped.txt`,
  `TextInputSession.Prose`, beside its `Obscure` and `Kind`. The developer surface does not move.
- **No break.**
- **Principles:** 4, 6 and 10 are followed. None is bent.
