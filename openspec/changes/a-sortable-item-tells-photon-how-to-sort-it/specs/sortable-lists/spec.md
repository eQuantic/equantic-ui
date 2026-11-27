## ADDED Requirements

### Requirement: On Photon a sortable item's stops say how to sort it

On Photon every keyboard stop inside an enabled `SortableItem`, the stop of a `Pressable`, a `Link` or an `Adjustable`,
SHALL carry as its semantic `Hint` the `Instructions` of the list's `Announcements`, as the web describes the focused
control with them. A text field inside an item SHALL carry none, as the web leaves a field alone, and neither SHALL a
stop outside any item, a stop of a disabled item, or a stop of a list with no words. An item of a list nested in another
item SHALL carry its own list's instructions.

#### Scenario: The SDK's instructions on a card

- **WHEN** a list built with `UI.Sortable(…)` holds a card whose child is a pressable named `a`
- **THEN** the semantics tree's node for `a` carries the hint "Press Space to pick it up, the arrow keys to move it, and
  Space again to drop it."

#### Scenario: An app's own instructions

- **WHEN** the list's `Announcements` give the instructions "Use Space."
- **THEN** the node for `a` carries the hint "Use Space."

#### Scenario: No hint where nothing sorts

- **WHEN** a pressable stands outside every item, an item is disabled, or the list has no words
- **THEN** its node carries no hint
