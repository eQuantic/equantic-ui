# element-tree Specification

## Purpose
How a live tree keeps what belongs to a position (a component's instance, a node's state) by the
position itself, when its components build, and in what order they mount and unmount, so state belongs
to where it stands rather than to a string a frame happens to spell the same way.

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
subscriber) SHALL build nothing. A frame after input SHALL build what the input reached: the
positions the handlers it ran marked. Input the host routes into a field, a code surface or a
spreadsheet SHALL be handed to the app's handler, and a frame after such a handler that called no
`SetState` SHALL rebuild every component, because the host cannot see where the value lives. A frame
the host draws for work posted to its thread SHALL build what the work marked. A frame after an invalidation the host was told of, a
resize, a density change or a hot reload SHALL rebuild every component.

#### Scenario: A loop runs

- **WHEN** a screen of stateful components with a running loop motion draws frames with no input
- **THEN** no component builds in those frames

#### Scenario: One component changes

- **WHEN** one of several stateful components calls `SetState`
- **THEN** the next frame builds that component and what it builds, and no other component

#### Scenario: A press

- **WHEN** a screen holds several stateful cards and a press on one card's button runs a handler that
  calls that card's `SetState`
- **THEN** the next frame builds that card and no other component

#### Scenario: A key typed into a code editor

- **WHEN** a screen holds a code editor beside other stateful components and a key is typed into the
  editor
- **THEN** the next frame builds the editor's component and no component beside it

#### Scenario: A key typed into a field whose handler sets state

- **WHEN** a stateful form holds a field, directly or through a field component with state of its own,
  and the field's handler calls the form's `SetState` with each key
- **THEN** each next frame builds the form and what it builds, and no component above or beside it

#### Scenario: A key typed into a field whose handler sets no state

- **WHEN** a stateful form keeps its field's value without `SetState`, directly or through a field
  component with state of its own, and keys are typed into the field
- **THEN** each next frame rebuilds every component, and the field shows every key typed

#### Scenario: Work posted to the thread that draws

- **WHEN** work posted from any thread calls one component's `SetState`
- **THEN** the frame it runs in builds that component and no other

### Requirement: A layer builds when it is shown

An `Overlay`'s child and an `Anchored` panel SHALL be built when their layer is realized in a frame,
and a layer that is not realized in a frame (an overlay closed with motion, a panel closed or no
longer hovered) SHALL unmount what it holds.

#### Scenario: A menu closes

- **WHEN** an anchored panel holding a stateful component is open for a frame and closed in the next
- **THEN** the component mounted in the first frame and unmounted in the second

### Requirement: A position keeps its node's state

On Photon the state a node keeps across frames (a scroll view's offset, a drag in progress or gliding
back, what an `InView` last reported, a `Presence`'s entrance, a transition's tracks) SHALL belong to
the node's position and SHALL leave the tree with it. A node that takes a position another node held,
because the type or the key there changed or because the component that built it was replaced, SHALL
start with its own state: a scroll view at its own `Offset`, a draggable at its rest, an `InView`
reporting its first sight, a transition mounting at its value and a `Presence` entering. A node that
keeps its position across a rebuild SHALL keep its state. An `InView` SHALL report only in a tree that
is drawn again, which a host's is and a tree laid out once is not.

#### Scenario: A page replaces another

- **WHEN** a host shows a page whose scroll view was scrolled to 120, and a `SetState` replaces the
  page with a page of another type whose scroll view stands at the same place in the tree
- **THEN** the new page's scroll view is at its own offset, 0

#### Scenario: A key changes

- **WHEN** a box holds a scroll view keyed `a` that was scrolled, and a rebuild hands the box a scroll
  view keyed `b`
- **THEN** the scroll view keyed `b` is at its own offset

#### Scenario: A node keeps its position

- **WHEN** a scroll view was scrolled to 120, and its page rebuilds with a scroll view of the same
  type and key at the same place
- **THEN** the scroll view is still at 120

#### Scenario: A drag does not pass to the next node

- **WHEN** a draggable was dragged 60 and released, and before its glide back ends a rebuild puts a
  draggable with another key at its place
- **THEN** the new draggable is drawn at its rest offset

#### Scenario: An InView at another's place

- **WHEN** an `InView` reported that it is visible, and a `SetState` replaces its page with a page of
  another type whose `InView` stands at the same place and is visible too
- **THEN** the new `InView` reports that it is visible

#### Scenario: A transition at another's place

- **WHEN** a box whose width transitions was 100 wide, and a `SetState` replaces its page with a page
  of another type whose box at the same place is 300 wide and transitions too
- **THEN** the new box is 300 wide in the first frame it is drawn

#### Scenario: A tree laid out once

