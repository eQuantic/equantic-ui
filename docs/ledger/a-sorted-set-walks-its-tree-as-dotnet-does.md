- **2026-11-10 · A sorted set walks its tree as .NET does**: a Bug under #1174
  ([#766](https://github.com/eQuantic/equantic-ui/issues/766)). `SortedSet<T>.RemoveWhere` asked its
  predicate in the set's order, where .NET asks it breadth first through its red-black tree, so a
  predicate that keeps a record saw another order. The runtime's set held a sorted array; it holds .NET's
  tree now (`utils/sorted-tree.ts`), built by the same insertion, removal, copy and construction from a
  sorted array, and `new SortedSet<T>(collection)` takes .NET's path, sorting before it builds. Measured
  on main, 13 of the 14 conformance cases first fail there. Proposed and archived through OpenSpec
  (`runtime-collections`).
