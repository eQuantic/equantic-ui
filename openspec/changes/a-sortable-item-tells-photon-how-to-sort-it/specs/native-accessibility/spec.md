## ADDED Requirements

### Requirement: A node's hint is heard after its name on every shell

A `SemanticNode` MAY carry a `Hint`: what the control does when used, or how to use it. A shell with an accessibility
bridge SHALL set it in its platform's word, so a screen reader reads it after the node's name and value: macOS as the
element's `accessibilityHelp`, iOS as its `accessibilityHint`, and Android as its node's tooltip text from API 28. A node
with no hint SHALL carry none, and an element whose node no longer has one SHALL lose the one it carried.

#### Scenario: AppKit hears the hint

- **WHEN** the semantics tree holds a button named `Card` whose hint is "Press Space to pick it up."
- **THEN** its AppKit element answers `accessibilityHelp` with "Press Space to pick it up."

#### Scenario: A hint that goes is taken away

- **WHEN** the same node is read again with no hint
- **THEN** its AppKit element answers `accessibilityHelp` with nothing
