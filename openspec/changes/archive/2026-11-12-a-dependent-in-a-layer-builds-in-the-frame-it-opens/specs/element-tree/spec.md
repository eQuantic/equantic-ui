# Spec Delta

## MODIFIED Requirements

### Requirement: A component rebuilds when a value it depends on changes, and nothing else does

A component that reads an inherited value through `DependOn<T>()` SHALL build again in the same frame
when a parent's build hands that value's position a new node whose `UpdateShouldNotify` answers true,
and a component that read nothing of it, or read it once through `Find<T>()`, SHALL NOT. A lookup SHALL
answer the nearest value of its type above the component, and a component whose position left the tree
SHALL NOT build when the value changes. A value inside a layer that changes in the frame the realizer
shows the layer SHALL build its dependents in that frame, before the layer is laid out, and SHALL NOT
ask for a frame after it.

#### Scenario: A value changes under a subtree built once

- **WHEN** a stateful holder places a new value over the same child, which holds a reader, a component
  that reads nothing and one that read the value once
- **THEN** only the reader builds again, in that frame, with the new value

#### Scenario: The value says nothing changed

- **WHEN** the new node's `UpdateShouldNotify` answers false
- **THEN** no dependent builds

#### Scenario: Two values of one type

- **WHEN** a reader stands under two values of its type, with a value of another type between them
- **THEN** it reads the nearest of its type

#### Scenario: A value inside a layer changes in the frame the layer is shown

- **WHEN** a stateful holder builds an overlay whose layer places a new value over the same child, which
  holds a reader and a component that reads nothing, and the realizer shows the layer
- **THEN** the reader builds with the new value before the layer is laid out, the other does not, and no
  position waits for the next frame

#### Scenario: A value changes while a layer's marked positions build

- **WHEN** a stateful holder inside a layer, out of reach of the walk that shows the layer, places a new
  value over the same child, which holds a reader, and builds when the layer is shown
- **THEN** the reader builds behind it in that frame, with the new value, and no position waits for the
  next frame
