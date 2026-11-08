# Proposal

Closes #817, a Bug under #207 (Handoff fidelity): a password field on Photon handed its text to
the screen reader.

## Why

A `TextEntry` with `Obscure` draws dots and tells the keyboard it is a password, and its semantics node
carried the text in clear. Every native bridge reads a field's value from that node: Android set it as
the node's text with `password="false"` (seen with `uiautomator dump` while proving #812), iOS as its
accessibility value and macOS as `AXValue`.

## What Changes

- `SemanticsVisitor` writes the dots the field draws into the node, one per character as the eye counts
  them, so no bridge can hand the text to a reader, and `SemanticNode` gains `Obscured`.
- Android sets the node's `Password` flag and macOS the `AXSecureTextField` subrole. iOS reads the dots
  as the value, which is what a secure text field exposes there.

For a developer: nothing to write. A field with `Obscure` is announced as a secure field on every
target, as `<input type=password>` already is on the web. `SemanticNode` widens by one optional
parameter at the end.

## Capabilities

### Modified Capabilities

- `native-accessibility`: an obscured field's text never reaches the semantic tree.

## Impact

- `src/eQuantic.UI.Primitives/Contracts/SemanticNode.cs`, `SemanticsVisitor.Text.cs`, and the Android and
  macOS `PhotonAccessibility`.
