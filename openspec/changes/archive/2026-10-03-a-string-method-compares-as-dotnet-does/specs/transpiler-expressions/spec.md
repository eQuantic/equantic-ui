# Spec Delta

## ADDED Requirements

### Requirement: A null-conditional call evaluates as C# does, an awaited argument included

A null-conditional access (`a?.M(x)`, `a?[i]`, and a chain behind one) SHALL evaluate its receiver
once and SHALL answer null when the receiver is null, without evaluating the rest of the chain or
its arguments. An argument that awaits SHALL be awaited in the method it is written in, only when
the receiver is not null, so a method that meets a null receiver goes on without suspending, and
the call's own answer SHALL NOT be awaited: a task it returns stays a task. Where the receiver is
not a local, a parameter or `this` and the call translates to a helper, a tail that awaits SHALL
fail the build with EQ1004, naming the fix: bind the receiver to a local first.

#### Scenario: An awaited argument behind a string

- **WHEN** `string s = "abc";` runs `var r = s?.StartsWith(await Needle(), StringComparison.Ordinal);`,
  where `Needle` counts its calls and answers `"a"`
- **THEN** `r` is true and `Needle` was called once

#### Scenario: An awaited argument behind a null receiver

- **WHEN** `string s = null;` runs the same line
- **THEN** `r` is null and `Needle` was never called

#### Scenario: A null receiver does not suspend the method

- **WHEN** an async method runs `var r = s?.StartsWith(await Needle(), StringComparison.Ordinal);`
  and then sets `finished = true`, with `s` null, and its caller reads `finished` before awaiting it
- **THEN** the caller reads true

#### Scenario: A receiver that is not a local

- **WHEN** a component calls `Get()?.StartsWith(await Needle(), StringComparison.Ordinal)`
- **THEN** the build fails with EQ1004, telling it to bind the receiver to a local first
