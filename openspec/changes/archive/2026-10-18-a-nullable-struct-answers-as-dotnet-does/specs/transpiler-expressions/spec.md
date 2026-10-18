## ADDED Requirements

### Requirement: A nullable struct's own member is read as the struct's

A member a struct declares, read on a nullable value of the struct through `?.` or in a property pattern,
SHALL be read as the struct's own member, whatever its name, `Value`, `HasValue`, `GetValueOrDefault` and
`Count` included, and SHALL answer null through `?.` for an empty value. `Nullable<T>`'s own `Value`,
`HasValue` and `GetValueOrDefault` SHALL be taken by the member the semantic model binds, and by their
names only where the model cannot say. A `Count` read on a nullable value SHALL be its value's, since
`Nullable<T>` declares none.

#### Scenario: A struct's own Value and HasValue through ?.

- **WHEN** `struct Probe` declares `int Value` and `bool HasValue => Value > 0`, and browser-side code
  computes `a?.Value` and `a?.HasValue` for `Probe? a = new Probe(0)`
- **THEN** they are 0 and False, as in .NET, where the browser answered the `Probe` itself and True

#### Scenario: A record struct's own Count

- **WHEN** browser-side code computes `c?.Count` and `c is { Count: 4 }` for `Cents? c = new Cents(4)`,
  where `record struct Cents(int Count)`
- **THEN** they are 4 and True, as in .NET, where the browser read a `length` no struct has

#### Scenario: A vocabulary value and the spreadsheet's

- **WHEN** browser-side code computes `s?.Value` for `SizeValue? s = SizeValue.Fixed(10)`, and a class of
  an app answers `Last?.Value` for a `SheetCellSnapshot?` recorded with `"x"`, built as an app's build
  builds it, in TypeScript and in JavaScript
- **THEN** they are 10 and `"x"`, as in .NET

#### Scenario: Nullable's own members

- **WHEN** browser-side code computes `a.HasValue` and `a.Value.Value` for `Probe? a = new Probe(3)`, and
  `b.GetValueOrDefault(new Probe(7)).Value` for an empty `Probe? b`
- **THEN** they are True, 3 and 7, as in .NET

### Requirement: An empty nullable unwrapped to a value throws

A nullable value unwrapped to a type that is not nullable SHALL throw `InvalidOperationException` with
.NET's message, "Nullable object must have a value.", when it is empty, before anything converts it,
wherever C# unwraps it: `Value`, a cast to the value's type or to another (a number, a char, a decimal, a
struct, an enum), the conversion C# applies around the value a struct's own conversion operator takes, and
a `foreach` element of a type that is not nullable. The value SHALL be read once. A present value SHALL
convert as it did, and a lifted conversion, to a nullable type, SHALL still answer null for null.

#### Scenario: A struct's own conversion operator

- **WHEN** `record struct Cents(int Count)` declares `explicit operator Cents(int value)`, and browser-side
  code computes `(Cents)n` for an empty `int? n`, catching `InvalidOperationException`
- **THEN** it is caught with "Nullable object must have a value.", as in .NET, where the browser built a
  `Cents` holding null; for `int? n = 5`, `((Cents)n).Count` is 5

#### Scenario: A cast and Value

- **WHEN** browser-side code computes `(int)n`, `(long)n`, `n.Value`, `(Level)e` and `(int)e` for an empty
  `int? n` and an empty `Level? e`, where `Level` is an enum
- **THEN** each throws `InvalidOperationException`, as in .NET, where the browser carried the null on

#### Scenario: A foreach over nullable elements

- **WHEN** browser-side code adds up `foreach (int x in xs)` over `new List<int?> { 1, null }`
- **THEN** the first element is added and the second throws `InvalidOperationException`, as in .NET

#### Scenario: A lifted conversion keeps its null

- **WHEN** browser-side code computes `(Cents?)n` and `long? l = n` for an empty `int? n`
- **THEN** both are null, as in .NET
