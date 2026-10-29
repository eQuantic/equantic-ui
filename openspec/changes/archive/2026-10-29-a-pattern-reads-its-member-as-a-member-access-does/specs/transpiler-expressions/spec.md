## ADDED Requirements

### Requirement: A property pattern reads its member as a member access reads it

eqc SHALL translate the read of a member a property pattern names exactly as it translates a member access
of the same member on a value of the same type, through the same strategies, at any depth: in a pattern
nested in another, in a positional part, in a list's element and in each member of an extended path. The
type of the value a pattern reads from SHALL be the one the bound pattern narrows to. A .NET member no value
in the browser carries SHALL be refused in a pattern with EQ2004, as its member access is.

#### Scenario: A count in a nested pattern

- **WHEN** `h is { Roles: { Count: 1 } }` and `h is { Inner: { Count: 2 } }` run, where `Roles` is a
  `HashSet<int>` holding one element and `Inner` is a `record Stats(int Count)` built with 2
- **THEN** both answer True, as .NET does

#### Scenario: A tuple's element

- **WHEN** `(1, 2) is { Item1: 1, Item2: 2 }` runs
- **THEN** it answers True, as .NET does

#### Scenario: A dictionary's keys and values

- **WHEN** `map is { Keys.Count: 1, Values.Count: 1 }` runs over a dictionary holding one entry
- **THEN** it answers True, as .NET does

#### Scenario: A member no browser value carries

- **WHEN** browser-side code tests `l is { Capacity: 8 }` for a `List<int>`
- **THEN** the build fails with EQ2004, as it fails for `l.Capacity`
