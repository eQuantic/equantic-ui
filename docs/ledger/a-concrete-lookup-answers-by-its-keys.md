- **2026-10-29 · A concrete lookup answers its indexer and Contains by its keys**: a bug under #849
  ([#1168](https://github.com/eQuantic/equantic-ui/issues/1168)), found reviewing #1145. A receiver typed as the
  concrete `Lookup<TKey, TElement>`, the class `ToLookup` builds, reached by a cast, an `as` or a type pattern, was
  not the lookup eqc knew: its `Contains(key)` was an element search among the groups, false for every key, and its
  indexer an array's, which threw. `LookupStrategy` claims the class beside `ILookup<TKey, TElement>`, so both
  members search the groups by the key type's equality as the interface does. `ContainsStrategy` takes a
  `System.Linq` `Contains` only when it is an extension, LINQ's own, so a lookup's `Contains` no strategy claims is
  EQ2004 at build rather than a quiet `false`. Proposed and archived through OpenSpec (`transpiler-sequences`).
