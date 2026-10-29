- **2026-10-29 · A property pattern reads its member as a member access reads it**: a bug under #849
  ([#1147](https://github.com/eQuantic/equantic-ui/issues/1147)), with the fence half of the same rule under #845
  ([#920](https://github.com/eQuantic/equantic-ui/issues/920)). `PatternConverter` named a pattern's member with a
  table of its own, where a member access goes through the member-access strategies, and the type of the value
  read from was handed down from the subject alone. A nested pattern had none, so `{ Roles: { Count: 1 } }` over a
  set and `{ Inner: { Count: 2 } }` over an app's record counted an array's `length`, in a positional part and a
  list's element too; a tuple's `Item1` was read as a field; a dictionary's `Keys` as a member the Map has not; and
  `list is { Capacity: 8 }` was written with no diagnostic. Each was false in the browser where .NET said true. A
  pattern's read is now a member access on a placeholder that stands for the value, handed to the converter, with
  the type the bound pattern narrows to at every depth, so every strategy and fence of a member access holds for
  it, and EQ2004 refuses what the browser cannot read. `PatternConverter.BuildCondition` and `CollectBindings`
  lose their `accessType` parameter. Proposed and archived through OpenSpec (`transpiler-expressions`).
