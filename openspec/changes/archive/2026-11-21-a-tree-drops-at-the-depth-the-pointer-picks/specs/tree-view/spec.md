## ADDED Requirements

### Requirement: A node lands at the depth the pointer picks

A tree with `OnMoved` SHALL be a sortable with depths: each node row SHALL carry its depth (its level less one) and the
sortable the tree's indentation per level. A node dropped between two rows at a depth SHALL land under the ancestor, at
one level shallower, of the row before the place (at the top level for depth 0), after that parent's last visible child
before the place, or first when it has none there. Dropped at a depth deeper than the row after the place, the node SHALL
be the last child of the folder whose subtree ends there; at that row's depth, it SHALL land among that row's siblings as
before. The rows of the carried node's own subtree SHALL NOT be places. The keyboard's ArrowRight and ArrowLeft SHALL
reach each depth as the sortable's do. A tree whose indentation is 0 SHALL read places as before.

#### Scenario: A file dropped as the last child of an open folder

- **WHEN** an open `src` holds `App.cs`, `Program.cs` and `Ui.cs`, `docs` follows it, and the person carries
  `README.md` to the gap between `Ui.cs` and `docs`, travelling one indentation toward the end of the rows
- **THEN** `OnMoved` receives a `TreeMove` whose `Node` is `README.md`, whose `Parent` is `src` and whose `After` is
  `Ui.cs`

#### Scenario: The same gap without travelling across

- **WHEN** the person carries `README.md` to the same gap without travelling across
- **THEN** `OnMoved` receives a `TreeMove` whose `Parent` is null and whose `After` is `src`

#### Scenario: A child dropped at the top level after its folder

- **WHEN** an open `src` holds `App.cs`, `Program.cs` and `Ui.cs`, and the person carries `Ui.cs` to the gap after
  itself, travelling one indentation toward the start of the rows
- **THEN** `OnMoved` receives a `TreeMove` whose `Node` is `Ui.cs`, whose `Parent` is null and whose `After` is `src`

#### Scenario: The keyboard puts a node at the end of a folder

- **WHEN** an open `src` holds `App.cs`, `docs` and then `README.md` follow it, `README.md` is active, and the person
  presses Space, ArrowUp twice to stand before `docs`, ArrowRight and Enter
- **THEN** `OnMoved` receives a `TreeMove` whose `Node` is `README.md`, whose `Parent` is `src` and whose `After` is
  `App.cs`, and the live region said that `README.md` is at position 2 of 2 in `src`

#### Scenario: The same move on Photon

- **WHEN** the first scenario is carried the same way on Photon
- **THEN** `OnMoved` receives the same `TreeMove`

## MODIFIED Requirements

### Requirement: The tree shows where a node would land

While a node is carried, the row it would land before SHALL draw a line along its top edge, the last row SHALL draw it
along its bottom edge when the node would land after it, each from the indentation of the depth the node would take,
and the row it would land inside SHALL draw its ring in `TreeDropColor` over the theme's selection tint. No row SHALL
change its size while the indicator moves. `TreeDropColor` SHALL default to the theme's focus ring.

#### Scenario: The line before a row

- **WHEN** the person carries `Ui.cs` over the top quarter of `Program.cs`
- **THEN** `Program.cs`'s row draws the line along its top edge from its own indentation, and every row keeps its height

#### Scenario: The line follows the depth

- **WHEN** the person carries `README.md` to the gap between the last child of an open `src` and `docs`, travelling one
  indentation toward the end of the rows
- **THEN** `docs`'s row draws the line along its top edge from the indentation of `src`'s children, not from its own
