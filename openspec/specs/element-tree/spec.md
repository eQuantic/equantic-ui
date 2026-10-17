# element-tree Specification

## Purpose
How a live tree keeps a component's instance by its position, when its components build, and in what
order they mount and unmount, so a component's state belongs to where it stands rather than to a
string a frame happens to spell the same way.

## Requirements

### Requirement: A position keeps its stateful instance

On Photon each node of the live tree SHALL hold a position, an element, and a `StatefulComponent`
SHALL keep the instance its position first mounted for as long as that position lives. A rebuild that
hands the position a node of the same runtime type and the same `Key` SHALL keep the element and its
instance, and the fresh instance's configuration SHALL reach the kept one through `AdoptConfig`. A
node of another type or another key SHALL replace the element, and its instance SHALL start fresh.
Among the children of a `Row`, a `Column`, a `Stack`, a `Grid` and a `Navigable`, a child with a key
SHALL be matched by its key wherever it moves, and a child without one by its index.

#### Scenario: A parent rebuilds

- **WHEN** a page holds a stateful counter that was pressed three times, and the page rebuilds with a
  fresh counter whose label changed
- **THEN** the counter shows three presses and the new label

#### Scenario: Keyed rows swap

- **WHEN** a column holds two stateful rows keyed `a` and `b`, row `a` was pressed once, and the
  column rebuilds with `b` before `a`
- **THEN** the row keyed `a` still shows one press, now in second place, and `b` shows none

#### Scenario: The type at a position changes

- **WHEN** a position held a stateful component of one type and the rebuild puts another type there,
  then the first type back
- **THEN** the first type's state starts from its constructor again

### Requirement: What leaves unmounts before what arrives mounts

A live tree SHALL call `OnMount` once for each stateful instance that enters it, after the frame
that built it, and `OnUnmount` once when its position leaves, and within one frame every
`OnUnmount` SHALL run before any `OnMount`. Taking the tree down (`PhotonHost.Unmount`) SHALL unmount
every instance still in it once, the root's included. A tree laid out once, with no host, SHALL call
neither hook.

#### Scenario: Two components swap places

- **WHEN** a frame replaces a mounted component with a component of another type
- **THEN** the leaving one's `OnUnmount` runs before the arriving one's `OnMount`

#### Scenario: A host that keeps drawing

- **WHEN** a host draws a stateful root for a hundred frames and the root calls `SetState` once
- **THEN** the root mounted once, and the host was asked for one frame

#### Scenario: A layout with no host

- **WHEN** a tree with a stateful component is laid out by `LayoutEngine.Layout` alone
- **THEN** neither `OnMount` nor `OnUnmount` runs

### Requirement: A frame builds before it lays out

A Photon frame SHALL build its components before it lays the tree out, and the layout SHALL measure
what was built and build nothing: a component SHALL build at most once in a frame however many times
its parent measures it, and intrinsic sizing SHALL read the same built subtree, so a stateful child is
measured with its own state.

#### Scenario: A container measures a child twice

- **WHEN** a frame lays out a component inside a box whose minimum width makes it measure its child
  again
- **THEN** the component built once in that frame

#### Scenario: A grid sizes a column to its content

- **WHEN** a grid whose column hugs its content holds a stateful component whose width grew with its
  state
- **THEN** the column is as wide as the component's current state draws it

### Requirement: Only what changed builds

A `SetState` SHALL mark its own position for a build and ask the host for a frame, and that frame
SHALL build the marked positions, ancestors before descendants, and nothing else. A frame the host
draws only because something moves (a glide, a transition, a presence, a loop, a frame ticker's
subscriber) SHALL build nothing. A frame after input or an invalidation the host was told of SHALL
rebuild every component.

#### Scenario: A loop runs

- **WHEN** a screen of stateful components with a running loop motion draws frames with no input
- **THEN** no component builds in those frames

#### Scenario: One component changes

- **WHEN** one of several stateful components calls `SetState`
- **THEN** the next frame builds that component and what it builds, and no other component

### Requirement: A layer builds when it is shown

An `Overlay`'s child and an `Anchored` panel SHALL be built when their layer is realized in a frame,
and a layer that is not realized in a frame (an overlay closed with motion, a panel closed or no
longer hovered) SHALL unmount what it holds.

#### Scenario: A menu closes

- **WHEN** an anchored panel holding a stateful component is open for a frame and closed in the next
- **THEN** the component mounted in the first frame and unmounted in the second
