## ADDED Requirements

### Requirement: An item is put in order inside its list by a pointer

A `SortableItem` inside a `Sortable` SHALL be picked up by a mouse that presses it and travels more than 5 px,
and by a finger held still on it for 250 ms. While it is carried, a copy of it SHALL follow the pointer and the
item SHALL be dimmed in place. On release over its own container, the container's `OnDropped` SHALL receive a
`SortDrop` whose `Item` is the item's id, whose `From` and `To` are the container's id, and whose `Index` is
the position it lands at in the list without it: before the first other item whose middle along the
container's axis the pointer has not passed. A press that does not travel SHALL press the item and start no
drag, and the click that ends a drag SHALL NOT reach the item's press handler.

#### Scenario: A card moved down its column

- **WHEN** a column holds cards `a`, `b` and `c`, and the person drags `a` and releases it below the middle of
  `b` and above the middle of `c`
- **THEN** `OnDropped` receives `SortDrop("a", "todo", "todo", 1)`, and `a`'s press handler did not run

### Requirement: An item moves across the lists of a group

Sortables with the same `Group` SHALL take each other's items, an empty one included; the drop SHALL be
reported by the container it lands in, with `From` the container it left. A container of another group SHALL
offer no place to the item.

#### Scenario: A card moved to an empty column

- **WHEN** the person drags card `a` from column `todo` and releases it over the empty column `done` of the same
  group
- **THEN** `done`'s `OnDropped` receives `SortDrop("a", "todo", "done", 0)`

### Requirement: The place is reported while the item is carried, and the app may refuse it

While an item is carried over a container, the container's `OnHovered` SHALL receive the `SortDrop` it would
report on release, each time the place changes, and `null` when the item leaves it or the drag ends. Before a
place is offered, the container's `Accepts` SHALL be asked; a place it refuses SHALL NOT be offered, and a
release there SHALL drop nothing.

#### Scenario: A refused column

- **WHEN** column `done` accepts no card whose key starts with `locked`, and the person drags `locked-1` over it
  and releases
- **THEN** `done`'s `OnHovered` received no place, and no `OnDropped` ran

### Requirement: A nesting container takes an item into another

In a `Sortable` that `Nests`, a pointer over the middle half of an item's box along the axis SHALL place the
carried item INTO that item: the `SortDrop`'s `Into` SHALL be that item's id and its `Index` that item's
position. An item SHALL NOT be placed into itself.

#### Scenario: A file dropped into a folder

- **WHEN** a nesting container holds `docs` and `README.md`, and the person releases `README.md` over the middle
  of `docs`
- **THEN** `OnDropped` receives a `SortDrop` whose `Item` is `README.md` and whose `Into` is `docs`

### Requirement: A carried item is put back by Escape

Escape while an item is carried SHALL end the drag with no drop, report `null` to the container that last
reported a place, and leave the item where it was.

#### Scenario: Escape halfway

- **WHEN** the person drags card `a` over column `done` and presses Escape before releasing
- **THEN** no `OnDropped` ran, `done`'s `OnHovered` last received `null`, and `a` is no longer dimmed
