# Tasks

## 1. The scanner

- [x] 1.1 Type names gathered from several expressions, and a loop head's list, from the `out var`s those expressions declare. Verify a `for` head writes `next: T = undefined!`, which the runtime's `tsc` takes where `let next!: T` in a head is a syntax error.

## 2. The scopes

- [x] 2.1 Have `while`, `do`, `for`, `using`, `try`, the switch, the switch expression, an initializer and a constructor's base call ask it. Verify `EmittedScopes` holds one of each, and that the runtime's `tsc` refuses them with main's scanner (TS18046) and takes them with this one.
- [x] 2.2 Declare a method's own `out` parameter by its type in the body that assigns it. Verify `EmittedScopes.Lease` walks one, refused on main (TS18046) and taken with the fix, and that the four library modules that move change their annotations alone.
- [x] 2.3 Type a name one declaration holds for separate scopes only when every declaration of it agrees. Verify two arms, two filters and a guard beside a pattern that declare `found` with two types are refused when the first type wins (TS2322) and taken with `any`.
- [x] 2.4 Hold a declaration-less `using`'s resource under one fixed name. Verify `UsingResourceNameTests` fails with the `Guid` name and passes with `$disposable`, and that the conformance suite runs one such `using` and two nested.
- [x] 2.5 Remove the scanner's overloads that take an expression and a `bool`, and declare the removals.

## 3. The record

- [x] 3.1 Write the ledger entry under `docs/ledger/`, citing #1865.
