## Why

Closes #1145, a bug under #849 (the transpiler's fences hold on every path, part three), under the epic #157.
`ILookup<TKey, TElement>.Contains(key)` answers whether the lookup holds a group of that key. On the web it lowered
as a collection's `Contains`, `$eq.collections.contains(l, 'b', 'item')`, which looks for an ELEMENT equal to the
key among the groups `ToLookup` builds (each group an array carrying its `key`), so it answered false for every
key: `new[] { "a", "b", "a" }.ToLookup(s => s).Contains("b")` was `True` in .NET and `False` in the browser.
`ContainsStrategy` claims every `Contains` a `System.Linq` type declares, and the lookup's own is one of them. The
lookup's indexer was already right, and found the group by its key.

## What Changes

For a developer using the SDK, nothing to write: `lookup.Contains(key)` answers in the browser what it answers in
.NET.

- **A lookup's `Contains(key)` asks for a group of that key**, comparing keys as the lookup's indexer and
  `ToLookup` do: a record, a decimal and a date by value, a NaN equal to itself, a class by its own `Equals`, and a
  null key a key like any other. The key is evaluated once, and `lookup?.Contains(key)` answers null for a null
  lookup.
- **The lookup's two members are one strategy**: `LookupIndexerStrategy` becomes `LookupStrategy`, which answers
  the indexer and `Contains` with one search over the groups, `find` for the group and `some` for whether there is
  one. The indexer's output does not change by a byte.

The part it reaches: eqc (`LookupStrategy`, `CSharpToJsConverter`'s registration). The runtime does not change.
The public surface moves in `eQuantic.UI.Compiler` only, a `tool` assembly whose removals are free:
`LookupIndexerStrategy` goes and `LookupStrategy` replaces it. The developer surface does not move. Nothing an app
wrote breaks: the call that changes answered false for every key on the web.
