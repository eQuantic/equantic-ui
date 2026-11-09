## ADDED Requirements

### Requirement: A pattern reads each member it tests once

A member a property pattern reads SHALL be read once for each value in a pattern-matching operation (an `is`, a
switch expression, a switch statement), however many alternatives, conjuncts, bindings, arms or sections read
it, a member of a member and a null it answers included, and SHALL be read again by each evaluation of the
operation, as .NET's decision graph reads it: a property whose getter is the app's, an abstract one, a virtual
one an override answers for, and an interface's alike. A member whose read runs none of the app's code (a field,
an auto-property the twin keeps under its own name, a property of the platform's read off a value of the
platform's) SHALL be written where the pattern reads it.

#### Scenario: A getter through an or inside a property pattern

- **WHEN** `var c = new Counter(); var hit = c is { Value: 2 or 1 }; return c.Reads * 10 + (hit ? 1 : 0);` runs,
  `Value` a property whose getter counts its reads in `Reads` and answers 1
- **THEN** it answers 11, as .NET does, where the getter ran twice and the answer was 21

#### Scenario: A getter through the alternatives of a nested pattern

- **WHEN** `var c = new Counter(); var hit = c is { Corner: { X: 2 } or { Y: 2 } }; return c.Reads * 10 + (hit ? 1 : 0);`
  runs, `Corner` a getter that counts its reads and answers `new Pt(1, 2)`
- **THEN** it answers 11, as .NET does, where the getter ran four times and the answer was 41

#### Scenario: A getter through the arms of a switch expression

- **WHEN** `var c = new Counter(); var r = c switch { { Value: 2 } => 2, { Value: 1 } => 1, _ => 0 }; return c.Reads * 10 + r;` runs
- **THEN** it answers 11, as .NET does, where the answer was 21

#### Scenario: A getter through the sections of a switch statement

- **WHEN** `var c = new Counter(); switch (c) { case { Value: 2 }: return c.Reads * 10 + 2; case { Value: 1 }: return c.Reads * 10 + 1; default: return c.Reads * 10; }` runs
- **THEN** it answers 11, as .NET does, where the answer was 21

#### Scenario: A getter that answers null

- **WHEN** `var c = new Counter(); var hit = c is { Missing: "a" or "b" }; return c.Reads * 10 + (hit ? 1 : 0);`
  runs, `Missing` a getter that counts its reads and answers null
- **THEN** it answers 10, as .NET does, where the answer was 20

#### Scenario: Each evaluation reads it again

- **WHEN** `var c = new Counter(); var hits = 0; for (var i = 0; i < 3; i++) if (c is { Value: 2 or 1 }) hits++; return c.Reads * 10 + hits;` runs
- **THEN** it answers 33, as .NET does, where the answer was 63

#### Scenario: A member nobody can observe being read

- **WHEN** `var p = new Pt(1, 2); return p is { X: 1 or 2 };` is transpiled, `Pt` a positional record
- **THEN** the test reads `p.x` where it compares it, and holds nothing
