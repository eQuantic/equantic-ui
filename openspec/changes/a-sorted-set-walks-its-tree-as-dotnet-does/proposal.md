## Why

Closes #766, a Bug under #1174.

`SortedSet<T>.RemoveWhere` asked its predicate in the set's order in the browser, where .NET asks it
breadth first through the red-black tree it holds the set in. A predicate with no effect of its own
answered alike; one that keeps a record, or throws partway, saw another order:
`new SortedSet<int> { 1, 2, 3, 4, 5, 6, 7 }` is asked 2,1,4,3,6,5,7 in .NET and was asked 1,2,3,4,5,6,7.
The order is the tree's shape, which its history decides, so the runtime's sorted array could not answer
it. Measured on main, 13 of the 14 conformance cases this change adds fail there.

## What Changes

- **A sorted set holds .NET's red-black tree** (`utils/sorted-tree.ts`), node for node: an `Add` splits
  the 4-nodes on its way down, a `Remove` turns the 2-nodes on its way down whether it finds the element
  or not, a merging `UnionWith` or `IntersectWith` builds the tree anew from the sorted array, and a
  view's `Clear` removes its elements from the last its breadth-first walk reaches.
- **`new SortedSet<T>(collection)` is .NET's constructor**: a sorted set of the same comparer, not a
  view, is copied tree and all; any other sequence is sorted as .NET's `Array.Sort` sorts it, the first
  of each run of equal elements kept, and its tree built from the sorted array. It added the elements
  one by one, so its tree was another, and of two equal elements (`1.0m`, `1.00m`) it kept the first
  walked. A union into an empty set copies the other as that constructor does.
- **`RemoveWhere` walks the tree breadth first**, a view's from the root of its range, and removes what
  matched from the last it asked.
- `Min` and `Max` walk down one side of the tree, a view's within its range.

For a developer using the SDK: nothing they write changes. A sorted set answers in the browser what it
answers in .NET, the order its `RemoveWhere` asks in included.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `runtime-collections`: a new requirement, "A sorted set holds .NET's red-black tree".

## Impact

- **The runtime**: `utils/sorted.ts` (`SortedSet`) and the new `utils/sorted-tree.ts`. The served
  runtime grows by about 1.8 KB gzipped (+0.56%), within its budget.
- **eqc, the server, the public surface and the developer surface**: unchanged.
