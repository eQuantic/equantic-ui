## ADDED Requirements

### Requirement: A LayoutBuilder builds its child for the room its parent gives

A `LayoutBuilder` SHALL take its room as a `Box` whose `Width` and `Height` are `Fill` does, and its builder
SHALL be handed a `BoxConstraints` whose minimums are zero and whose maximum on each axis is the size the parent
decides there, or `float.PositiveInfinity` where the box's size on that axis is left to what it holds. Its child
SHALL be what the builder answers, laid out as that box's child. The builder SHALL run again when the room
changes or the node is built again, and not when the same node is laid out again in the same room. Photon and
the web SHALL hand the builder the same constraints for the same tree in the same window.

#### Scenario: A builder in a box of 300 by 200

- **WHEN** a `LayoutBuilder` stands in a `Box` 300 wide and 200 tall
- **THEN** its builder is handed `BoxConstraints(0, 300, 0, 200)`, on Photon and on the web

#### Scenario: A builder in a column sized by what it holds

- **WHEN** a `LayoutBuilder` stands in a `Column` that sizes its height from its children, inside a window 400
  wide
- **THEN** its builder is handed a maximum width of 400 and an unbounded maximum height, on both targets

#### Scenario: A narrower room builds another child

- **WHEN** a builder answers a `Row` at a maximum width of 480 or more and a `Column` below it, and its box goes
  from 600 to 360 wide
- **THEN** the child becomes the `Column`, and the components it holds are the builder's new ones

#### Scenario: The same room builds nothing again

- **WHEN** a frame lays out a `LayoutBuilder` whose node and room are the ones its builder last ran for
- **THEN** the builder does not run, and the child it built stays

### Requirement: The server writes a LayoutBuilder empty

The server SHALL write a `LayoutBuilder` as the filling box with nothing in it, since it has no box to measure.
The browser's first lowering SHALL write the same box, so hydration adopts it. The browser SHALL then measure
the room the box is given, independently of what it holds, run the builder, and draw its child.

#### Scenario: A page holding a builder

- **WHEN** a page whose body is a `LayoutBuilder` is served and hydrated in a browser 1024 wide
- **THEN** the served markup holds the empty box, and after hydration the box holds what the builder answers
  for a maximum width of 1024

### Requirement: An author holds and applies BoxConstraints

`BoxConstraints` SHALL answer as Flutter's value does: `Tight` and `Loose`, `IsTight`, `HasBoundedWidth`,
`HasBoundedHeight`, `Biggest`, `Smallest`, `Constrain`, `Enforce`, `Loosen` and `Deflate`, with an unbounded
maximum as `float.PositiveInfinity`. The browser's twin SHALL answer what .NET answers for the same arguments, in
single precision. A `ConstrainedBox` SHALL give its child the room its parent gives, at most its constraints'
maximums, and SHALL be at least their minimums, as a `Box` with those bounds is on every target.

#### Scenario: Enforcing inside a parent's room

- **WHEN** `new BoxConstraints(minWidth: 100, maxWidth: 400).Enforce(BoxConstraints.Tight(new Size(300, 50)))`
  is computed in .NET and in the browser
- **THEN** both answer a minimum and maximum width of 300 and a minimum and maximum height of 50

#### Scenario: A ConstrainedBox in a wide slot

- **WHEN** a `ConstrainedBox(BoxConstraints.Loose(new Size(320, 480)), child)` stands in a column 600 wide whose
  children stretch
- **THEN** it is 320 wide on both targets, and its child is laid out within 320
