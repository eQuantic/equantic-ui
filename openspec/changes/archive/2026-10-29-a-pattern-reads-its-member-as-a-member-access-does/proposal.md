## Why

Closes #1147, a bug under #849 (the transpiler's fences hold on every path, part three), and #920 under #845,
the fence half of the same rule. A property pattern names a member, and eqc wrote that member's read with a
table of its own in `PatternConverter`, where a member access of it goes through the member-access strategies.
The two disagreed, and the pattern was the wrong one. Measured with the conformance harness, each of these is
`True` in .NET and was `False` in the browser:

- a pattern nested in another, in a positional part or in a list's element:
  `h is { Roles: { Count: 1 } }` over a `HashSet<int>`, `{ Inner: { Count: 2 } }` over an app's
  `record Stats(int Count)`, `(set, 3) is ({ Count: 2 }, 3)`, `all is [{ Count: 1 }]`. The type of the value
  read from was handed down from the subject alone, so a nested pattern had none, and `Count` read an
  array's `length`;
- a tuple's element, `(1, 2) is { Item1: 1, Item2: 2 }`, read as a field where a tuple is an array;
- a dictionary's keys and values, `map is { Keys.Count: 1, Values.Count: 1 }`;
- and a .NET member no browser value carries, `list is { Capacity: 8 }`, was written with no diagnostic,
  where its member access fails the build with EQ2004 since #847 (#920).

## What Changes

For a developer using the SDK, a property pattern answers in the browser as it answers in .NET, at any depth,
and a member the browser cannot read is refused in a pattern as it is in a member access:

- eqc reads a property pattern's member as a member access of it: the read is written as an ordinary member
  access, on a placeholder that stands for the value already translated, and handed to the converter, so
  every strategy that translates `x.Count`, `t.Item1`, `map.Keys` or a field in its slot translates the
  pattern's read too, and every fence that refuses one refuses the other. The pattern's own table goes.
- The type of the value a pattern reads from is the bound pattern's (`IRecursivePatternOperation.NarrowedType`),
  asked of each pattern, so a nested one knows it as the outermost does. A positional pattern over a record
  nested in another reads its parts by name from it too.
- A .NET member no value in the browser carries, read in a pattern, fails the build with EQ2004.

The part it reaches is eqc (`PatternConverter` and its three callers). The public surface of
`eQuantic.UI.Compiler`, a tool, moves: `PatternConverter.BuildCondition` and `PatternConverter.CollectBindings`
lose their `accessType` parameter, which the bound tree replaces. The developer surface does not move. An app
whose pattern reads a member no browser value carries now fails its build with EQ2004, where it was quietly
false; the migration line is the one EQ2004 gives: read it in a `[ServerAction]`.
