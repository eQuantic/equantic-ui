## ADDED Requirements

### Requirement: A string's search with no comparison is refused

`StartsWith`, `EndsWith`, `IndexOf` and `LastIndexOf` of a string with no `StringComparison`, with a start and a
count or without, and a method group of one, SHALL fail the build with EQ1004 in code that reaches the browser,
naming `StringComparison.Ordinal` and `OrdinalIgnoreCase`. .NET runs them by the current culture, which has no
search in the browser. A char's search and `Contains` are ordinal in .NET and SHALL keep building.

#### Scenario: A search with no comparison

- **WHEN** `name.StartsWith("ab")` is built for a page
- **THEN** the build fails with EQ1004; it ran ordinally, and `"­ab".StartsWith("ab")` answered false where
  .NET answers true

#### Scenario: A char's search

- **WHEN** `name.StartsWith('a')` is built for a page
- **THEN** it builds

### Requirement: A culture comparison that ignores case keeps widths and kana apart

A culture comparison that ignores case SHALL keep half and full width forms and the kana types apart as .NET's
does: each form a primary difference right after its other form, whatever follows it in the text, a small
katakana after the forms of its large one.

#### Scenario: A full width letter

- **WHEN** `string.Equals("Ａ", "A", StringComparison.InvariantCultureIgnoreCase)` runs
- **THEN** it answers false, as .NET does; it answered true

#### Scenario: A form against what follows it

- **WHEN** `string.Compare("Ａa", "Ab", StringComparison.InvariantCultureIgnoreCase)` runs
- **THEN** it answers 1, as .NET does

### Requirement: A value held as an interface or a type parameter writes its text as the value does

A value whose static type is an interface or a type parameter SHALL write its text as one held as `object`
does: a number or a date in the culture in force, and a type of the app's by its own `ToString`.

#### Scenario: A number behind an interface on a pt-BR page

- **WHEN** `IFormattable x = 1.5; return $"{x}";` runs on a pt-BR page
- **THEN** it answers `1,5`, as .NET does; it answered `1.5`
