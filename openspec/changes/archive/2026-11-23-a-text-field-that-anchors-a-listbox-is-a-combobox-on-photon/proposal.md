# Proposal

Closes #1088, a Bug under #849.

## Why

On Photon, a text field that a listbox hangs from read as a plain text field, and moving through its list said
nothing until the reader walked into the panel. The code editor's go-to field (#1083) is one: a `TextEntry` that
anchors an `Anchored` whose panel is a `Listbox`. On the web its text control is the combobox, with
`aria-controls` and `aria-activedescendant` on the focused input, so a reader follows the arrows. On Photon only a
`Pressable` anchor became a `ComboBox`.

## What Changes

- **A text field a listbox hangs from is an editable combobox on Photon.** It reaches the semantics tree as the
  new `SemanticRole.EditableComboBox`, named by its label (or its placeholder without one), holding its text as
  its value, and saying whether its list is open. The role is appended to the enum, by its rule.
- **Each platform hears it in its own words:** Core-AAM's pair for an editable combobox, `AXComboBox` on the Mac
  and `android.widget.EditText` on Android, activatable, with no UIKit trait, as a text field has none.
- **The option the keyboard is on is announced from the field as it changes.** It is the row
  `Anchored.ActiveIndex` counts to, the one the web names in `aria-activedescendant`, never the one marked
  selected. The host's live region announcer says it politely, and a closed list says nothing, so the option is
  heard again when the list opens or the keyboard comes back to it.
- **The testing reader hears the web's field as the same role.** `ComponentTester` read an input with
  `role="combobox"` as a select-only combobox; it now reads it as the editable one, so the semantics parity
  fixture compares the two targets for this field.

A developer writes nothing different. The public surface gains one enum member,
`SemanticRole.EditableComboBox = 30`; no member goes, and the developer surface does not move.

## Impact

- `eQuantic.UI.Primitives`: the enum member.
- `eQuantic.UI.Native.Components`: the semantics walk, the native role table, the realizer's overlay pass and the
  live region announcer (a mark can be said from a position other than the one that keeps it).
- `eQuantic.UI.Testing`: the announced tree.
- The shells: none changed. macOS and iOS post the announcement as they post every live region's. Android finds
  the field's node and reports a change from it, but TalkBack reads a change only from a live region's node, and
  the field's node carries its own text, so on Android the option is not heard yet: a follow-up, which needs a
  device to measure.
