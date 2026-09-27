# Spec Delta

## Purpose

Where the JavaScript declares a variable a C# expression declares (a pattern's binding, an `out var`,
a deconstruction's element), with the scope C# gives it. The name it is declared under is the one
`transpiler-names` gives every reader.

## ADDED Requirements

### Requirement: An expression variable is declared by its statement, with C#'s scope

A variable a C# expression declares SHALL be declared by the JavaScript the statement holding it is
written as, with the scope Roslyn gives it. An expression statement, an `if`, a `return`, a `throw`,
a `yield return`, a local declaration, a `switch`'s governing expression and a `lock` SHALL declare
it in the block it lives on in, where the statements after them can read it; when that block is a
switch's, every section SHALL be able to assign and read it. A `while`, a `do`, a `for`, a
`foreach` and a `using` SHALL declare it inside the statement, so two sibling statements may bind
one name. A variable a lambda, an anonymous method, a local function or a query clause declares
SHALL be declared inside it, once for every call.

#### Scenario: The guard idiom reads it after the if

- **WHEN** `if (!int.TryParse("7", out var r)) return -1; return r;` runs
- **THEN** it answers 7

#### Scenario: A deconstruction's elements

- **WHEN** `(var c, var d) = (3, 4); return c + d;` and `var (a, (b, c)) = (1, (2, 3)); return a * 100 + b * 10 + c;` run
- **THEN** they answer 7 and 123

#### Scenario: A switch section assigns what another declared

- **WHEN** `switch (k) { case 1: int.TryParse("5", out var a); return a; case 2: a = 7; return a; default: return 0; }` runs with `k` 2
- **THEN** it answers 7

#### Scenario: A variable named like the switch's subject used to be

- **WHEN** `return 1 switch { 1 => int.TryParse("2", out var _s) ? _s : 0, _ => 0 };` runs
- **THEN** it answers 2, the switch binding its subject to a name no C# identifier holds

#### Scenario: Two sibling loops bind one name

- **WHEN** two consecutive `while` loops each declare `out var n` in their condition, adding 1 twice and then 2 three times
- **THEN** the total is 8

#### Scenario: A recursive local function has its own variable on every call

- **WHEN** `int Digits(int depth) { int.TryParse(depth.ToString(), out var d); var rest = depth > 0 ? Digits(depth - 1) : 0; return d * 10 + rest; }` answers `Digits(2)`
- **THEN** it answers 30

### Requirement: A loop's condition has a fresh variable every time round

A variable declared in a `while`'s, a `do`'s or a `for`'s condition, and in a `foreach`'s body,
SHALL be a new variable on every iteration, as .NET makes it: a closure made in one iteration SHALL
keep that iteration's value.

#### Scenario: A closure over a while's condition

- **WHEN** a `while (int.TryParse(i < 2 ? (i + 1).ToString() : "x", out var n))` loop adds `() => n` to a list on each iteration, and the first two closures answer `a() * 10 + b()`
- **THEN** it answers 12

#### Scenario: A closure over a foreach body's out var

- **WHEN** a `foreach` over `"1"` and `"2"` parses each into `out var n` and adds `() => n`, and the closures answer `a() * 10 + b()`
- **THEN** it answers 12

### Requirement: Every member kind declares what its expressions declare

A getter, a setter, a constructor, a method and its local functions, an iterator, a field's and a
property's initializer, a record's methods, accessors, operators and conversions, and a component's
Build SHALL each declare the variables their expressions declare, in the TypeScript eqc emits and
in plain JavaScript. Two initializers of one class MAY bind the same name. Plain JavaScript SHALL
carry no type annotation on a declaration.

#### Scenario: Two field initializers binding one name

- **WHEN** a class declares `_first = int.TryParse("9", out var parsed) ? parsed : 0` and `_second = int.TryParse("8", out var parsed) ? parsed : 0`, and a property answers `_first * 10 + _second`
- **THEN** the property answers 98, in both of the emitter's modes

#### Scenario: A component's Build

- **WHEN** a component's Build runs `int.TryParse("7", out var number); (var tens, var units) = (3, 4);` and stores `number * 100 + tens * 10 + units`
- **THEN** it stores 734
