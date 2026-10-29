## Context

A collection expression takes its shape from the type it is converted to. `CollectionExpressionStrategy` wrote
an array for every target but a set and a sorted set, which it had learned to build from #384 and #531. An array
is right for an array, a `List` and the faces a list answers to; it is wrong for every collection the browser
holds as a class of the runtime's own, which `BoundaryShape` names: the dictionaries (`DictionaryName`) and the
queue, the stack and the linked list (`CollectionClass`). The constructions of those classes already exist,
`DictionaryStrategy` for the dictionaries and `QueueStackStrategy` for the others.

Measured with Roslyn 5.9.0 at `LanguageVersion.Preview`: for the six types, `[]`, `[with(10)]` and
`[with(capacity: 3)]` compile, and an element or a spread does not (CS9215, CS1061), unless the app declares an
`Add` extension. An `IDictionary` or `IReadOnlyDictionary` target does not compile (CS9174), and this Roslyn has
no dictionary expression (`["a": 1]`).

## Goals / Non-Goals

**Goals:** `[]` and `[with(…)]` build the class `new()` and `new(capacity)` build, wherever the expression is
written: a local, a field, a property's initializer, an object initializer's member, an argument, a return, a
branch of a conditional.

**Non-Goals:** `with(…)` arguments other than a capacity, which the strategy's own fence refuses with EQ2007 for
every target as before, a comparer included where `new(comparer)` builds (#577 lifted that for the
constructor). The capacity of a list or a set built by `[with(…)]`, which stays dropped. A lookup's `Contains`,
found while pinning the inherited names (#1145).

## Decisions

- **How Flutter does it.** Dart's collection literals take their class from the literal's form (`[]` a
  `List`, `{}` a `Map` or a `Set` by context), never from a declared type, so there is no row to follow; C#'s
  rule is the target type, and the strategy reads it, as it already does for a set.
- **One construction per class.** The expression asks each class's own strategy for its empty construction
  (`DictionaryStrategy.Empty`, `QueueStackStrategy.Empty`) rather than spelling the factories a second time, so
  a dictionary's key equality and a sorted one's ordering are decided where `new()` decides them.
- **An element is refused, not guessed.** An element reaches one of these types only through an extension
  `Add` the app declares, which C# calls per element. Nothing writes that call here, and an array would answer
  none of the class's members, so the build fails with EQ1004.
- **The local's annotation follows `new()`'s.** A dictionary's factory given nothing infers `unknown` keys and
  values, and `new()`'s local degrades to `any` for that reason; `[]`'s does too. A queue, a stack and a linked
  list are left to inference, as `new()`'s are.
