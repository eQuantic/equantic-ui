# Spec Delta

## ADDED Requirements

### Requirement: A null-conditional call evaluates as C# does, an awaited argument included

A null-conditional access (`a?.M(x)`, `a?[i]`, and a chain behind one) SHALL evaluate its receiver
once and SHALL answer null when the receiver is null, without evaluating the rest of the chain or
its arguments. An argument that awaits SHALL be awaited in the method it is written in, whatever
the call translates to, and the module SHALL parse.

#### Scenario: An awaited argument behind a string

- **WHEN** `string s = "abc";` runs `var r = s?.StartsWith(await Needle(), StringComparison.Ordinal);`,
  where `Needle` counts its calls and answers `"a"`
- **THEN** `r` is true and `Needle` was called once

#### Scenario: An awaited argument behind a null receiver

- **WHEN** `string s = null;` runs the same line
- **THEN** `r` is null and `Needle` was never called
