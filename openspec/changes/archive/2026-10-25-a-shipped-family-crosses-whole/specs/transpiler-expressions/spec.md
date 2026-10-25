## ADDED Requirements

### Requirement: A params parameter takes what C# hands it

eqc SHALL pass a params parameter what the bound tree says the call hands it: each element a call lists,
as it is, after a named argument too, or a collection handed whole, spread into the rest parameter the
parameter is on the other side, named or not. An array handed to a params parameter of another element
type SHALL be one element where C# makes it one. A C# 13 params collection SHALL be read as an array is.
This SHALL hold for a call, an app type's constructor and a vocabulary type's constructor alike.

#### Scenario: A collection handed whole

- **WHEN** `record FontFamily(string Name, params IReadOnlyList<FontFile> Files)` is built as
  `new FontFamily("Brand", [regular, italic])`
- **THEN** `Files.Count` is `2`, as in .NET

#### Scenario: Elements listed after a named argument

- **WHEN** `static string Tagged(string tag, params int[] xs)` is called as `Tagged(tag: "t", 1, 2, 3)`
- **THEN** `xs.Length` is `3`, as in .NET

#### Scenario: An array of ints handed to params object[]

- **WHEN** `static int Objects(params object[] xs) => xs.Length` is called with an `int[]` of two
- **THEN** it answers `1`, as in .NET

#### Scenario: A params collection listed at a constructor

- **WHEN** a class whose constructor takes `params IReadOnlyList<int>` is built as `new Bag(1, 2, 3)`
- **THEN** the build completes and the constructor reads three elements, as in .NET
