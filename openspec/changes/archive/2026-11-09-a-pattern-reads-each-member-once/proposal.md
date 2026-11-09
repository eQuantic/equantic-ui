## Why

Closes #1150, a bug under #849 (the transpiler's fences hold on every path, part three), under the epic #157.
.NET evaluates a pattern-matching operation as one decision graph, which fetches each member a property pattern
reads once for the operation, however many alternatives, conjuncts, bindings, arms or sections read it. eqc
wrote the member's read again at every mention, so a getter that runs code ran it again. Measured on the
module-graph harness with a getter that counts its reads, `c is { Value: 2 or 1 }`,
`c is { Value: 1 } and { Value: > 0 }`, `c is { Value: > 0 and var v }`, a switch expression's arms over
`{ Value: … }` and a switch statement's sections answered 21 where .NET answers 11, and
`c is { Corner: { X: 2 } or { Y: 2 } }` answered 41. #1157 (#1149) read the pattern's input once and left the
members it reads inside to this change.

## What Changes

For a developer using the SDK, nothing to write: a pattern reads each member it tests once in the browser, as
.NET does, whether the getter is the app's own, an abstract one, a virtual auto-property an override answers
for or an interface's, and whether it answers a value or null.

- **The operation holds a member's read as it holds a `Deconstruct`'s parts.** `MatchParts` keeps a temporary
  for each member of each value the operation reads whose read runs the app's code, assigned by the first test
  that reaches it and read by every other test and binding, declared where the operation already declares its
  temporaries (the switch expression's arrow, the switch statement's block, the `is`'s own arrow), so each
  evaluation of the operation starts it over.
- **What is held is a box around the value.** A member may answer null, so the temporary holds `[value]`, which
  never is, and a null member is read once too.
- **A member nobody can observe being read is written in place**, as it was: a field, an auto-property the twin
  keeps under its own name, and a property of the platform's read off a value of the platform's (a list's
  `Count`, a string's `Length`).

The parts it reaches: eqc only (`MatchParts`, `PatternConverter`, `PropertyStore`). The public surface does not
move, nor does the developer surface. Nothing an app wrote breaks. What a page does changes only where a property
pattern reads a member whose getter has an effect.
