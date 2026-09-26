# csharp-translation Specification

## Purpose
How eqc translates a C# construct into the JavaScript twin: the answer the twin gives is the one
.NET gives, and the twin typechecks under the runtime's own build.

## Requirements

### Requirement: An extended property pattern reads each member of its path

An extended property pattern (`{ A.B: pattern }`) SHALL be translated as the nested pattern
`{ A: { B: pattern } }`: each member of the path named as a member access names it, and a member
that is null before the last SHALL make the pattern answer false.

#### Scenario: A count through a member

- **WHEN** `new Bag(new List<int> { 1, 2 }, null) switch { { Items.Count: > 1 } => 1, _ => 0 }` runs
- **THEN** it answers 1, as .NET does

#### Scenario: A null on the path

- **WHEN** `new Bag(new List<int> { 1 }, null) switch { { Corner.X: 0 } => 1, _ => 0 }` runs
- **THEN** it answers 0, as .NET does, and nothing throws

### Requirement: A string crosses whole

Every string literal a twin holds, a declared default included, SHALL be written with its line
breaks escaped, so the module parses and the value is the one .NET holds.

#### Scenario: A default holding a line break

- **WHEN** `record Sep(string Joined = "a" + "\n", char Line = '\n', string Plain = "\r\n")` is
  constructed without arguments and its three members are concatenated
- **THEN** the answer is `a`, a line feed, a line feed, a carriage return and a line feed, as .NET
  gives it

### Requirement: A declared default is the constant C# folds

A default value SHALL be the constant the C# compiler folds from its expression, a negative number
included, both where a type is constructed without the argument and where a named call skips it.

#### Scenario: A negative default

- **WHEN** a record declares `int SourceLine = -1` and a call names another argument and skips it
- **THEN** the twin constructs it with -1, not null

### Requirement: A record's twin says what C# declares

A record's twin SHALL annotate a nullable delegate as a function that may be missing, and SHALL
import every runtime name its annotations use, `Decimal` included.

#### Scenario: A record with a fold label and an amount

- **WHEN** a record has a `decimal Amount`, a method returning `(decimal Net, decimal Tax)`, and a
  parameter `Func<int, string>? label`
- **THEN** its module imports `Decimal`, the method is annotated `[Decimal, Decimal]`, and the
  parameter `((value: number) => string) | null`
