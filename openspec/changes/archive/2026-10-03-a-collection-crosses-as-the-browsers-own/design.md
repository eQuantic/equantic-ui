# Design

## Context

See proposal.md for what fails. The constraints:

- System.Text.Json writes every one of these collections as a JSON array, in the order the
  collection enumerates. Measured: a `Stack` of 1, 2, 3 is `[3,2,1]` (its top first), a `Queue` is
  front first, a `SortedSet` sorted, a `HashSet` in insertion order.
- eqc holds a `HashSet`, an `ISet` and an `IReadOnlySet` as a JavaScript `Set` (`HashSetStrategy`),
  and a `SortedSet`, a `Queue`, a `Stack` and a `LinkedList` as the runtime's classes
  (`$eq.collections`), each built from an iterable. The runtime's `Stack` keeps its top at the end of
  its array.
- A dictionary already crosses this way: its spec is never the identity, because a JSON object always
  has to become the runtime's dictionary class.

## Goals / Non-Goals

**Goals:** one spec shape for every such collection, decided by the type's symbol from one list
that the generator reads too.

**Non-Goals:** a collection with a custom comparer, which the serializer does not carry either; an
app's own collection type, which crosses as what its twin declares.

## Decisions

### The build rebuilds the class, the developer converts nothing

Flutter has no hydration. Dart's `jsonEncode` refuses a `Set` outright, and a Flutter app converts
one to a list by hand and back. Here the build knows the type at both ends of the wire, so it does
that conversion itself, and a developer's `HashSet` stays a `HashSet` on both sides.

### One spec shape: `{ collection, of }`

`collection` names the class (`set`, `sortedSet`, `queue`, `stack`, `linkedList`) and `of` the
element's spec, null when an element arrives as it is. The spec is always written, as a dictionary's
is, since the array has to become the class even when no element needs coercing. A stack is rebuilt
from the reversed array, so its top comes off first, as on the server. A value that is already the
class passes through, so hydrating twice is harmless.

Alternative considered: a witness branch in `hydrateValue` (rebuild from the field's default). It
answers only where a field has a default of the right class, and never for a projection, which has
no default. The spec answers everywhere the compiler knows the type, which is everywhere.

### The list of collection types lives in `BoundaryShape`

The compiler's spec and the generator's leaves must agree, or a projection accepts a leaf the browser
cannot rebuild, which is the drift #515's review kept finding. So the names go in
`src/Shared/BoundaryShape.cs` beside the sequence and dictionary names, and both read it.

## Risks / Trade-offs

- [A collection's elements are objects, and a JS `Set` compares them by reference] → this is how eqc
  holds a `HashSet` already. Crossing changes nothing about it.
- [A new collection type gets a runtime class without a name in the list] → it keeps crossing as an
  array, as today. The conformance case for each listed type is the template to extend.
