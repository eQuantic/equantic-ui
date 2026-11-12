# Proposal

Closes #1413, a Bug under #1174.

## Why

A component that reads an inherited value through `DependOn<T>()` builds one frame late when the value
sits inside an overlay's layer and changes in the frame the layer is shown. The realizer builds a
deferred slot while it lays the frame out (`NodeElement.ShowDeferred`), after the frame's build phase
ended, and `BuildOwner.ScheduleDependent` built a dependent in the same frame only during that phase.
Outside it the dependent waited like a `SetState`, for the next frame. So the frame that showed the
layer painted the dependent with the old value, and asked the host for another frame to correct it.

## What Changes

- A dependent marked while the realizer shows a layer builds in that layer's `BuildDeferred`, by depth,
  before the layer is laid out: one marked by the walk that hands the slot its new configuration joins
  the list `BuildDeferred` reads, and one marked while that list is being built joins it behind the
  position building.
- A dependent of a layer inside the layer still waits for its own layer, as a marked position does.

## Impact

- `src/eQuantic.UI.Primitives/Elements/BuildOwner.cs` and `NodeElement.cs`. No API changes.
- Photon and the web's element frame both show layers through `ShowDeferred`, so both draw the new
  value in the frame the layer opens, and neither asks for a frame after it.
