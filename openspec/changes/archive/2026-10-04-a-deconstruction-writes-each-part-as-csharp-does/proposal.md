# Proposal

Closes #542, a sub-issue of #164.

## Why

A deconstruction was lowered to JavaScript destructuring, which writes each part straight into its
target, so two things C# does at each part did not happen. A target whose write is a call could not
be a destructuring target: `(map["a"], map["b"]) = point` over a dictionary wrote the entry's read,
`$eq.mapGet(map, "a")`, as the target, a SyntaxError that cost the whole module. And a part was not
converted to its target's type: `long total; int n; (total, n) = rec` left a number where every long
is a BigInt, and the next `total + 1L` threw a TypeError. The same held for a declaration
(`(long t, int m) = pair`) and a loop (`foreach ((long a, int b) in pairs)`).

## What Changes

- Each part reaches its target as C# puts it there: converted to the target's type through the
  conversion the bound tree's `DeconstructionInfo` names for it (`ValueFlow.Apply`), and written by
  what the target is, a dictionary's entry through its class (`$eq.mapSet`), with its receiver and
  key evaluated before the value, as every captured target already was.
- An assignment with such a part is lowered with temporaries, the shape a captured target already
  takes, and a declaration or a loop declares a converted part after the destructuring, from its
  temporary.
- A deconstruction with neither keeps the destructuring it had.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `transpiler-records`: a deconstruction's part is converted to its target's type and written by
  what its target is.

## Impact

`DeconstructionPattern` (each part's write and conversion), `AssignmentExpressionStrategy` (when the
temporaries are needed) and `ForEachVariableStatementStrategy` (a converted loop part). Nothing an app
writes changes. Proved by `DeconstructionConformanceTests.APart_IsWrittenAndConvertedAsItsTargetTakesIt`
on both sides.
