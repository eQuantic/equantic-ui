## ADDED Requirements

### Requirement: The keyboard asks for the menu of a composite's active item

When the focus is on a composite that keeps it on its one stop and names its active item (a `Navigable`), the menu
key or Shift+F10 SHALL reach the innermost enabled `ContextTarget` around that active item, on the web as on
Photon; with no target around the active item, the innermost one around the composite SHALL answer, as before.

#### Scenario: A tree's active row

- **WHEN** a tree whose rows are each wrapped in a `ContextTarget` has the focus, its active item is its second row,
  and the person presses Shift+F10
- **THEN** the second row's target receives a request whose `At` is null, and no other target does
