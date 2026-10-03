## ADDED Requirements

### Requirement: A type pattern tests the type

A type pattern with nothing bound (`o is int`, `int => …`, `case int:`, either side of `or` and
`and`) SHALL test the value's type as a declaration pattern does, read off the type's symbol: a
string, a bool, a long as a BigInt, an integral type as a whole number, a real as a number, and a
decimal and the dates as the runtime's classes.

#### Scenario: A long among ints

- **WHEN** browser-side code asks `o is int or long` of an `object` holding `5L`
- **THEN** the answer is `true`, as in .NET

#### Scenario: A switch over types

- **WHEN** a switch statement over an `object` holding `5` has `case long:` and then `case int:`
- **THEN** the `int` section runs, as in .NET

### Requirement: A named constant in an is test compares with its value

`x is C`, where `C` binds as a constant (a `const` field or an enum member), SHALL compare `x` with
the constant's value, an enum member as the value the browser holds for it.

#### Scenario: A const

- **WHEN** browser-side code asks `x is Limits.Max` of `x = 3`, where `Limits.Max` is the const `10`
- **THEN** the answer is `false`, as in .NET
