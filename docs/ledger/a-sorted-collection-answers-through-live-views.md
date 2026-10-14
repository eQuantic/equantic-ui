- **2026-10-14 · A sorted collection answers through live views, and a sorted set gives a view of a range**:
  a `SortedDictionary`'s and a `SortedList`'s `Keys` and `Values` were arrays copied when read, their
  walks refused a change in CoreLib's words and went on where .NET's red-black tree counts a change, a
  change asked of a view changed a copy, `TryAdd` on a sorted dictionary was a ReferenceError, and
  `SortedSet.GetViewBetween` failed the build with EQ2004. The three dictionaries now share one live view
  that refuses a change in each collection's words, and a view of a sorted set shares its set's elements
  and takes .NET's paths through the set operations, the count its `ExceptWith` reads included. The
  review of the diff found that a lookup went to the dictionary under its name alone, an extension of the
  app's own included, and that a `Remove(key, out _)` which found nothing counted as a change; both
  follow .NET now; 110 conformance cases executed on both sides
  ([#864](https://github.com/eQuantic/equantic-ui/issues/864),
  [#865](https://github.com/eQuantic/equantic-ui/issues/865)). Proposed and archived through OpenSpec
  (`openspec/specs/runtime-dictionaries`, `runtime-collections`, `transpiler-bcl`).
