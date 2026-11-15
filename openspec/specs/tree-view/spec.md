# tree-view Specification

## Purpose
How a `TreeView` shows a hierarchy an app has or loads: one row per visible node, children loaded the
first time their node opens, the WAI-ARIA tree pattern's keyboard and type-ahead, and a path revealed.
The tree's reading on every target is `screen-reader-parity`'s.

## Requirements

### Requirement: A TreeView shows its visible nodes as a tree

`TreeView` SHALL draw one row per VISIBLE node: every root, and the children of every open node, in
depth-first order. Each row SHALL be a tree item stating its level (1 for a root), its position among
its siblings and their count (`TreePlace`), its label, its icon when the node has one, a chevron when the
node may have children, and `Expanded` for such a node. The node whose key is `SelectedKey` SHALL be the
one item stated as selected; the others SHALL be stated as not selected. A node with `IsLeaf` SHALL have
no chevron and no `Expanded`.

#### Scenario: Roots and an open folder

- **WHEN** a `TreeView` of `src` (children `App.cs`, `Ui.cs`) and `README.md` (a leaf), with `src` named
  in `ExpandedKeys` and `SelectedKey` `Ui.cs`, is read through `ComponentTester`
- **THEN** it announces a tree whose items are `src` (level 1, 1 of 2, expanded), `App.cs` (level 2,
  1 of 2), `Ui.cs` (level 2, 2 of 2, selected) and `README.md` (level 1, 2 of 2, no expanded state), in
  that order

### Requirement: A node opens and closes, and a press reports it

A press on a row, or Enter while it is the active item, SHALL report its node through `OnPressed` and
make it the active item. When the node may have children, the same press SHALL open it when closed and
close it when open, and SHALL report the change through `OnToggled`.

#### Scenario: A press on a closed folder

- **WHEN** the person presses `src`, closed, on a tree whose `onPressed` records the key
- **THEN** `src` is reported, `OnToggled` reports `src` open, and the next frame shows `App.cs` and
  `Ui.cs` under it

### Requirement: Children not given are loaded when their node first opens

When a node that is not a leaf and has no `Children` opens, `TreeView` SHALL call `LoadChildren` with the
node once, show one item reading the SDK's loading text under it while the task runs, and then show the
children the task returned. A task that fails SHALL leave one item naming the failure, whose press or
Enter SHALL call `LoadChildren` again. Children loaded SHALL be kept by the node's key across the app's
rebuilds, and children the app gives in `Children` SHALL win over them.

#### Scenario: A folder loads once

- **WHEN** a folder whose loader returns `a.txt` after a delay is opened, closed and opened again
- **THEN** the loader ran once, a loading item stood under the folder until the task finished, and
  `a.txt` shows under it the second time without a loading item

#### Scenario: A failed load retries

- **WHEN** the loader throws on its first call and returns `a.txt` on its second, and the person presses
  the error item
- **THEN** the error item names the failure, the loader runs a second time, and `a.txt` shows

### Requirement: The keyboard walks the tree as the WAI-ARIA tree pattern

The tree SHALL be one Tab stop. With the tree focused: Down and Up SHALL move the active item to the
next and the previous visible item; Right SHALL open a closed parent, move into an open parent's first
child, and do nothing on a leaf; Left SHALL close an open parent and otherwise move to the item's
parent; Home and End SHALL move to the first and the last visible item; Page Down and Page Up SHALL move
by the rows the viewport shows, or ten without a viewport; Enter SHALL press the active item. Moving
SHALL NOT change `SelectedKey` nor report a press. The active item SHALL be named to assistive tech
without the focus leaving the tree.

#### Scenario: Walking down and into a folder

- **WHEN** the tree of the first requirement, with `src` closed, has the focus on `src` and receives
  Right, Right, Down
- **THEN** `src` is open after the first Right, the active item is `App.cs` after the second and `Ui.cs`
  after Down, and nothing was pressed

#### Scenario: Left goes back up

- **WHEN** the active item is `Ui.cs` under open `src`, and the tree receives Left, then Left
- **THEN** the active item is `src` after the first Left, and `src` is closed after the second

### Requirement: Typing a label's first letters moves to it

A printable character typed while the tree has the focus SHALL move the active item to the next visible
item, after the active one and wrapping around, whose label starts with the characters typed within
700 ms of each other, compared case-insensitively. Space SHALL NOT start a search.

#### Scenario: Two letters in quick succession

- **WHEN** the visible items are `App.cs`, `Assets`, `README.md`, the active item is `README.md`, and the
  person types `a` then `s` within 700 ms
- **THEN** the active item is `App.cs` after `a`, and `Assets` after `s`

### Requirement: A path is revealed

When `RevealPath` names keys from a root down to a node, `TreeView` SHALL open every key of the path but
the last, loading the children of each that needs it, and SHALL make the last the active item, scrolled
into view when the tree has a height. A new path SHALL be revealed again; the same path given again
SHALL NOT move what the person has since moved.

#### Scenario: The current route's node

- **WHEN** a tree whose `docs` folder loads `guides` (which loads `intro.md`) is given `RevealPath`
  `["docs", "guides", "intro.md"]`
- **THEN** `docs` and `guides` are open, each loaded once, and `intro.md` is the active item
