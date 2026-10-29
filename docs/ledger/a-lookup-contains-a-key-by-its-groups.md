- **2026-10-29 · A lookup's Contains asks for a group of that key**: a bug under #849
  ([#1145](https://github.com/eQuantic/equantic-ui/issues/1145)). `ILookup<TKey, TElement>.Contains(key)` lowered
  as a collection's `Contains`, `$eq.collections.contains(l, key, 'item')`, which looked for an element equal to
  the key among the groups `ToLookup` builds, so it answered false for every key. eqc answers it now from the
  lookup's own groups, with the search its indexer already made: `some` where the indexer runs `find`, by the key
  type's equality, so a record, a decimal and a date are found by value, a NaN equal to itself, a class by its own
  `Equals` and a null key like any other, the key evaluated once. The lookup's two members are one strategy,
  `LookupStrategy`, which replaces `LookupIndexerStrategy` in the compiler's surface, and the indexer's output
  does not change. Proposed and archived through OpenSpec (`transpiler-sequences`).
