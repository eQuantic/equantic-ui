## ADDED Requirements

### Requirement: An item may carry the name the keyboard's steps say

A `SortableItem` with a `Name` SHALL be named by it in every step the keyboard's sort says, on the web and on Photon,
before what its subtree says; an item with an empty `Name` SHALL be named by what its subtree says, as before. The web
lowering SHALL write the name on the item's element (`data-eq-sort-name`) when it has one.

#### Scenario: A heading named by its column, not its press

- **WHEN** a sortable item holds a press labelled "Sort by Amount", its `Name` is "Amount", and the keyboard picks it
  up at the last of 5 places in a list labelled "Columns"
- **THEN** a reader hears "Picked up Amount, position 5 of 5 in Columns."
