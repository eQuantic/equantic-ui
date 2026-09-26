# Spec Delta

## Purpose

Where the JavaScript declares a variable a C# expression declares (a pattern's binding, an `out var`,
a deconstruction's element), and under what name.

## ADDED Requirements

### Requirement: An expression variable is declared by its statement, with C#'s scope

A variable a C# expression declares SHALL be declared by the JavaScript the statement holding it is
written as, with the scope Roslyn gives it. An expression statement, an `if`, a `return`, a `throw`,
a `yield return`, a local declaration, a `switch`'s governing expression and a `lock` SHALL declare
it in the block it lives on in, where the statements after them can read it. A `do`, a `foreach` and
a `using` SHALL declare it inside the statement, so two sibling statements may bind one name. A
variable a lambda, an anonymous method or a local function declares SHALL be declared inside it,
once for every call.

#### Scenario: The guard idiom reads it after the if

- **WHEN** `if (!int.TryParse("7", out var r)) return -1; return r;` runs
- **THEN** it answers 7

#### Scenario: A deconstruction's elements

- **WHEN** `(var c, var d) = (3, 4); return c + d;` and `var (a, (b, c)) = (1, (2, 3)); return a * 100 + b * 10 + c;` run
- **THEN** they answer 7 and 123

#### Scenario: Two sibling loops bind one name

- **WHEN** two consecutive `while` loops each declare `out var n` in their condition, adding 1 twice and then 2 three times
- **THEN** the total is 8

#### Scenario: A recursive local function has its own variable on every call

- **WHEN** `int Digits(int depth) { int.TryParse(depth.ToString(), out var d); var rest = depth > 0 ? Digits(depth - 1) : 0; return d * 10 + rest; }` answers `Digits(2)`
- **THEN** it answers 30

### Requirement: A loop's condition has a fresh variable every time round

A variable declared in a `while`'s or a `for`'s condition, and in a `foreach`'s body, SHALL be a new
variable on every iteration, as .NET makes it: a closure made in one iteration SHALL keep that
iteration's value.

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

### Requirement: A name JavaScript reserves is one name on every path

A C# local, parameter or local function whose name JavaScript refuses as a binding (a keyword the
verbatim escape allows, such as `@class`, or a strict-mode reserved word) SHALL be renamed to one
legal name at its declaration and at every reference, whichever way it was bound: a declaration, an
`out var`, a pattern, a deconstruction, a `for`, a `foreach`, a `catch`, a `using`, a lambda's or a
local function's parameter, a query's range variable, and a call through a delegate local or to a
local function.

#### Scenario: A verbatim local and a verbatim out var

- **WHEN** `var @class = 5; int.TryParse("7", out var @new); return @class * 10 + @new;` runs
- **THEN** it answers 57

#### Scenario: A local function named like a keyword

- **WHEN** `int Delete(int from) => from - 1; return Delete(3);` runs
- **THEN** it answers 2
