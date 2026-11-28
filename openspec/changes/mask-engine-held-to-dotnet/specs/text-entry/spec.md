## ADDED Requirements

### Requirement: A text entry carries a formatter, and the input family hands it on

A `TextEntry` SHALL carry a `Formatter`, an `ITextInputFormatter` that answers each `TextEdit` (the range it
replaces, the text, and whether it was typed, pasted, or deleted backward or forward) and each `TextCommand` (undo,
redo, the overwrite toggle) with a `TextEditingValue`, and says whether the field takes prose, whether it
overwrites, and which characters are its own. `TextInput` and `FormInput` SHALL take a `formatter` and hand it to
their entry, across a parent's rebuilds.

#### Scenario: A rebuild keeps the formatter

- **WHEN** a parent builds a `TextInput` with a formatter, then builds it again with a second formatter
- **THEN** the entry carries the first one after the first build and the second one after the second
