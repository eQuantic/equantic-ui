## ADDED Requirements

### Requirement: Photon carries an item as the web does

The Photon host SHALL carry a `SortableItem` by the same rules the web does: picked up by a mouse or a pen
that travels more than 5 px and by a finger held still for 250 ms, dimmed in place under a copy that follows
the pointer and leans with its speed (upright when the person asked for less motion), placed by the same
arithmetic, with the same guard, hover reports, drop and Escape; the scroll view of the list under the
pointer SHALL scroll near its edge, and the place SHALL count the items scrolled out of sight. The keyboard
SHALL carry an item as on the web, and the focus SHALL follow the dropped item. On Photon the steps SHALL NOT
be spoken yet.

#### Scenario: A card carried to the other column on Photon

- **WHEN** a board on Photon holds columns `todo` (`a1`, `a2`, `a3`) and `doing` (`b1`, `b2`) of one group, and
  a mouse presses `a1`, travels and releases it between `b1` and `b2`
- **THEN** `doing`'s `OnDropped` receives `SortDrop("a1", "todo", "doing", 1)`, `doing`'s `OnHovered` last
  received `null`, and `a1`'s press handler did not run

#### Scenario: The column scrolls under the card

- **WHEN** column `doing` scrolls, holds eight cards, and the pointer carrying `a1` rests near the bottom edge of
  its scroll view for half a second before the release
- **THEN** the column scrolled, and the drop's `Index` counts the cards scrolled out of sight above the pointer
