# Proposal

Closes #1807, a Bug under #1587 ("The transpiler's fences hold on every path, part five").

## Why

C#'s default context is unchecked. In it, `int` and `long` arithmetic wraps: with `m = int.MaxValue`,
`m + 1` is `int.MinValue`. The transpiler wrapped them only inside an explicit `unchecked`, so the
browser answered 2147483648 for the same expression, and 9223372036854775808 for a long. A value
outside the type's range then reached comparisons, hashes, formatting and the server.

This was a documented divergence, chosen so that a plain `i + 1` would not carry a `| 0`. Its price is
that a rule ported from the server that leans on the wrap answers differently in the browser, and
nothing says so. Examples are a hash written without `unchecked`, a packed value or a checksum. That
silent mismatch is the one thing the conformance suite exists to rule out. A reader raised this exact
case on r/csharp as the reason they kept integer-heavy C# on WASM.

## What Changes

- Every fixed-width integer wraps where C# wraps it, in the default context as in `unchecked`. This
  covers `+`, `-`, `*`, `<<`, a negation, an increment, a decrement and a compound assignment.
  - An int sum is `a + b | 0`.
  - An int product is `Math.imul(a, b)`, which keeps the low bits a double past 2^53 loses.
  - An int's `<<` is JavaScript's own, which already answers in 32 bits.
  - A long or a ulong wraps through `BigInt.asIntN` / `BigInt.asUintN`.
  - A `checked` context still throws.
- A constant C#'s compiler folded is left as written: outside `unchecked`, it is in range or the build
  failed (CS0220). So `-5` stays `-5`.
- A postfix step in a lambda whose delegate returns nothing is written as the assignment. No function
  is built around it to answer a value nothing reads.

The compiler's own surface changes (`ArithmeticContext`, `IntegerWidth`). It is a tool surface, whose
removals are free. Neither the developer surface nor any contract surface moves.

An app meets this as arithmetic that answers what .NET answers. The served runtime, whose shared
components are transpiled C#, grows 0.35% in gzip (385,119 to 386,456 bytes, measured with gzip -9).

## Principles

The transpiler answers as .NET answers, or fails the build where it cannot (docs/PRINCIPLES.md). A
divergence documented beside working code is still a divergence an app meets in silence.
