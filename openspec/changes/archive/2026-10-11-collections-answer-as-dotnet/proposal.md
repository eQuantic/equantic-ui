# Proposal

Closes #723 and #726, Bugs under #565 (The transpiler's fences hold on every path, continued). Both were
found by #715's null-argument measurement: a member the runtime's collection lacked, and a constructor
whose twin read a null source as no collection.

## Why

Executed on both sides at a7d5f3d, where 75 of the 83 cases of `CollectionMemberConformanceTests` fail:

- `Queue<T>.CopyTo`, `Stack<T>.CopyTo` and every `SortedSet<T>.CopyTo` were a `TypeError` in the
  browser: the runtime's twins had no `copyTo` (#723).
- `SortedSet<T>` had none of `UnionWith`, `IntersectWith`, `ExceptWith`, `SymmetricExceptWith`,
  `IsSubsetOf`, `IsProperSubsetOf`, `IsSupersetOf`, `IsProperSupersetOf`, `Overlaps`, `SetEquals` or
  `RemoveWhere`, each a `TypeError` too (#723).
- `new Queue<int>(source)`, `Stack`, `LinkedList`, `SortedSet`, `Dictionary`, `SortedDictionary` and
  `SortedList` built an empty collection from a null source, where .NET refuses it with an
  `ArgumentNullException` naming `collection` or `dictionary`; `new List<int>(source)` spread it, a
  `TypeError` (#726).
- `new Queue<int>(-1)` and `new Stack<int>(-1)` built a collection where .NET refuses the capacity, and
  a string copied into a queue, a stack, a list or a linked list was read by its code points, so
  `new List<char>("a\U0001F600b").Count` was 3 where .NET counts 4 UTF-16 chars.

## What Changes

- The runtime's `Queue` and `Stack` gain `copyTo`, and its `SortedSet` gains `copyTo`, the four set
  operations, the six questions and `removeWhere`, each .NET's algorithm with .NET's refusals in .NET's
  order. Of two elements the set's comparer finds equal, a union and an intersection keep the instance
  .NET keeps (`1.0m` or `1.00m`): the other set's where .NET merges two sets of one comparer, and the
  set's own, or the first added, where it adds the other's elements one by one.
- The factories `$eq.collections.queue`, `stack` and `linkedList` take no argument, a capacity or a
  collection, as the constructors do, and refuse a null collection and a negative capacity in .NET's
  words. `sortedSet`, `dictionary`, `sortedDictionary` and `sortedList` take the name of the parameter
  a copied source binds to, which eqc reads from the bound constructor, and refuse a null source by it.
- `new List<T>(collection)` is lowered to a new twin, `$eq.collections.listCopy`, instead of a spread.

For a developer: these members answer in the browser what they answer in .NET, and a null handed to a
collection's constructor throws the exception .NET throws, where the browser built an empty collection.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `runtime-collections`: a queue, a stack and a sorted set copy and combine as .NET's do, and a
  collection's constructor refuses a null source by its parameter's name.

## Impact

- eqc: the sorted set's construction (`QueueStackStrategy`), the dictionaries' (`DictionaryStrategy`)
  and the list's (`ObjectCreationStrategy`), and one `Eq` constant, `ListCopy` (public surface:
  `PublicAPI.Unshipped.txt`).
- Runtime: `utils/collections.ts`, `utils/sorted.ts`, `utils/dictionary.ts`, `utils/list.ts` and
  `$eq.collections.listCopy`.
- Not reached: the web realizer, Photon, the SDKs and the templates. The developer surface does not move.
- #715's null-argument baseline only shrinks.
- Left for issues of their own: a queue, a stack, a sorted set and a linked list keep enumerating after
  they change, where .NET's enumerators refuse it (#765); and `RemoveWhere` asks its predicate in the
  set's order, where .NET walks its red-black tree breadth first (#766).
