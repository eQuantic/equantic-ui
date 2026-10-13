# Proposal

Closes #866, a Bug under #845 (The transpiler's fences hold on every path, third part).

## Why

Executed on both sides, 73 of the 74 cases of `CollectionSurfaceConformanceTests` fail before this
change. eqc lowered every member of `Queue<T>`, `Stack<T>`, `LinkedList<T>`, `LinkedListNode<T>` and
`SortedSet<T>` to its camel-cased name, whether or not the runtime's twin carried it, so a member the
twin lacked compiled without a diagnostic and was a TypeError in the browser:

- `TryDequeue`, `TryPeek`, `TryPop` and a sorted set's `TryGetValue`, the idioms that drain a queue or a
  stack among them;
- a queue's and a stack's `Capacity`, `EnsureCapacity` and `TrimExcess`;
- a linked list's `Find`, `FindLast`, `AddAfter`, `AddBefore` and `CopyTo`.

Two more answered wrongly without an error: `LinkedList.Remove(node)` looked for the node among the
values and removed nothing, and `new LinkedListNode<T>(value)` built a linked list holding the value.
Members the runtime is not to carry, such as `SortedSet.GetViewBetween` (#865), `Comparer` and an
explicit `GetEnumerator()`, also went out as calls that failed in the browser.

## What Changes

For a developer, every member of the five types either answers as .NET's does or fails the build:

- `TryDequeue`, `TryPeek`, `TryPop` and `TryGetValue` answer whether there was an element and write it,
  or the type's default, into the `out` argument: a variable, a discard, an array element or a field,
  each part read once and in place.
- A queue and a stack keep .NET's capacity: grown by each one's own rule, sized by a construction from a
  sequence as .NET sizes it, and read, ensured and trimmed by `Capacity`, `EnsureCapacity` and
  `TrimExcess`, with .NET's refusals. A queue whose capacity changes ends a walk over it, as .NET's does,
  and a stack's does not.
- A linked list links by its nodes: `Find`, `FindLast`, `AddAfter`, `AddBefore`, the `AddFirst`,
  `AddLast` and `Remove` that take a node, and `CopyTo`, with .NET's refusals for a node that is null,
  belongs to another list or to none, or already belongs to one. A node removed, or cleared with its
  list, belongs to no list and links to nothing. `new LinkedListNode<T>(value)` builds a node.
- Any other member of the five fails the build with EQ2004, naming the member and listing what the
  runtime carries. `ToString`, `Equals` and `GetHashCode` stay every object's.

## Impact

- eqc: `QueueStackStrategy` moves onto the IR and claims each of the five types by a members table, so
  the BCL audit's verdicts are true: `SortedSet<T>.Comparer` and `GetViewBetween` go from native to
  fenced, and the queue's and stack's capacity members, which the audit already counted as native, now
  answer. `QueueStackStrategy`'s public surface moves to `IExpressionIrStrategy`'s members.
- Runtime: `utils/collections.ts` (`Queue`, `Stack`, `LinkedList`, `LinkedListNode`) and
  `utils/sorted.ts` (`SortedSet.tryGetValue`); `$eq.collections.linkedListNode` is new.
- The developer surface does not move. An app that called one of the refused members compiled and
  failed in the browser; it now fails the build, which names the member.
- Not reached: a LINQ operator still reads a queue or a stack into an array first, so an eager LINQ
  result's capacity is its count where .NET doubles from four (#685).
