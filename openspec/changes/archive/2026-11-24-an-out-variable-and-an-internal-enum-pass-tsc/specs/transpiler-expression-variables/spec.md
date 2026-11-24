## MODIFIED Requirements

### Requirement: An expression variable is declared by its statement, with C#'s scope

A variable a C# expression declares SHALL be declared by the JavaScript the statement holding it is
written as, with the scope Roslyn gives it. An expression statement, an `if`, a `return`, a `throw`,
a `yield return`, a local declaration, a `switch`'s governing expression and a `lock` SHALL declare
it in the block it lives on in, where the statements after them can read it; when that block is a
switch's, every section SHALL be able to assign and read it. A `while`, a `do`, a `for`, a
`foreach` and a `using` SHALL declare it inside the statement, so two sibling statements may bind
one name. A variable a lambda, an anonymous method, a local function or a query clause declares
SHALL be declared inside it, once for every call.
In TypeScript an `out var` of a named type SHALL be declared by that type, definitely assigned, and a miss
that leaves null there SHALL write it as one TypeScript takes, so a value read through it stays typed.

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

#### Scenario: A list read through an out var is walked typed

- **WHEN** a method reads `TryGetValue(pointer, out var arena)` on a `Dictionary<int, Pointer>` and walks
  `foreach (var member in arena.Members)` over a `List<string>`
- **THEN** the module declares `let arena!: Pointer;`, a miss writes `arena = null!`, and the runtime's `tsc` takes
  the walk, whose elements are strings
