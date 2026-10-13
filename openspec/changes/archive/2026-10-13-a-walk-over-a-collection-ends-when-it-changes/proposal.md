# Proposal

Closes #765, a Bug under #565 (The transpiler's fences hold on every path, continued).

## Why

Executed on both sides at 9b43eb2b, 25 of the 36 cases of `CollectionWalkConformanceTests` fail. A
`foreach` over the runtime's `Queue`, `Stack`, `LinkedList` or `SortedSet` went on after the collection
changed under it:

- a queue and a stack walked a copy, so they walked the elements they had and refused nothing;
- a linked list and a sorted set walked themselves, so `foreach (var x in l) l.AddLast(x);` and
  `foreach (var x in s) s.Add(x + 10);` never ended in the browser, where .NET refuses the change at the
  next step;
- a sorted set's `Reverse()` was a TypeError: the set had no such member in the browser.

.NET's enumerators compare the collection's version at every step, the one after the last element
included, and refuse a change with an `InvalidOperationException` in the words of the assembly the
collection lives in: CoreLib's for `Queue<T>` ("Collection was modified; enumeration operation may not
execute."), System.Collections' for `Stack<T>`, `LinkedList<T>` and `SortedSet<T>` ("Collection was
modified after the enumerator was instantiated."). The runtime's `HashSet` and `Dictionary` already
refuse what .NET's refuse.

## What Changes

- Each of the four collections keeps a version, bumped where .NET bumps its own: a queue by `Enqueue`,
  `Dequeue` and `Clear`; a stack by `Push`, `Pop` and `Clear`; a linked list by every node added or
  removed and by `Clear`, a value written into a node being no change; a sorted set by every `Add`, by
  every `Remove` of a set that holds anything, each whether or not it changes the set, by `Clear`, and
  by a set operation along the path .NET takes through it, which asks `Contains` before adding or
  removing an element of a sequence and does not ask it of a sorted set's elements.
- Each walks the collection as it is and ends the walk at the step after a change with .NET's
  exception, in its assembly's words.
- `SortedSet.Reverse()` walks from the largest element, reading the set when it is walked and each
  time it is, and refuses a change as the forward walk does.

## Impact

- Runtime: `utils/collections.ts` (`Queue`, `Stack`, `LinkedList`), `utils/sorted.ts` (`SortedSet`) and
  `utils/dictionary.ts`, which gains the refusal in System.Collections' words.
- Not reached: eqc, the shared components' modules, `List<T>` (#863), `HashSet`, `Dictionary`,
  `SortedDictionary` and `SortedList` (#864), `SortedSet.GetViewBetween` (#865), and the members the
  four collections' twins lack, `TryDequeue` and `TryPop` among them (#866). A LINQ operator
  over one of these collections reads it whole before its first callback, so a change in the loop's
  body reaches no walk there, as before (#685).
