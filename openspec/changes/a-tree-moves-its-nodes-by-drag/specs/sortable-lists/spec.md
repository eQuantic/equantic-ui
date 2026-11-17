## MODIFIED Requirements

### Requirement: A keyboard sorts an item and a reader hears each step

With the focus on a `SortableItem` or inside it, or on a composite whose active item is a `SortableItem` or inside
one (a tree's, a grid's), Space SHALL pick the item up; an arrow along the list's axis SHALL move it one place in its
list; an arrow across the axis SHALL move it to the previous or next list of its group in document order, at the same
position or the last; Space or Enter SHALL drop it, reporting the `SortDrop` to the list it is in; Escape, Tab or a
pointer's press SHALL put it back with no drop. Each place SHALL be offered to the list's `Accepts` first, a refused
place SHALL NOT be taken, and the list SHALL report each place through `OnHovered` and `null` when the item leaves it.
Each step SHALL be announced through one polite live region, from the SDK's strings in the reader's language, naming
the item, its position, the count and the list's `Label`. After a drop the focus SHALL return to the item, or stay on
the composite whose active item it was.

#### Scenario: A card moved to the next column by the keyboard

- **WHEN** the focus is on card `a`, first of column `todo` labelled `To do`, and the person presses Space,
  ArrowDown, ArrowRight and Space, with column `done` labelled `Done` holding one card
- **THEN** `done`'s `OnDropped` receives `SortDrop("a", "todo", "done", 1)`, and the live region said in turn
  that `a` was picked up at position 1 of 3 in To do, that it is at position 2 of 3 in To do, that it is at
  position 2 of 2 in Done, and that it was dropped at position 2 of 2 in Done

#### Scenario: Escape puts it back

- **WHEN** the person picks card `a` up with Space, presses ArrowDown and then Escape
- **THEN** no `OnDropped` ran, the list last reported `null`, and the live region says `a` was put back

#### Scenario: A composite's active item is picked up

- **WHEN** the focus is on a tree's host whose active item is `README.md`, a sortable item, and the person presses
  Space, ArrowUp and Space
- **THEN** the list's `OnDropped` runs for `README.md`, and the focus is still on the tree's host

## ADDED Requirements

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
