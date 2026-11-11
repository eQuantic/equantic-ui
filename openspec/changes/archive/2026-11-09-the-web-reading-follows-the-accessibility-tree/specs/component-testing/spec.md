## ADDED Requirements

### Requirement: The web reading follows the accessibility tree a browser builds

`ComponentTester.Semantics` SHALL read the lowered markup as a browser builds its accessibility tree: an
element another element owns through `aria-owns` SHALL be read under its owner, after the owner's own
children and in the order the attribute lists; a field with no other name SHALL be named by its `title` and
then its `placeholder`; and a live region (`role` `status`, `log` or `alert`, or `aria-live` other than
`off`) SHALL be announced as a `Group` around what it holds.

#### Scenario: A table written in bands is read row by row

- **WHEN** a test pumps a table viewport labelled `Ledger` whose cells are added down the columns
- **THEN** its semantics read `Ledger`, `0:0`, `0:1`, `1:0`, `1:1`

#### Scenario: A field with no label is named by its placeholder

- **WHEN** a test pumps a text input with no label and the placeholder `Search the archive`
- **THEN** a text field named `Search the archive` is announced

#### Scenario: A live region is a group around its message

- **WHEN** a test pumps a toast saying `Saved`
- **THEN** its semantics hold a `Group` and, after it, the static text `Saved`
