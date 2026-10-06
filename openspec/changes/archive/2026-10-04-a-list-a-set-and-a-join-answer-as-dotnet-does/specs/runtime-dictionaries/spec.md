## MODIFIED Requirements

### Requirement: A key compares as .NET's default comparer compares it

Two keys SHALL be the same key when .NET's default equality comparer for the key type says so: by
value for a record, a struct, a tuple, a decimal, a date and a class that overrides `Equals`, by
reference for a class that does not, and by value for a number (NaN equal to NaN), a string, a char,
a bool, a long, an enum and a `Guid`. A key whose type does not decide (`object`, an interface, a
type parameter) SHALL compare as the value it holds compares. A tuple key SHALL compare each element
by its own type's rule, an array element by reference. The rule SHALL be the one a set's elements and
a list's searches take for the same type.

#### Scenario: A date key

- **WHEN** a `Dictionary<DateTime, int>` sets `d[new DateTime(2026, 1, 1)] = 1` and reads `d[new DateTime(2026, 1, 1)]`
- **THEN** it answers 1, as in .NET

#### Scenario: A record under object

- **WHEN** a `Dictionary<object, int>` sets `d[new Point(1, 2)] = 1` and then `d[new Point(1, 2)] = 2`
- **THEN** it holds one key, whose value is 2, as in .NET

#### Scenario: A char key

- **WHEN** a `Dictionary<char, int>` sets two keys and a `foreach` walks its pairs
- **THEN** the walk visits both in insertion order, as in .NET, where the plain path threw

#### Scenario: A tuple key holding an array

- **WHEN** a `Dictionary<(int[], int), int>` keyed by `(a, 1)` is asked whether it contains `(new[] { 1 }, 1)`
- **THEN** it answers false, as in .NET, where comparing the array element by element found it
