## MODIFIED Requirements

### Requirement: Only what changed builds

A `SetState` SHALL mark its own position for a build and ask the host for a frame, and that frame
SHALL build the marked positions, ancestors before descendants, and nothing else. A frame the host
draws only because something moves (a glide, a transition, a presence, a loop, a frame ticker's
subscriber) SHALL build nothing. A frame after input SHALL build what the input reached: the
positions the handlers it ran marked, and the nearest stateful component that holds a field, a code
surface or a spreadsheet the host routed the input into. A frame the host draws for work posted to
its thread SHALL build what the work marked. A frame after an invalidation the host was told of, a
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

#### Scenario: A key typed into a field whose handler sets no state

- **WHEN** a stateful component holds a field whose handler stores the value without `SetState`, and
  keys are typed into the field
- **THEN** each next frame builds that component, the field shows every key typed, and no other
  component builds

#### Scenario: Work posted to the thread that draws

- **WHEN** work posted from any thread calls one component's `SetState`
- **THEN** the frame it runs in builds that component and no other

## ADDED Requirements

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
