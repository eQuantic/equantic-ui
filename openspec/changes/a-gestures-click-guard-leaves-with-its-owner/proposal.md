# Proposal

Closes #1889, a Bug under #1587.

## Why

Two gestures on the web end the same way. A finger's hold that opens a menu, and an armed drag (sortable, draggable, a sheet dismissed by dragging), each arm a capturing click guard so the click their release ends in reaches nothing. Each guard was removed by a bare timer that nothing cancelled: 600 ms for the hold, 50 ms for the drag. The gesture arena (#1884) arms one for a press that lost, and its 50 ms timer starts at the arena's end, after the arena left the map its reset walks. #1876 found the same shape in the table viewport's pan.

In the runtime suite, a spec's page torn down before such a timer fired had it call a host that had lost its methods, and vitest failed runs in which every file had passed. In an app, the listener outlived what armed it until the timer fired.

## What Changes

- **One owned click guard** (`dom/click-guard.ts`). An owner arms a guard on the host it holds. It can be told which clicks it stops, and its time can start later than its arming. The guard goes, timer and listener, in three cases:
  - with the click it stops;
  - at once when its owner lets go;
  - otherwise after its time.
- **The context controller owns a hold's guard**, and its reset lets go of it.
- **Each gesture controller's delegate owns its drags' guards.** `followPointer` is handed the owner, and the delegate's reset lets go of them.
- **The gesture arena's controller owns a lost press's guard.** It stops only clicks inside the press, its time starts at the arena's end, and the controller's reset lets go of every guard it holds, an ended arena's included.
- **The table viewport's pan guard (#1888) is the same mechanism**, moved onto the same helper.

A developer writes nothing different. No public surface and no developer surface moves.

## Impact

- `eQuantic.UI.Runtime`:
  - `dom/click-guard.ts` (new);
  - `dom/context-request.ts`;
  - `dom/pointer-gesture.ts` and its three controllers (`draggable.ts`, `sortable.ts`, `drag-dismiss.ts`);
  - `dom/gesture-arena.ts`;
  - `shared/table-viewports.ts`;
  - their specs.
