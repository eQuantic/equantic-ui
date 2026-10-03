# transpiler-exceptions Specification

## Purpose
How eqc lets C# throw and catch in the browser as .NET does: an exception carries its .NET types,
a catch clause takes the exception its type names and its filter accepts, in C#'s order, and a
rethrow rethrows the exception caught.

## Requirements

### Requirement: An exception carries its .NET type

A .NET exception in the browser SHALL carry the .NET types it is, the most derived first and
`System.Exception` last. `new T(…)` for an exception type, decided by T's symbol whatever its name and
including an app's own class and the target-typed `new(…)`, SHALL build one with T's chain, and an
exception the runtime throws on .NET's behalf SHALL be of the type .NET throws for the same operation.
A JavaScript `TypeError` SHALL read as a `NullReferenceException`, and any other error without a chain
as an `Exception` and nothing more specific. A type pattern, a switch arm and an `as` over an exception
type SHALL test that type.

#### Scenario: A type pattern over exceptions

- **WHEN** `Exception e = new ArgumentNullException("p"); return $"{e is ArgumentException}{e is InvalidOperationException}{e is Exception}";` runs
- **THEN** it answers `TrueFalseTrue`, as .NET does

#### Scenario: An overflow the runtime throws

- **WHEN** `int m = int.MaxValue; try { return checked(m + 1); } catch (DivideByZeroException) { return 1; } catch (ArithmeticException) { return 2; }` runs
- **THEN** it answers 2, as .NET does

#### Scenario: A member read through null

- **WHEN** `string s = null; try { return s.Length; } catch (ArgumentException) { return 1; } catch (NullReferenceException) { return 2; }` runs
- **THEN** it answers 2, as .NET does

#### Scenario: An exception class of the app's own

- **WHEN** `class GateClosedException : InvalidOperationException` is thrown and caught by
  `catch (ArgumentException) { … } catch (GateClosedException e) { return e.Message; }`
- **THEN** the second clause takes it and answers its message, as .NET does

### Requirement: A catch clause takes the exception its type names

The catch clauses of a `try` SHALL be tried in order, as C# tries them: the first whose type takes the
exception, its own type or one derived from it, and whose filter, if any, answers true, SHALL run, and
an exception no clause takes SHALL be thrown on. A clause with no type, or of `System.Exception`, SHALL
take every exception.

#### Scenario: Two clauses

- **WHEN** `try { throw new ArgumentException("x"); } catch (InvalidOperationException) { return 1; } catch (ArgumentException) { return 2; }` runs
- **THEN** it answers 2, as .NET does, where the module did not parse

#### Scenario: A type that does not match

- **WHEN** `try { try { throw new ArgumentException("x"); } catch (InvalidOperationException) { return 1; } } catch (Exception) { return 2; }` runs
- **THEN** it answers 2, as .NET does, where the inner clause took the exception and answered 1

### Requirement: A filter decides whether its clause takes the exception

A clause's `when` filter SHALL run when the clause's type takes the exception, with the clause's
variable bound, and SHALL decide whether the clause takes it. A filter that throws SHALL answer false,
and the exception it was asked about SHALL go on to the next clause. The variables a filter declares
SHALL be declared, and its clause's block SHALL read them.

#### Scenario: A filter that refuses

- **WHEN** `try { try { throw new Exception("a"); } catch (Exception e) when (e.Message == "b") { return 1; } } catch (Exception) { return 2; }` runs
- **THEN** it answers 2, as .NET does, where the filter was dropped and the inner clause answered 1

#### Scenario: A filter that declares a variable

- **WHEN** `try { throw new FormatException("12"); } catch (Exception e) when (int.TryParse(e.Message, out var n)) { return n; }` runs
- **THEN** it answers 12, as .NET does

#### Scenario: A filter that throws

- **WHEN** `Exception x = null; try { try { throw new InvalidOperationException("a"); } catch (Exception e) when (x.Message == "z") { return "first"; } } catch (InvalidOperationException e) { return "outer:" + e.Message; }` runs
- **THEN** it answers `outer:a`, as .NET does

### Requirement: A rethrow rethrows the exception caught

`throw;` SHALL rethrow the exception its clause caught, whatever the clause's variable has been
assigned since.

#### Scenario: A rethrow after the variable is reassigned

- **WHEN** `try { try { throw new Exception("a"); } catch (Exception e) { e = new Exception("b"); throw; } } catch (Exception e) { return e.Message; }` runs
- **THEN** it answers `a`, as .NET does, where `throw;` was a SyntaxError
