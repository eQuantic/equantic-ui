## ADDED Requirements

### Requirement: A field's description reaches the native tree

A `TextEntry`'s `Description` SHALL reach Photon's semantics tree as its node's `Description`, and each shell
SHALL hand it to its platform's screen reader: `accessibilityHelp` on macOS, `accessibilityHint` on iOS,
`AccessibilityNodeInfo.setHintText` on Android and UI Automation's `HelpText` on Windows.

#### Scenario: A mask that asks for more

- **WHEN** a Photon `MaskedInput` of mask `00000-000` holds `123`
- **THEN** its field's semantic node carries the value `123` and a description saying five more characters are
  required, and the macOS shell answers both from the field's accessibility element
