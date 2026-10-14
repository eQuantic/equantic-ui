# Proposal

Closes #864 and #865, two Bugs under #845 (The transpiler's fences hold on every path, third part).

## Why

Executed on both sides, `SortedDictionary` and `SortedList` answered as .NET's do only while nobody
held their keys or values, and `SortedSet.GetViewBetween` did not compile:

- `Keys` and `Values` were arrays copied when they were read, so a view read after a change held what
  the collection held before it, and a walk over the keys that added a key ran on where .NET refuses it;
- the walk over the pairs refused a change in CoreLib's words, where the two live in System.Collections
  and refuse in its own ("Collection was modified after the enumerator was instantiated."), and a sorted
  dictionary's walk went on after a `Remove` of a key it did not hold, which .NET's red-black tree counts
  as a change;
- a change asked of a view (`ICollection<T>.Add`, `Clear`, `Remove`, a key written through a sorted
  list's `Keys[0]`) changed a copy, or was a TypeError, where .NET throws a `NotSupportedException`;
- `GetViewBetween` was refused with EQ2004 since #876, and was a TypeError before it;
- `TryAdd` on a sorted dictionary, which C# binds to `CollectionExtensions`' extension, went out under that
  class's name, which nothing defines in the browser: a ReferenceError.

## What Changes

- `Keys` and `Values` of a sorted dictionary and a sorted list are live views, read when they are read,
  as a dictionary's have been since #463, through one view the runtime's three dictionaries share. A walk
  over one ends at any change, as the walk over the pairs does, in System.Collections' words.
- A sorted dictionary counts what its tree counts: a `Remove` of a key it does not hold while it holds
  any, and an `Add` of a key it holds, before it refuses it. A sorted list counts neither. A
  `Remove(key, out value)` of a key it does not hold removes nothing, as `CollectionExtensions` asks for
  the key first, where a discarded out lowered to a `Remove(key)`.
- A view refuses a change in its collection's words: a sorted dictionary's as a dictionary's ("Mutating a
  key collection derived from a dictionary is not allowed."), a sorted list's call as its nested types
  ("This operation is not supported on SortedList nested types because they require modifying the
  original SortedList."), and a key written through a sorted list's `Keys[i]` in a dictionary's words.
  `Remove` is refused whether or not the view holds the value, where a dictionary's view answered false
  for a value it did not hold.
- `SortedSet.GetViewBetween(lower, upper)` answers a sorted set over that range of its set, which shares
  the set's elements, so each sees what the other changes. It refuses an element outside its range, bounds
  out of order and a view of a view outside its range as .NET does, and takes .NET's paths through the set
  operations: it never merges, a set compared with it asks it for a view of its own range, and its
  `ExceptWith` asks the count it last counted, which is taken again where .NET reads the view as a
  collection: its `Count`, a copy into a list or a set, and a set's operation that asks it its count.
- A dictionary's `TryAdd` is the runtime class's, through the extension and its static form alike, and a
  lookup or a `TryAdd` is the dictionary's only when the call binds to System.Collections.Generic's: an
  extension of the app's own under one of those names was lowered as the dictionary's own.

## Impact

- Runtime: `utils/view.ts` (new, the live view), `utils/dictionary.ts`, `utils/sorted.ts`, and
  `utils/linq.ts` and `utils/list.ts`, which read a sorted set's count before they copy it, and
  `utils/collections.ts`, whose `Remove` asks a view to refuse before it searches.
- eqc: `QueueStackStrategy` lowers `GetViewBetween`, and `DictionaryStrategy` with `DictionaryLookup`
  lowers `TryAdd` through `CollectionExtensions`, asks a lookup's method which type declares it, and
  lowers a `Remove(key, out _)` that finds nothing to no change. The BCL surface baseline moves
  `GetViewBetween` from `fenced:EQ2004` to `native`.
- No public or developer surface moves: the runtime's TypeScript and eqc's lowering are not in
  `PublicAPI.Shipped.txt` nor in the developer-surface baseline.
- Known divergences: LINQ reads a sorted set's count before every operator, where .NET's `First` or
  `Where` only walk it, so a view's `ExceptWith` after one of those may remove what it was asked to where
  .NET's, with a count never read, removes nothing; and `List<T>.AddRange` does not read it, where .NET's
  does, so a view's `ExceptWith` after one may remove nothing where .NET's removes.
