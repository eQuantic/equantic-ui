# Design

## How Flutter solves it

An `InheritedWidget` is a widget with a child whose element keeps the elements that called
`dependOnInheritedWidgetOfExactType` on it. When its widget is replaced and `updateShouldNotify`
answers true, it marks them dirty, and the owner's build scope builds them in the same frame, by
depth. `didChangeDependencies` runs before such a build. A `BuildContext` is the element itself.
`docs/FLUTTER-PARITY.md` has the element tree's rows, which this follows.

## What eQuantic does the same way

- `InheritedComponent` is a node with one child, and its position is an `InheritedElement`, a third
  kind beside the component's and the node's. An element knows the nearest inherited position above
  it from its parent when it mounts, since a position never moves, so a lookup walks only the
  inherited positions.
- The engine is written once and transpiled into the runtime, and a type argument does not reach the
  browser. So the engine looks a value up by a predicate (`Func<InheritedComponent, bool>`), and the
  `is T` lives in `ComponentContext.DependOn<T>`, outside the engine. `ElementEngineTranspilesTests`
  holds the engine to what eqc translates.
- A dependent marked while the build phase runs is built in the same frame, in the dirty list behind
  the position building and in depth order, where a `SetState` from inside a build waits for the next
  frame. A value a parent's build changed is one the frame shows.

## Where it differs

- `BuildContext` is not the element: a component position creates ONE `ComponentContext` when it is
  first asked for it, and that context is a view of the position. The frame's values (the theme, the
  density, the type scale, the measurer) stay the owner's, as the hosts hand them each frame, and a
  position that reads one is registered with the owner, which marks the readers when a frame hands
  others. That is Flutter's `MediaQuery` and root `Theme`, kept on the owner rather than in the tree,
  so no position and no id moves.
- `InFlow` stays a node. A position already knows whether one is above it (`Element.UnderInFlow`), so
  its context answers from there, and the engine no longer arms the ambient `InFlow.Current` around a
  build. The web and the server, which build with no positions, keep the ambient one until #1395.
- The route and the services stay where they are. The server's owner per request (#1094) moves them,
  and #1395 makes the route inherited.
- `Navigator` holds the tree's navigation handler, which the host hands its owner
  (`BuildOwner.NavigationRequested`), so two windows in one process each navigate their own.

## What it costs

A context per component position, made once, and a list of dependents per inherited position. A
frame whose values did not change compares three of them and marks nothing.
