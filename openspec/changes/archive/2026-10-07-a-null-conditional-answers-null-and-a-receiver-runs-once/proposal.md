# Proposal

Closes #633 and #619, two Bugs under #565 (The transpiler's fences hold on every path, continued).

## Why

A null-conditional read whose translation is JavaScript's optional chain, `a?.B` as `a?.b`, is
`undefined` in the browser when `a` is null, where C# answers `null`. The two part ways wherever the
value is used: a parameter the emitter types `T | null` refuses it in the runtime's own build, JSON
drops a key that holds it, and a dictionary that looks for null does not find it. Measured through the
conformance harness, `ContainsValue(null)` over a value read that way answers false in the browser and
true in .NET (#633).

A method group bound to its receiver wrote the receiver twice, once to read the method and once to bind
it, so a receiver that is a call ran twice: `c.Make().Value` emitted `c.make().value.bind(c.make())`,
and a counter it bumps answers 4 where .NET answers 2 (#619).

## What Changes

- **An optional chain answers null where its value is used.** `a?.b` becomes `(a?.b ?? null)` where the
  value goes on to something: an assignment, an argument, a return, an operand. It stays a bare chain
  where nothing can tell the two apart: a call that returns nothing (C# never lets its value be used),
  a statement that discards it, the left of a `??`, and the tail of another chain, which answers where
  the chain ends.
- **A method group reads its receiver once.** The bind is a template over the receiver, so a receiver
  that is not a plain name is evaluated once, as C# evaluates it when the delegate is made, and `this`
  stays inline: `this.value.bind(this)`.

What does not move: the developer's C#, the developer surface and the public API. Two twins of the
runtime's library change where a chain's value is used or a method group binds a field's object:
`CodeDiffLayout` and `Spreadsheet`.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `transpiler-expressions`: a null-conditional read answers null where its value is used, and a method
  group reads its receiver once.
