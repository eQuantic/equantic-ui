# Proposal

Closes #451, #475, #476, #477, #478, #482, #483 and #486, sub-issues of #164.

## Why

Each of these compiled, emitted, and answered differently in the browser, measured on main
(6964482f) through the conformance runner, both sides executed:

- A type pattern with nothing bound (`o is int or long`, `int => …`, `case int:`) had no case in the
  pattern converter and tested `false` (#482). The type test read the spelling, so `System.Int64` was
  not recognized, and a long, a BigInt here, was asked whether it was a number.
- `x is Limits.Max` parses as a type test and binds as a constant pattern: it was answered
  `x != null`, true for every number (#451).
- A lock's expression was dropped unless it declared a variable, so `lock (Gate())` never called
  `Gate` (#475). `new object()` named a class JavaScript does not have (#478).
- A for loop declares one variable for the whole loop, and the head's `let` gave each iteration its
  own: three closures over `i` answered 0, 1 and 2 where .NET answers 3, 3 and 3 (#476). A delegate
  read from a list and called in place, `fs[0]()`, was read off `this` (#477).
- `(a, b) = point` over a record was array destructuring, which threw `{} is not iterable` (#486).
- A static property guarding its own store with `field` declared the store on the instance, which a
  static accessor's `this` (the class) does not reach, and a record dropped the property outright
  (#483).

## What Changes

- The pattern converter tests a type pattern as a declaration pattern does, and the type test reads
  the symbol: a string, a bool, a long as a BigInt, an integer as a whole number, a real as a number,
  a decimal and the dates as the runtime's classes.
- `x is C` that binds as a constant compares with the constant's value (an enum's member as the twin
  holds it).
- A lock runs its expression unless it only reads; `new object()` is a fresh object.
- A for loop whose variable a closure captures declares it in front of the loop, in a block of its
  own; a delegate value reached by any expression but a name is called as that value.
- A record deconstructed by assignment destructures by its members' names.
- A static `field` store is declared on the class, in a component, a plain class and a record, with
  the record's accessors.

## Capabilities

### New Capabilities

- `transpiler-statements`: how a statement's own semantics cross (a lock, a for loop, a delegate call).

### Modified Capabilities

- `transpiler-expressions`: a type pattern and a named constant in a pattern.
- `transpiler-records`: deconstruction by assignment and a static `field` store.

## Impact

- **eqc**: `PatternConverter`, `IsPatternStrategy`, `InlinedConstantStrategy` (its literal writer is
  shared), `LockStatementStrategy`, `ForStatementStrategy`, `InvocationStrategy`,
  `ObjectCreationStrategy`, `AssignmentExpressionStrategy`, `TypeScriptEmitter`, `RecordTypeEmitter`.
- **Conformance harness**: a program imports the scalar classes a type test names.
- **Break**: a type test of an integral type is `Number.isInteger`, so `object o = 2.5; o is int` is
  false, as in .NET, where it was true.
