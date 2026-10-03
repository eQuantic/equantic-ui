# Proposal

Closes #516, a sub-issue of #164 ("The transpiler's fences hold on every path", under the epic #157).

## Why

A component value typed `HashSet<T>` reaches the browser as the JSON array the server's serializer
writes, while every read eqc emits on it is a Set's (`size`, `has`, `add`). So a prefetched
`HashSet<string> _roles` draws on the server and throws on `_roles.Contains("admin")` after the page
hydrates. Measured on 272c8c2b with the embedded bun: the witness path, `hydrateValue(new Set(), […])`,
returns an `Array`, and the spec path for a `HashSet<long>` is `['long']`, a list.

The same holds for every collection the browser keeps as one of its own classes: a `SortedSet`, a
`Queue`, a `Stack` and a `LinkedList` are runtime classes (`$eq.collections`), and each crosses as an
array no code of theirs reads. And #515 refuses a set in a server value's projection with EQ2114
until a set can cross.

## What Changes

- A value of a collection type the browser holds as its own class reaches the browser's code as that
  class: a `HashSet`, an `ISet` or an `IReadOnlySet` as a JavaScript `Set`, and a `SortedSet`, a
  `Queue`, a `Stack` and a `LinkedList` as the runtime's, each element hydrated by its own type, and a
  stack with its top coming off first, as on the server. Nothing the developer writes changes.
- A server value's projection takes a set, or one of those collections, of scalars as a value that
  crosses whole, as it takes a list or a dictionary: `report.Roles.Contains("admin")` builds and
  answers as on the server, where #515 refused it.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `hydration-contract`: which collections cross whole, and the requirement that each reaches the
  browser as the class its code reads.

## Impact

- **eqc**: the hydration spec writes a collection spec for each such type, always, as it does for a
  dictionary.
- **The runtime**: `hydrate` rebuilds the class from the array.
- **The source generator**: the projection's leaves.
- **Both**: the names of those collection types live in `src/Shared/BoundaryShape.cs`, which both
  read.
- **Public surface**: none moves. The developer surface does not either.
- **Break**: none. A value that crossed as an array and threw now crosses as its class.
