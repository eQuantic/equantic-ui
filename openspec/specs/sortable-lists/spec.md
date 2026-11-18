# sortable-lists Specification

## Purpose
How a `Sortable` and its `SortableItem`s are put in order by a pointer, inside a list and across the lists
of a group: what the list reports on a drop and while an item is carried, the app's guard, nesting, and
Escape.

## Requirements

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

### Requirement: A keyboard sorts an item and a reader hears each step

With the focus on a `SortableItem` or inside it, or on a composite whose active item is a `SortableItem` or inside
one (a tree's, a grid's), Space SHALL pick the item up; an arrow along the list's axis SHALL move it one place in its
list; an arrow across the axis SHALL move it to the previous or next list of its group in document order, at the same
position or the last; Space or Enter SHALL drop it, reporting the `SortDrop` to the list it is in; Escape, Tab or a
pointer's press SHALL put it back with no drop. Each place SHALL be offered to the list's `Accepts` first and a
refused place SHALL NOT be taken: an arrow along the axis SHALL pass over the places the list refuses to the next one
it accepts, as a pointer passes over them, and keep the place it holds when none is left that way. The list SHALL
report each place through `OnHovered` and `null` when the item leaves it.
Each step SHALL be announced through one polite live region, in the words of the list the item was picked up from
(its `Announcements`), naming the item, its position, the count and the list's `Label`. After a drop the focus SHALL
return to the item, or stay on the composite whose active item it was.

#### Scenario: A card moved to the next column by the keyboard

- **WHEN** the focus is on card `a`, first of column `todo` labelled `To do`, and the person presses Space,
  ArrowDown, ArrowRight and Space, with column `done` labelled `Done` holding one card
- **THEN** `done`'s `OnDropped` receives `SortDrop("a", "todo", "done", 1)`, and the live region said in turn
  that `a` was picked up at position 1 of 3 in To do, that it is at position 2 of 3 in To do, that it is at
  position 2 of 2 in Done, and that it was dropped at position 2 of 2 in Done

#### Scenario: Escape puts it back

- **WHEN** the person picks card `a` up with Space, presses ArrowDown and then Escape
- **THEN** no `OnDropped` ran, the list last reported `null`, and the live region says `a` was put back

#### Scenario: The keyboard passes over a refused place

- **WHEN** a list holds `a`, `b` and `c`, its `Accepts` refuses position 2, and the person picks `c` up with Space
  and presses ArrowUp
- **THEN** `c` passes over position 2 to position 1, and the live region says that `c` is at position 1 of 3

#### Scenario: A composite's active item is picked up

- **WHEN** the focus is on a tree's host whose active item is `README.md`, a sortable item, and the person presses
  Space, ArrowUp and Space
- **THEN** the list's `OnDropped` runs for `README.md`, and the focus is still on the tree's host

### Requirement: Photon carries an item as the web does

The Photon host SHALL carry a `SortableItem` by the same rules the web does: picked up by a mouse or a pen
that travels more than 5 px and by a finger held still for 250 ms, dimmed in place under a copy that follows
the pointer and leans with its speed (upright when the person asked for less motion), placed by the same
arithmetic, with the same guard, hover reports, drop and Escape; the scroll view of the list under the
pointer SHALL scroll near its edge, and the place SHALL count the items scrolled out of sight. The keyboard
SHALL carry an item as on the web, and the focus SHALL follow the dropped item. Each step of a keyboard's carry
SHALL be said through the polite live region of the list the item was picked up from, in that list's words, which
the macOS and iOS shells post as an announcement and the Android shell reads from the list's node, marked live while
it holds words.

#### Scenario: A card carried to the other column on Photon

- **WHEN** a board on Photon holds columns `todo` (`a1`, `a2`, `a3`) and `doing` (`b1`, `b2`) of one group, and
  a mouse presses `a1`, travels and releases it between `b1` and `b2`
- **THEN** `doing`'s `OnDropped` receives `SortDrop("a1", "todo", "doing", 1)`, `doing`'s `OnHovered` last
  received `null`, and `a1`'s press handler did not run

#### Scenario: The column scrolls under the card

- **WHEN** column `doing` scrolls, holds eight cards, and the pointer carrying `a1` rests near the bottom edge of
  its scroll view for half a second before the release
- **THEN** the column scrolled, and the drop's `Index` counts the cards scrolled out of sight above the pointer

#### Scenario: Photon says a keyboard's steps

- **WHEN** a list on Photon labelled `To do` holds `a`, `b` and `c`, and the person picks `a` up with Space, presses
  ArrowDown and Space, a second apart
- **THEN** the host's announcements say in turn that `a` was picked up at position 1 of 3 in To do, that it is at
  position 2 of 3 in To do, and that it was dropped at position 2 of 3 in To do, and the list's semantic node is a
  polite live region holding the last of them

### Requirement: In a nesting list the keyboard steps through the places inside items

