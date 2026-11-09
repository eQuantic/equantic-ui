# Proposal

Closes #996, a task of #937 (the Element tree: one instance per position, on every target).

## Why

A Photon field draws the value the app hands it. So a key shows only once something builds the
component that holds the value, and the host cannot see which component that is: a form can keep the
value without a `SetState`, and `TextInput` keeps state of its own (its focus) and holds the field but
not the value. Since #976 the host answers that safely and expensively: the frame after a field's
handler that called no `SetState` rebuilds every component, as every frame after input did before the
host found controls by their position. It is the one input that still rebuilds the whole tree.

Flutter's answer is the structural one: `EditableText` keeps what was typed in its controller, and the
app's value reaches the controller only when the field's parent configures it again. The browser does the same
with an input's live value, and #1026 settles what the web shows after a render: the value the page
holds, whether or not it changed since the last render (a page that reset a field in the same render as
the typing that filled it kept showing the typed digits, on eQuantic.Auth's sign-in).

This issue's text proposed taking the app's value "only when the app hands a different one". That is
the rule #1026 found wrong: a reset to the value the page already held before the typing is not a
different one, and the typed text stayed. This change takes the rule #1026 settled, so the two targets
agree.

## What Changes

A Photon field's position keeps what was typed, and shows it until the page builds the field again,
when it shows what the page holds.

For a developer using the SDK:

- A key typed into a field shows at once, whatever the handler does with it, and the frame after it
  builds only what the handler marked with `SetState`. A form that keeps the value without `SetState`
  no longer rebuilds every component on every key.
- When the page builds the field again, the field shows the value the page hands it: a formatter's
  output, a reset (even to the value the page held before the typing), or the old value of a handler
  that rejected the key and called `SetState`. This is what the web does after #1026.
- A handler that rejects a key by ignoring it (no `SetState`) leaves the key showing until the page
  builds the field again, as an input does in a browser. To put the field back, the handler calls
  `SetState`. This is the one migration line.

On Photon, the layout's measure, the paint walk, the semantics walk and the host (the caret, the
selection, the word and line moves, a click's caret, a triple click and the phone keyboard's editing
value) read the field's text from its position, through one function in `eQuantic.UI.Native.Framework`.
One write-once component moves on both targets: `TextInput`, which holds a field and not its value,
keeps the last value its field reported until its parent configures it again, so its own rebuild when
the focus moves no longer builds the field from the value its parent handed it before the typing. eqc,
the SDKs and the templates do not move. The public surface of `eQuantic.UI.Native.Framework`
(experimental, `EQX0001`) gains `FieldText`; the developer surface does not move.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `element-tree`: a frame after a key typed into a field builds what the handler marked, and nothing
  else, with no fallback.
- `text-entry`: a Photon field keeps what was typed until the page builds it again, and then shows what
  the page holds.

## Impact

- `src/eQuantic.UI.Native.Framework`: `FieldText` and the state it keeps on a field's position; the
  measure of a `TextEntry`.
- `src/eQuantic.UI.Native.Components`: `PhotonHost` (the text editing, and the fallback removed for a
  field), the paint walk of a field and the semantics walk of a field.
- `src/eQuantic.UI.Components/TextInput.cs`, and its transpiled twin in the runtime.
- `docs/FLUTTER-PARITY.md`'s Element tree row, which names this as the difference left.
- The wiki's Photon page, "What a frame builds", in its five languages: its bullet on a frame after
  input says a field's handler that calls no `SetState` rebuilds every component.