- **WHEN** a tree holding a visible `InView` is realized once with no owner
- **THEN** the `InView` reports nothing

### Requirement: A presence that leaves fades out from the frame it left in

When a `Presence`'s position leaves the tree, its last paint SHALL be replayed as an exit, paint only,
from the frame it left in until the exit ends, and nothing in it SHALL take input. A `Presence` that
takes the place of one that left SHALL enter while the old one's exit fades. A layer that closes in a
frame SHALL show its presences' exit in that same frame. Presences that leave in one frame SHALL fade
in the order they were painted, whatever order their positions leave the tree in.

#### Scenario: A page with a presence replaces another

- **WHEN** a page holding a `Presence` is replaced by a page of another type holding a `Presence` at
  the same place
- **THEN** the frame that replaces it draws the old presence's last paint over the new one's first,
  and the old one fades out while the new one enters

#### Scenario: An overlay closes with motion

- **WHEN** an overlay with motion that holds a `Presence` closes
- **THEN** the frame that closes it draws the presence's last paint at full opacity, and the frames
  after it fade it out

#### Scenario: Two presences leave together

- **WHEN** a stack holds two overlapping presences, the second painted over the first, and a rebuild
  takes both out, or two layers that each hold a presence close in the same frame
- **THEN** the frame they leave in draws the first one's exit under the second one's

### Requirement: The host finds a control by its position

On Photon the press, the keyboard focus, the field being edited and the hover chain SHALL follow the
position of the node they belong to: they SHALL stay on a control for as long as its position holds
it, across rebuilds, and SHALL NOT pass to a node that takes its place, because a page replaced
another or a key changed. A press whose control left before the release SHALL run nothing. Focus
within a subtree SHALL be the focused control's position lying under the subtree's.

#### Scenario: A page replaces another under the focus

- **WHEN** a button has the keyboard focus and a `SetState` replaces its page with a page of another
  type whose button stands at the same place
- **THEN** no button has the focus, no ring is drawn and Enter presses nothing

#### Scenario: A control replaced during a press

- **WHEN** a pointer is down on a button, and a rebuild puts a button with another key at its place
  before the pointer lifts
- **THEN** lifting the pointer runs neither button's handler

#### Scenario: The same node at a new position

- **WHEN** an app hands one button node to two page types, a pointer is down on it, and one page
  replaces the other before the pointer lifts
- **THEN** lifting the pointer runs nothing, because the button's new position was never pressed

#### Scenario: A hover at another's place

- **WHEN** the pointer rests on a box with a hover style, and a rebuild puts a box with another key at
  its place
- **THEN** the new box is drawn without its hover style until the pointer moves over it

#### Scenario: A keyed row keeps its focus

- **WHEN** a row of a keyed list has the keyboard focus and a rebuild inserts a row before it
- **THEN** the same row keeps the focus

### Requirement: A node's accessibility id is its position's

Each node a platform reader can land on SHALL be named by an integer id that its position carries
from the moment it mounts for as long as it lives, and a linked run inside a paragraph by an id of
its own kept by the paragraph's position. Activating or adjusting through a bridge SHALL name the
node by that id. A node at another's place SHALL have a new id.

#### Scenario: A rebuild keeps the ids

- **WHEN** a screen is read by a platform bridge, rebuilt, and read again
- **THEN** every node the rebuild kept has the id it had

#### Scenario: A reader activates a control

- **WHEN** a bridge activates the id of a button
- **THEN** the button's handler runs, as a press would run it

### Requirement: A shipped component carries every setting a parent hands it

Every `StatefulComponent` the SDK ships SHALL carry, through `AdoptConfig`, every public `set` or `init`
property and every constructor argument of the fresh instance a parent's rebuild hands its position, so the
kept instance answers what the fresh one does. An input read once on purpose SHALL be named, with its
reason, in `tests/eQuantic.UI.Web.Tests/Coverage/adopt-config.baseline.txt`, and that list SHALL only shrink.
A component that keeps the fresh instance whole as its configuration SHALL count as carrying every input.

#### Scenario: A form disables its fields while it submits

- **WHEN** a page holds a `DatePicker`, and rebuilds handing it `Disabled = true`
- **THEN** the picker its position keeps is disabled

#### Scenario: A component drops a setting

- **WHEN** a shipped component's `AdoptConfig` does not carry one of its `init` properties
- **THEN** `AdoptConfigCoverageTests` fails, naming the component and the property

#### Scenario: A setting read once on purpose

- **WHEN** a page rebuilds handing a `CodeEditor` another `InitialCode`
- **THEN** the editor keeps the document someone is typing in, and the baseline names `CodeEditor.InitialCode`
  with its reason
