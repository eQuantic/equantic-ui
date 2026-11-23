## MODIFIED Requirements

### Requirement: A type pattern tests the type

A type pattern with nothing bound (`o is int`, `int => …`, `case int:`, either side of `or` and
`and`) SHALL test the value's type as a declaration pattern does, read off the type's symbol: a
string, a bool, a long as a BigInt, an integral type as a whole number, a real as a number, and a
decimal and the dates as the runtime's classes. A type test against a type parameter (`x is T`, `x as T`,
a type pattern `T t`, a `catch (T e)`) SHALL be refused with EQ2016, which names the parameter, since the browser
carries no type arguments at run time, and SHALL never be answered as a null check.

#### Scenario: A long among ints

- **WHEN** browser-side code asks `o is int or long` of an `object` holding `5L`
- **THEN** the answer is `true`, as in .NET

#### Scenario: A switch over types

- **WHEN** a switch statement over an `object` holding `5` has `case long:` and then `case int:`
- **THEN** the `int` section runs, as in .NET

#### Scenario: A test against a type parameter is refused

- **WHEN** a generic class's method tests `value is T`, `value as T` or a switch arm `T made`
- **THEN** the build fails with EQ2016, naming `T`, once per test

#### Scenario: A test against a class still compiles

- **WHEN** the same class's method tests `value is Shape`, where `Shape` is a class
- **THEN** no EQ2016 is reported
