## ADDED Requirements

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
