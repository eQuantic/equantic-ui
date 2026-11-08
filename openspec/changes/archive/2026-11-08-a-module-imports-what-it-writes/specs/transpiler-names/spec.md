## ADDED Requirements

### Requirement: A type named by a reserved word or a global takes a `$`

A class, record or struct whose twin the build writes, named by a word JavaScript reserves or by a
global the emitted code reads, SHALL be written, imported and referenced under its name and a `$`,
which no C# identifier can hold, and so SHALL its module. A nested type is named by its owner already,
and a type of .NET's or of the vocabulary keeps its name.

#### Scenario: A class named function

- **WHEN** a class `function` is built, extended by `@class`, tested with `is` and `as`, held in a
  member and an array, and printed
- **THEN** every answer is .NET's, its module is `function$` and `@class`'s is `class$`

#### Scenario: A class named Math beside .NET's

- **WHEN** `Math.Twice(2) + System.Math.Max(1, 2)` runs, `Math` being a class of the app
- **THEN** it answers 6, as in .NET

#### Scenario: A struct named Symbol in a sequence's module

- **WHEN** a class that implements `IEnumerable<int>` holds a field of a struct named `Symbol`, and a
  `foreach` walks it
- **THEN** it walks every element, as in .NET
