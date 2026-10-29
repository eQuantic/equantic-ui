## ADDED Requirements

### Requirement: A component's field of a struct with no zero twin is left as a property of one is

A component's instance or static field whose type is a vocabulary struct the twin cannot build a zero
for (`SizeValue`, `EdgeInsets`), declared with no initializer, SHALL hold that twin's own default as a
property of the same type does, and its module SHALL type-check: the instance field is declared with
no initializer and definitely assigned, and the static field with no initializer.

#### Scenario: An init setting backed by a struct field

- **WHEN** a component declares `private SizeValue _width;` and `public SizeValue Width { get => _width; init => _width = value; }`
- **THEN** its module declares `_width!: SizeValue;`, `tsc` accepts it, and a `Width` never set reads
  what an unset `SizeValue Width { get; init; }` read before
