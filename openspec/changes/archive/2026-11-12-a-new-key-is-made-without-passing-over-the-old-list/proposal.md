# Proposal

Closes #1505, a Bug under #1174.

## Why

`NodeElement.UpdateKeyed` looks each keyed member up in a table of the old members' keys, and fell
back to `NextUnclaimed`, a pass over every old member, whenever the table had no member for it. That
included a key the table does not hold at all, where no old member can match: `CanUpdate` compares
the keys, so the pass always came back empty. A list whose keys all change (a table window that moves
a page) paid one pass over the old list per new member. At 10,000 members, replacing every key cost
2,457 ms on .NET, where building the list cost 2.4 ms. On the transpiled engine it cost 2,726 ms
against 1.3 ms. #1501, the first change that builds the browser's tree through the engine, measured
374 ms for a table window rebuild against a 150 ms ceiling.

The issue also read the fallback as handing a new key another key's member, and its state. It does
not: the same key comparison refuses it. A new key inserted at the front, and a list whose keys are
all replaced, are pinned here on both sides, and they passed before the fix as they pass after.

## What Changes

- A key the table does not hold is made at once. The fallback runs only for a key the table holds,
  when its member was taken by a sibling with the same key or is of another type.

## Impact

- `src/eQuantic.UI.Primitives/Elements/NodeElement.cs` and its transpiled twin, on Photon now and in
  the browser once the browser builds through the engine.
- No API changes, and no member keeps or loses state it did not before.
