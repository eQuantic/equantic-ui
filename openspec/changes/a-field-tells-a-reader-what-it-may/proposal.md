# Proposal

Closes #817 and #1088, two Bugs in what a text field on Photon tells a screen reader: a password field
handed its text over (#817, under #207, Handoff fidelity), and a field a listbox hangs from was read as a
plain text field whose list said nothing as the arrows moved through it (#1088, under #1083's code editor
work).

## Why

A `TextEntry` with `Obscure` draws dots and tells the keyboard it is a password, and its semantics node
carried the text in clear. Every native bridge reads a field's value from that node: Android set it as
the node's text with `password="false"` (seen with `uiautomator dump` while proving #812), iOS as its
accessibility value and macOS as `AXValue`.

The code editor's go-to field (#1083) is a `TextEntry` anchoring an `Anchored` whose panel is a
`Listbox`. On the web its text control is the combobox, with `aria-activedescendant` naming the option
the arrows reach. On Photon `SemanticsVisitor` made only a `Pressable` anchor a combobox, and nothing on
Photon read `Anchored.ActiveIndex` at all: the field read as a text field, and moving through the list
said nothing until the reader walked into the panel. A `Select`, a `Menu` and a `TimePicker` keep the
keyboard on their trigger the same way, and their highlighted row was paint only on Photon too.

## What Changes

- `SemanticsVisitor` writes the dots the field draws into the node, one per character as the eye counts
  them, so no bridge can hand the text to a reader, and `SemanticNode` gains `Obscured`.
- Android sets the node's `Password` flag and macOS the `AXSecureTextField` subrole. iOS reads the dots
  as the value, which is what a secure text field exposes there.
- `SemanticRole` gains `EditableComboBox`, appended: a field a listbox hangs from, the first text field
  in the anchor as the web picks it, with `Expanded` saying whether its list shows. The bridges hear it
  as Core-AAM's own pair, `AXComboBox` and `android.widget.EditText`, the class Chrome gives the web half
  of the same field.
- The item row an open menu's or listbox's keys are on is a polite live region while it is the one: the
  realizer finds it among the rows it lays out, counted as the web numbers them, its semantic node says
  it is live, and the announcer says its name as it changes. A region that was not one last frame is
  news again, so a row the keys come back to is said again.

For a developer: nothing to write. A field with `Obscure` is announced as a secure field on every
target, as `<input type=password>` already is on the web, and a field with a list under it is a combobox
whose options are heard as the keys reach them, as on the web. `SemanticNode` widens by one optional
parameter at the end, and `SemanticRole` by one value at the end.

## Capabilities

### Modified Capabilities

- `native-accessibility`: an obscured field's text never reaches the semantic tree; a field a listbox
  hangs from is an editable combobox, in each platform's words; the option the keys are on is heard as
  it changes.

## Impact

- `src/eQuantic.UI.Primitives/Contracts/SemanticNode.cs`, `SemanticsVisitor.Text.cs`, `NativeRole.cs`,
  `Semantics.cs`, `LiveRegionAnnouncer.cs`, `LiveRegionMark.cs`, `QueuedLayer.cs`, `EmitVisitor.Containers.cs`,
  `PhotonRealizer.cs`, and the Android and macOS `PhotonAccessibility`.
