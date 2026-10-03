# Proposal

Part of #516, under #522's review: the third round of the pull request found that a sorted collection
of an enum orders its members' names, and that a queue's and a stack's `Contains` compare references.

## Why

- An enum's member crosses as its camelCase name, and the ordering table had no order for it, so a
  `SortedSet<Rank>` or a `SortedDictionary<Rank, T>` built or hydrated in the browser ordered the
  names by `<`: `Alpha` before `Zeta` whatever their values. `Max` and `Min` refused an enum for the
  same reason.
- `Queue.Contains` and `Stack.Contains` used `Array.includes`, so a decimal or a date was found only
  by the same object, and one that crossed hydration never matched an equal one built in the browser.

## What Changes

- The ordering table writes a non-flags enum's members out with their values, and a flags enum (a
  number in the browser) orders as one. `Max` and `Min` order an enum by its value.
- A queue and a stack find a member as `EqualityComparer<T>.Default` does, as the linked list does.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `transpiler-bcl`: a sorted collection of an enum orders by value, and a queue and a stack find a
  member by value.

## Impact

- **eqc**: `ValueOrdering`, and every caller writes the ordering's JavaScript as given.
- **Runtime**: `utils/ordering.ts` (an enum's order), the sorted factories, `Queue` and `Stack`.
- **Break**: `Max` and `Min` over an enum compile, where they failed the build with EQ1004.