In a `Sortable` that `Nests`, the arrows along the axis SHALL step the carried item through the places before each
other item, inside it, and after the last, in that order (2n + 1 places for n other items), kept inside the list, by
`SortPlacement.KeyStep` on both targets. A place inside an item SHALL be reported with `Into` naming it and announced
with the item's name; a drop there SHALL be announced the same way.

#### Scenario: Into the item above

- **WHEN** a nesting list holds `docs` and `README.md`, and the person picks `README.md` up with Space and presses
  ArrowUp and Space
- **THEN** `OnDropped` receives a `SortDrop` whose `Item` is `README.md` and whose `Into` is `docs`, and the live region
  said that `README.md` is inside `docs` and then that it was dropped inside `docs`

### Requirement: A list says its steps in the words it is given

`Sortable` SHALL carry what a reader hears while its items are sorted by the keyboard as `SortAnnouncements`: the
instructions, and the texts for picked up, moved, moved inside, refused, dropped, dropped inside and put back, each
with numbered holes filled by `SortWords.Fill` on both targets (`{0}` the item's name; for a place `{1}` its
position from one, `{2}` the count and `{3}` the list's name; for a place inside `{1}` the item it lands inside).
`UI.Sortable` SHALL fill them with the SDK's words in the reader's culture when the app gives none, and SHALL use
the app's when it does. Both realizers SHALL read the words from the node, never from a catalog of their own, and a
`Sortable` with none SHALL say nothing. On the web the focused item SHALL be described by its list's instructions.

#### Scenario: An app's own words on the web

- **WHEN** a list built with `UI.Sortable(…, announcements: new SortAnnouncements("Use Space.", "Took {0}.",
  "{0} now {1}/{2}.", "{0} in {1}.", "Not there.", "Left {0} at {1}.", "Left {0} in {1}.", "Back."))` holds `a`, `b`
  and `c`, and the person focuses `a`, presses Space, ArrowDown and Space
- **THEN** `a` is described by "Use Space.", and the live region said in turn "Took a.", "a now 2/3." and
  "Left a at 2."

#### Scenario: An app's own words on Photon

- **WHEN** the same list on Photon is sorted by the same keys, a second apart
- **THEN** the host's announcements say "Took a.", "a now 2/3." and "Left a at 2."

#### Scenario: The SDK's words by default

- **WHEN** `UI.Sortable(…)` is given no words, under the `pt-BR` culture
- **THEN** its `Announcements` are the SDK's `pt-BR` texts, and the instructions read
  "Pressione Espaço para pegá-lo, as setas para movê-lo e Espaço de novo para soltá-lo."

### Requirement: An item may carry the name the keyboard's steps say

A `SortableItem` with a `Name` SHALL be named by it in every step the keyboard's sort says, on the web and on Photon,
before what its subtree says; an item with an empty `Name` SHALL be named by what its subtree says, as before. The web
lowering SHALL write the name on the item's element (`data-eq-sort-name`) when it has one.

#### Scenario: A heading named by its column, not its press

- **WHEN** a sortable item holds a press labelled "Sort by Amount", its `Name` is "Amount", and the keyboard picks it
  up at the last of 5 places in a list labelled "Columns"
- **THEN** a reader hears "Picked up Amount, position 5 of 5 in Columns."

### Requirement: The look of a carried item is the theme's and the list's

The look of an item carried from a `Sortable` SHALL be the theme's `SortableTheme` with the list's own `Style` laid
over it, member by member, and the defaults of `SortPlacement` where neither sets a member: the item in its place
SHALL be drawn at `LiftedOpacity` (0.4), the copy that follows a pointer at `CopyOpacity` (0.92) under the theme's
shadow for elevation level `CopyElevation` (4), and the copy SHALL lean with the pointer's speed no further than
`MaxLean` degrees (12) either way, staying upright at 0. The look SHALL be the one of the list the item was picked up
from, on the web and on Photon.

#### Scenario: A theme's look on the web

- **WHEN** the theme's `SortableTheme` sets `LiftedOpacity` to 0.25 and `CopyElevation` to 5, and a mouse carries card
  `a` of a list with no style of its own
- **THEN** card `a` is drawn at opacity 0.25, and the copy carries the theme's level 5 shadow at opacity 0.92

#### Scenario: A list's own look over the theme's

- **WHEN** the theme sets `MaxLean` to 6 and the list's `Style` sets `MaxLean` to 0, and a mouse carries a card fast
  across
- **THEN** the copy stays upright

#### Scenario: The defaults

- **WHEN** neither the theme nor the list sets a `SortableTheme`
- **THEN** the carried item is drawn at 0.4, and the copy at 0.92 under the theme's level 4 shadow, leaning up to 12°

#### Scenario: The same look on Photon

- **WHEN** the same theme and list are drawn on Photon and a mouse carries card `a`
- **THEN** the host draws card `a` at the theme's `LiftedOpacity` and the copy at the list's `CopyOpacity`, under the
  theme's shadow for `CopyElevation`, leaning no further than `MaxLean`
