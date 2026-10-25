## ADDED Requirements

### Requirement: An autofocused entry puts its caret at the end

A `TextEntry` with `Autofocus` SHALL take the keyboard when it is mounted with its caret at the end of the
text it holds, on the web as on Photon, as Flutter's autofocused `TextField` does.

#### Scenario: A field that opens holding a prefix

- **WHEN** an autofocused entry holding `:` is mounted and `12` typed
- **THEN** it holds `:12`
