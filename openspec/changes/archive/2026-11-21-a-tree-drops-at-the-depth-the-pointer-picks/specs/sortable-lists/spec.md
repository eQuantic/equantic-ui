## ADDED Requirements

### Requirement: A list's items may have depths, and a place says the depth a carried item takes

A `Sortable` whose `Indent` is above 0 SHALL be a list whose items have depths: each `SortableItem.Depth` (0 at the top)
and `Indent` the distance across the axis between two depths. In such a list the items right after a carried item that
are deeper than it SHALL be carried with it: they SHALL NOT be places of their own. A place between two items (not
inside one) SHALL be reported with `SortDrop.Depth`, the depth the carried item takes, which SHALL be at least the depth
of the item after the place and at most one deeper than the item before it (`SortPlacement.Depths`). A pointer SHALL ask
for the carried item's own depth plus one per `Indent` it travelled across the axis since it picked the item up, rounded
to the nearest (`SortPlacement.Wanted`); the depth asked for, kept in the place's range, SHALL be offered to the list's
`Accepts` first, then each shallower one, then each deeper one, and the first accepted SHALL be the place. A place inside
an item, and every place of a list whose `Indent` is 0, SHALL report no depth. The web and Photon SHALL run the same
arithmetic.

#### Scenario: A pointer that travels across takes the item deeper

- **WHEN** a list with depths and an `Indent` of 16 holds `src` (0), `App.cs` (1) and `docs` (0), and a mouse carries
  `docs` from depth 0 to between `App.cs` and the end, travelling 16 px toward the end of the cross axis
- **THEN** the place reported has `Index` 2 and `Depth` 1

#### Scenario: A pointer that does not travel across keeps the item's own depth

- **WHEN** the same mouse carries `docs` to the same place without travelling across
- **THEN** the place reported has `Depth` 0

#### Scenario: A refused depth gives way to a shallower one

- **WHEN** a list with depths holds `a` (0) and `b` (0), its `Accepts` refuses every place whose `Depth` is 1, and a mouse
  carries `b` above itself to after `a`, travelling one `Indent` across
- **THEN** the place reported has `Depth` 0

#### Scenario: The carried item's subtree is no place of its own

- **WHEN** a list with depths holds `src` (0), `App.cs` (1), `Program.cs` (1) and `docs` (0), and a mouse carries `src`
  over `Program.cs`
- **THEN** no place among `App.cs` and `Program.cs` is reported, and the place reported is `src`'s own, `Index` 0

#### Scenario: The same depth on Photon

- **WHEN** the first scenario's list is carried the same way on Photon
- **THEN** the host reports the same `Index` and `Depth`

### Requirement: In a list with depths the keyboard reaches each depth and a reader hears the level

In a list with depths, at a place between two items, the arrow across the axis toward its end (ArrowRight in a
vertical list, ArrowDown in a horizontal one) SHALL ask one depth deeper and the other one shallower, passing over the
depths the list refuses as the arrows along the axis pass over refused places; once the place offers no depth that way,
the arrow SHALL move the item to the neighbouring list of its group, as in a list without depths. The depth the keyboard
last chose SHALL be kept, within each place's range, as the item moves along. Each step's words SHALL keep their holes,
filled from the depths: `{1}` the item's position among the items at its depth under the same holder, `{2}` their
count, the carried item included, and `{3}` the holder's name, the holder being the nearest earlier item one depth
shallower, or the list's name at depth 0 (`SortPlacement.Siblings`). A list without depths SHALL say what it said.

#### Scenario: ArrowRight makes the item the last child of the folder above

- **WHEN** a nesting list with depths holds `src` (0), `App.cs` (1), `docs` (0) and `README.md` (0), and the person picks
  `README.md` up with Space, presses ArrowUp until it stands before `docs`, then ArrowRight and Space
- **THEN** the drop reports `Index` 2 and `Depth` 1, and the live region said that `README.md` is at position 2 of 2 in
  `src` and that it was dropped at position 2 of 2 in `src`

#### Scenario: The same steps on Photon

- **WHEN** the same list on Photon is sorted by the same keys
- **THEN** the host reports the same drop and says the same words

#### Scenario: No deeper depth moves to the next list

- **WHEN** a list with depths of a group holds `a` (0) and `b` (0), the group's next list holds `c`, and the person picks
  `a` up with Space, where no deeper depth is offered, and presses ArrowRight
- **THEN** `a` is at a place of the next list
