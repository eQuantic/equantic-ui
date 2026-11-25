## ADDED Requirements

### Requirement: A navigable grid is read as a grid whose header row heads its columns

Photon SHALL announce a `Navigable` as a `Tree` when its role is a tree and as a `Grid` otherwise, named by its
label, as the web writes `role="tree"` or `role="grid"`, and a reader SHALL walk into it. When the grid says its
first row heads the columns, each direct child of that row SHALL be a `ColumnHeader` named by the words it holds,
and the grid's other rows SHALL be read as they are.

#### Scenario: A calendar's month

- **WHEN** a `Calendar` showing July 2026 is laid out on Photon
- **THEN** it is a `Grid` named "July 2026", followed by seven `ColumnHeader`s in the culture's day names, and then
  the month's days as `GridCell`s, with no day name read as plain text

#### Scenario: The web and Photon agree

- **WHEN** a `Calendar`, and a `DatePicker` with its calendar open, are lowered to the DOM and laid out on Photon
- **THEN** the grid, its column headers and its cells are the same roles and names on both targets
