## MODIFIED Requirements

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

## ADDED Requirements

### Requirement: A list says its steps in the words it is given

`Sortable` SHALL carry what a reader hears while its items are sorted by the keyboard as `SortAnnouncements`: the
instructions, and the texts for picked up, moved, moved inside, refused, dropped, dropped inside and put back, each
with numbered holes filled by `SortAnnouncements.Fill` on both targets (`{0}` the item's name; for a place `{1}` its
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
