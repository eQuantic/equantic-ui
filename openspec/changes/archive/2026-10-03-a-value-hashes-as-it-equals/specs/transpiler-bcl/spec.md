## ADDED Requirements

### Requirement: A value hashes as it equals

`GetHashCode()` SHALL answer in the browser by .NET's contract: two values `Equals` finds equal SHALL
hash equal, whatever their type, a string, a number, a long, a bool, a value tuple, a record, a
struct, a decimal of any scale, a date and a value under `object` included. A type that overrides
`GetHashCode` SHALL answer its own, an instance of a class that does not SHALL hash by its identity,
`base.GetHashCode()` SHALL call the base's override where the app wrote one, and
`HashCode.Combine(…)` SHALL combine its values' hashes in order.

#### Scenario: Equal values hash equal

- **WHEN** browser-side code compares `new P(1, 2).GetHashCode()` with another `new P(1, 2)`'s, for
  `record P(int X, int Y)`, and `1.0m.GetHashCode()` with `1.00m.GetHashCode()`
- **THEN** both comparisons are true, as in .NET

#### Scenario: An override answers its own

- **WHEN** browser-side code calls `new Weighted(3).GetHashCode()` for
  `record Weighted(int X) { public override int GetHashCode() => X * 7; }`
- **THEN** it answers `21`, as in .NET

### Requirement: A Guid is its canonical text

A Guid made from text by `Guid.Parse`, `Guid.TryParse` or `new Guid(text)` SHALL be the text .NET
writes for it, the lowercase `D` format, read from any format .NET reads (`N`, `D`, `B`, `P`, `X`, in
either case, surrounded by white space), so two spellings of one Guid SHALL be one value to `==`, a
dictionary's key, a set and its hash. A text .NET refuses SHALL be refused, and a failed `TryParse`
SHALL leave `Guid.Empty`.

#### Scenario: Two spellings, one Guid

- **WHEN** browser-side code compares `Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950A")` with
  `Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950a")`, and adds an uppercase and a parenthesized
  spelling of one Guid to a `HashSet<Guid>`
- **THEN** the comparison is true and the set holds one Guid, as in .NET

### Requirement: A date leaving the calendar is refused as .NET refuses it

A `DateTime`'s and a `DateTimeOffset`'s `Add`, `Subtract`, `AddMonths`, `AddYears` and the `+` and `-`
operators with a `TimeSpan`, their compound forms included, SHALL refuse a result outside the calendar
with .NET's message, naming the parameter of the member that refuses it: `value` for `Add`, `Subtract`
and `AddYears`, `months` for `AddMonths`, and `t` for the operators. `AddMonths` SHALL refuse a count
past 120000 either way, and `AddYears` one past 10000, in .NET's words.

#### Scenario: The operator names its own parameter

- **WHEN** browser-side code evaluates `DateTime.MaxValue + TimeSpan.FromTicks(1)` and
  `DateTime.MaxValue.Add(TimeSpan.FromTicks(1))`, catching each message
- **THEN** both are "The added or subtracted value results in an un-representable DateTime.", the
  first with `(Parameter 't')` and the second with `(Parameter 'value')`, as in .NET
