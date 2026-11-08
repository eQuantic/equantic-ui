## ADDED Requirements

### Requirement: An obscured field's text never reaches the semantic tree

A `TextEntry` with `Obscure` SHALL be announced with the dots it draws as its value, one per character
as the eye counts them, and its `SemanticNode` SHALL say it is `Obscured`, so no bridge can hand the
text to a screen reader. Android SHALL mark the node a password, and macOS SHALL give it the
`AXSecureTextField` subrole.

#### Scenario: A password field

- **WHEN** a `TextEntry` holding `hunter2😀` with `Obscure` is laid out
- **THEN** its semantic node's value is eight dots and it says `Obscured`

#### Scenario: A plain field

- **WHEN** a `TextEntry` holding `hunter2` without `Obscure` is laid out
- **THEN** its semantic node's value is `hunter2` and it does not say `Obscured`
