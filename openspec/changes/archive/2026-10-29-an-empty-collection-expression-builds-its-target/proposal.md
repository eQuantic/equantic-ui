## Why

Closes #1144, a bug under #849 (the transpiler's fences hold on every path, part three), under the epic #157.
`Dictionary<string, int> ids = [];`, the form the IDE suggests for `new()` (IDE0028, IDE0300), lowered to a
JavaScript array, and so did a `SortedDictionary`, a `SortedList`, a `Queue`, a `Stack` and a `LinkedList`
started with `[]` or `[with(…)]`. Each of them is a class of the runtime's on the web, which `new()` builds, and
the array answers none of its members: a dictionary's first write threw (`map.set is not a function`) and its
`Count` was null, a queue had no `enqueue`, and a stack's `Count` was empty. `CollectionExpressionStrategy` read
the converted type for a set and a sorted set only.

The report that led here said a `Dictionary<string, T>` lowers to a plain object, whose `__proto__` key goes to
the prototype's setter, so a diff engine interning `__proto__` lines through one never matched them on the web.
Measured on main, it does not: every dictionary has been the runtime's `Dictionary`, held by a `Map`, since
#443, and every construction form stores and reads `__proto__`, `constructor`, `toString`, `hasOwnProperty` and
`valueOf` as .NET does, the code diff engine's twin included. Searching the construction sites that report
named turned up this one, which builds no dictionary at all.

## What Changes

For a developer using the SDK, nothing to write: a collection expression builds on the web the collection its
target names, as it does in .NET.

- **An empty collection expression whose target is a dictionary, a queue, a stack or a linked list builds the
  runtime's class**, as `new()` builds it. None of these types has the `Add` a collection expression adds an
  element by, so C# allows only `[]` and `[with(…)]`; the capacity `with(…)` hands the constructor is kept
  where the class keeps one (a dictionary, a queue, a stack) and dropped where its construction drops it (a
  sorted dictionary or list).
- **An element added by an `Add` of the app's own** (an extension over one of these types) is refused with
  EQ1004, where an array was written.
- **A local started with `[]` and typed as a dictionary** is annotated `any` in TypeScript, as one started with
  `new()` is.
- **Keys named like what every JavaScript object inherits are pinned** through every construction form, and
  through the code diff engine, by conformance tests that execute both sides.

The part it reaches: eqc (`CollectionExpressionStrategy`, `DictionaryStrategy`, `QueueStackStrategy`,
`LocalDeclarationStrategy`). The runtime does not change, and neither the public surface nor the developer
surface moves. Nothing an app wrote breaks: the form that changes threw or answered wrong on the web.
