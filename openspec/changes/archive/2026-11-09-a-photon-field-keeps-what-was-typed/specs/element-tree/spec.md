## MODIFIED Requirements

### Requirement: Only what changed builds

A `SetState` SHALL mark its own position for a build and ask the host for a frame, and that frame
SHALL build the marked positions, ancestors before descendants, and nothing else. A frame the host
draws only because something moves (a glide, a transition, a presence, a loop, a frame ticker's
subscriber) SHALL build nothing. A frame after input SHALL build what the input reached: the
positions the handlers it ran marked. Input the host routes into a field, a code surface or a
spreadsheet SHALL be handed to the app's handler. A frame after a key typed into a field SHALL build
what the field's handler marked and nothing else, because the field keeps what was typed itself. A
frame after a code surface's or a spreadsheet's handler that called no `SetState` SHALL rebuild every
component, because the host cannot see which component shows what the surface's model holds. A frame
the host draws for work posted to its thread SHALL build what the work marked. A frame after an
invalidation the host was told of, a resize, a density change or a hot reload SHALL rebuild every
component.

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
- **THEN** no component builds in the frames after the keys, and the field shows every key typed

#### Scenario: Work posted to the thread that draws

- **WHEN** work posted from any thread calls one component's `SetState`
- **THEN** the frame it runs in builds that component and no other
