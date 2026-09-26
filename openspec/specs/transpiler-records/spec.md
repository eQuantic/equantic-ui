# transpiler-records Specification

## Purpose
How eqc builds the twin of a record, a struct or a class: its constructor, and what each member
starts as.

## Requirements

### Requirement: A member starts as its declaration says

A twin's constructor SHALL give each member the default its declaration gives it (a positional
parameter's default, or a property's or a field's initializer), converted as the expression it is,
and its type's default when it declares none.

#### Scenario: A field initializer

- **WHEN** `record Fields { public string Log = "x"; }` is built with `new Fields()`
- **THEN** its `Log` is `"x"`, as in .NET

#### Scenario: A decimal and a long property

- **WHEN** `record Props { public decimal Price { get; init; } = 1.5m; public long Count { get; init; } = 5; }`
- **THEN** `(new Props().Price + 1m).ToString()` answers `2.5` and `(new Props().Count + 1L).ToString()` answers `6`

#### Scenario: A fresh collection per construction

- **WHEN** a property is initialized with `= new()` and two instances are built
- **THEN** adding to one instance's collection leaves the other's empty

### Requirement: A construction that skips a member leaves it to the constructor

A construction site SHALL pass nothing of its own for a member it does not set, so the
constructor's default applies.

#### Scenario: An object initializer sets one member

- **WHEN** `new Fields { N = 3 }` is built
- **THEN** its `Log` is `"x"` and its `N` is 3

### Requirement: A default and a base clause read the constructor's parameters

A member default and a base clause's arguments SHALL read a primary-constructor parameter as that
parameter, before any member of the instance is set.

#### Scenario: An initializer reads a positional parameter

- **WHEN** `record Tagged(int Id) { public string Tag = "#" + Id; }`
- **THEN** `new Tagged(4).Tag` answers `#4`

#### Scenario: A base clause computes from a parameter

- **WHEN** `record Offset(int X) : Measure(X + 1)` with `record Measure(int Value)`
- **THEN** `new Offset(3).Value` answers 4, and building it throws nothing

### Requirement: A record's twin says what C# declares

A record's twin SHALL annotate a nullable delegate as a function that may be missing, and SHALL
import every runtime name its annotations use, `Decimal` included.

#### Scenario: A record with a fold label and an amount

- **WHEN** a record has a `decimal Amount`, a method returning `(decimal Net, decimal Tax)`, and a
  parameter `Func<int, string>? label`
- **THEN** its module imports `Decimal`, the method is annotated `[Decimal, Decimal]`, and the
  parameter `((value: number) => string) | null`
