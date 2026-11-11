## Why

Closes #1396, a Bug under #1174, on the 1.0 critical path: it blocks E3a-3 (#1301) of the element engine (#1127).

#1303 (in #1350) made a downcast keep its type for the checker, and dropped it for a type the build keeps on the server, which has no twin to name. It asked that of `IsKeptOut()`, which counts a class whose BASE is `[ServerOnly]` as kept out. That is the rule for an app's class, which cannot cross over a base that has no module. A type of the runtime has a twin of its own: `Pressable`, over `SingleChildNode`, and `Row` and `Column`, over `FlexNode`, both bases marked `[ServerOnly]`. So `((Pressable)node).Child` was written `node.child`, read off `VisualNode`, which `tsc` refused, 24 times in the element engine's `ChildSlots`.

## What Changes

- A downcast to a type of the runtime drops its type only where that type is itself `[ServerOnly]`. A cast to an app's type keeps the rule it had.

For a developer using the SDK: nothing they write changes, and nothing the browser runs changes, since the assertion is type-only.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `transpiler-classes`: a new requirement, a downcast to a type of the runtime keeps its type whichever class declares the member.

## Impact

- eqc only (`CastExpressionStrategy`). No public surface and no developer surface moves.
