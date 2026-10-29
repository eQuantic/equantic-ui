## ADDED Requirements

### Requirement: A pattern reads its input once

The value an `is` pattern tests SHALL be evaluated once, whatever the pattern's shape (`or`, `and`, `not`,
relational, type, property, positional and list patterns, a binding, and their combinations), and so SHALL
the value of `x is Type`: a call, a property whose getter has an effect, and a member of `this` alike. An
input the pattern names nothing of SHALL be evaluated all the same. A switch expression and a switch
statement SHALL evaluate their input once.

#### Scenario: A getter through an or

- **WHEN** `var c = new Counter(); var hit = c.Value is 2 or 1; return c.Reads * 10 + (hit ? 1 : 0);` runs,
  `Value` a property whose getter counts its reads in `Reads` and answers 1
- **THEN** it answers 11, as .NET does, where the getter ran twice and the answer was 21

#### Scenario: A getter through a binding

- **WHEN** `var c = new Counter(); return c.Value is > 0 and var v ? c.Reads * 10 + v : -1;` runs
- **THEN** it answers 11, as .NET does

#### Scenario: A getter through the type test

- **WHEN** `var c = new Counter(); var hit = c.Text is char; return c.Reads * 10 + (hit ? 1 : 0);` runs,
  `Text` a getter that counts its reads and answers `"xy"`
- **THEN** it answers 10, as .NET does, where the getter ran to ask the type and again to ask the length

#### Scenario: A call through a property pattern

- **WHEN** `int n = 0; Pt Make() { n++; return new Pt(1, 2); } var hit = Make() is { X: 1, Y: 2 }; return n * 10 + (hit ? 1 : 0);` runs
- **THEN** it answers 11, as .NET does

#### Scenario: An input the pattern names nothing of

- **WHEN** `int n = 0; System.Func<int> f = () => { n++; return 1; }; var hit = f() is var _; return n * 10 + (hit ? 1 : 0);` runs
- **THEN** it answers 11, as .NET does, where `f` was never called and the answer was 1
