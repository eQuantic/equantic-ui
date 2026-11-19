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

### Requirement: A node is renamed in place

When the tree has an `OnRenamed`, F2 with the tree focused, or a new `TreeEditRequest` naming a node, SHALL replace
that node's label with a text field holding the label, all of it selected, with the keyboard in it. Enter, or the
field losing the keyboard, SHALL call `OnRenamed` with the node and the name typed without the spaces around it,
unless that name is empty or the label unchanged, which SHALL cancel. Escape SHALL cancel. After Enter or Escape the keyboard SHALL
be back on the tree, its active item the node. Without `OnRenamed`, F2 and a rename request SHALL do nothing.

#### Scenario: F2, a new name and Enter

- **WHEN** the active item is `App.cs`, the person presses F2, types `Main.cs` and presses Enter
- **THEN** a field holding `App.cs`, selected, stood in the row; `OnRenamed` received `App.cs` and `Main.cs`; and
  the keyboard is on the tree with `App.cs` the active item

#### Scenario: Escape and an empty name cancel

- **WHEN** a rename is started, Escape pressed; then another started, its text cleared, and Enter pressed
- **THEN** `OnRenamed` was never called and the label is unchanged

### Requirement: An item is created in place

When the tree has an `OnCreated`, a new `TreeEditRequest` with `Creates` SHALL open the node it names (loading its
children if it must) and show a text field as the first row under it, at its children's indentation, with the
request's `Icon`. Enter, or the field losing the keyboard, SHALL call `OnCreated` with the node and the name typed
without the spaces around it, unless that name is empty; Escape or an empty name SHALL drop the field. After Enter or Escape the keyboard
SHALL be back on the tree.

#### Scenario: A new file in a closed folder

- **WHEN** `src`, closed, is the subject of a creating request with a file icon, and the person types `New.cs` and
  presses Enter
- **THEN** `src` opened, a field with the file icon stood first under it at level 2, and `OnCreated` received `src`
  and `New.cs`

### Requirement: The app's task keeps the node busy, and the tree shows its outcome

While the task `OnRenamed` or `OnCreated` returned runs, the node renamed, or the node created under, SHALL be busy.
When the task ends, the tree SHALL call `LoadChildren` again for the node whose children the change is in (the
renamed node's parent, or the node created under) when the tree had loaded them, and SHALL show the new children
when they arrive, keeping the old ones until then. A task that fails SHALL leave the tree as it was.

#### Scenario: A rename in a loaded folder

- **WHEN** `App.cs`, loaded under `src`, is renamed to `Main.cs` and the app's task completes
- **THEN** `App.cs` was busy while the task ran, `src`'s loader ran a second time, and its rows now show what that
  load returned

### Requirement: The app reads a node's children again

A new `TreeReloadRequest` SHALL call `LoadChildren` again for each key it names whose children the tree had
loaded, keeping the old children on screen until the new ones arrive, and SHALL do nothing for a key whose
children the app gives or the tree never loaded. A version already honoured SHALL NOT reload again.

#### Scenario: A deletion the app made

- **WHEN** the app deletes `App.cs` and gives a reload request naming `src`
- **THEN** `src`'s loader runs again once, and `App.cs` is gone from the rows when it returns

### Requirement: A node opens its menu

When the tree has a `MenuBuilder`, a context request on a node's row (a secondary click, a finger's hold, or the
menu key or Shift+F10 while it is the active item) SHALL open, anchored to the row (at the pointer's point when a
pointer asked), the menu of the items `MenuBuilder` returned for that node, with the `Menu`'s keys and dismissal;
choosing an item SHALL report the node and the item's index through `OnMenuSelect` and close the menu. The
request SHALL make the node the active item. A builder returning no items SHALL open nothing.

#### Scenario: The menu key on the active item

- **WHEN** the active item is `src`, and the person presses the menu key, ArrowDown and Enter on a tree whose
  builder gives `Rename` and `Delete`
- **THEN** a menu of `Rename` and `Delete` opened under `src`'s row, and `OnMenuSelect` received `src` and 1

### Requirement: A busy node takes no input

A node named in `BusyKeys`, or busy with the tree's own task, SHALL be drawn at the theme's disabled opacity and
stated as disabled. A press on it, Enter, F2 and a context request SHALL do nothing, while the keyboard SHALL still
walk over it.

#### Scenario: A node the app is moving

- **WHEN** `App.cs` is in `BusyKeys`, the person walks onto it and presses Enter, then F2, then the menu key
- **THEN** nothing is pressed, no field and no menu open, and `App.cs` is the active item

### Requirement: A row's content and measures are the developer's

When the tree has a `RowBuilder`, each node's row SHALL show what the builder returns for its `TreeRowState` (the
node, its level, whether it is open, whether it is selected and whether it is busy) after the chevron's slot, in
place of the icon and the label, while the tree keeps the row's indentation, chevron, fill, focus ring and every
state a reader hears; a row being edited SHALL show the tree's own content. `Style` SHALL be laid over the theme's
`ListTheme` and the tree SHALL read from the result its row height, indentation, horizontal padding, gap, row shape,
icon colour, chevron colour, hover fill and label colour, each falling back to its default when null. The chevron
SHALL keep the tree's own size, not the accordion's.

#### Scenario: A row with a badge

- **WHEN** a tree's builder returns a row of the node's label and a badge of its unread count, and its `Style` sets
  `TreeIndent` to 24
- **THEN** each row shows the label and the badge after its chevron, a level-2 row starts 24 dp further in than a
  level-1 row, and the rows are still tree items with their level and place

### Requirement: A node moves by drag, and the app is told where

A tree with `OnMoved` SHALL let a pointer pick a node row up, as a sortable item, and carry it over the tree. Over the
middle half of a row it SHALL land inside that row's node, as its first child; over the rest of a row it SHALL land
before or after it, among that row's siblings. The drop SHALL reach `OnMoved` as a `TreeMove` holding the node, its
new parent (null at the top level) and the sibling it lands after (null when it lands first). A drop at the place the
node already holds SHALL call nothing. Status rows and the creation field SHALL NOT be picked up, and neither SHALL a
busy node nor any node while a field is open.

#### Scenario: A file dragged into a folder

- **WHEN** a tree shows `docs` (closed) above `README.md`, and the person drags `README.md` and releases it over the
  middle of `docs`
- **THEN** `OnMoved` receives a `TreeMove` whose `Node` is `README.md`, whose `Parent` is `docs` and whose `After` is
  null

#### Scenario: A file dragged between two others

- **WHEN** an open `src` holds `App.cs`, `Program.cs` and `Ui.cs`, and the person releases `Ui.cs` over the top quarter
  of `Program.cs`
- **THEN** `OnMoved` receives a `TreeMove` whose `Node` is `Ui.cs`, whose `Parent` is `src` and whose `After` is
  `App.cs`

### Requirement: A node never lands inside a file or inside itself, and the app may refuse a place

A place whose new parent is a leaf, or the carried node, or one of its descendants SHALL NOT be offered. Every other
place SHALL be offered to `CanMove` first; a place it refuses SHALL be neither shown nor dropped on.

#### Scenario: A folder before its own child

- **WHEN** `src` is open and holds `App.cs` and `Program.cs`, and the person carries `src` over the top quarter of
  `Program.cs` and releases it
- **THEN** no indicator was drawn there and `OnMoved` did not run

#### Scenario: A file over a file

- **WHEN** the person carries `README.md` over the middle of the leaf `App.cs` and releases it
- **THEN** `OnMoved` did not run

#### Scenario: The app refuses a folder

- **WHEN** `CanMove` refuses every move into `locked`, and the person carries `README.md` over the middle of `locked`
- **THEN** `locked` draws no ring and a release there calls nothing

### Requirement: The tree shows where a node would land

While a node is carried, the row it would land before SHALL draw a line along its top edge from its own indentation,
the last row SHALL draw it along its bottom edge when the node would land after it, and the row it would land inside
SHALL draw its ring in `TreeDropColor` over the theme's selection tint. No row SHALL change its size while the
indicator moves. `TreeDropColor` SHALL default to the theme's focus ring.

#### Scenario: The line before a row

- **WHEN** the person carries `Ui.cs` over the top quarter of `Program.cs`
- **THEN** `Program.cs`'s row draws the line along its top edge, and every row keeps its height

### Requirement: A closed folder opens under a pause

A carried node held inside a closed node that has children SHALL open that node after 700 ms, loading its children if
it must, as a press would. Moving to another place, dropping or ending the drag before then SHALL leave it closed.

#### Scenario: Spring-loading a folder

- **WHEN** the person carries `README.md` over the middle of the closed `docs` and holds it there for a second
- **THEN** `docs` opens and its children are shown under it while `README.md` is still carried, and the row
  carried and dimmed is still `README.md`'s: each row is keyed by its node, so the rows that open take elements of
  their own

### Requirement: The app's move keeps the node busy, and the parents are read again

`OnMoved` SHALL return a task. While it runs the moved node SHALL be busy; when it ends, the children the tree loaded
for the node's old parent and for its new parent SHALL be read again.

#### Scenario: A move between two loaded folders

- **WHEN** `src` and `docs` are open and loaded, and `App.cs` is moved from `src` into `docs`
- **THEN** `App.cs` is busy until the task ends, and then both `src` and `docs` load their children again

### Requirement: The keyboard moves a node

With the focus on the tree and a node active, Space SHALL pick that node up, the up and down arrows SHALL step it
through every place, before each row, inside it and after the last, Space or Enter SHALL drop it as a pointer's drop,
and Escape SHALL put it back. After the drop the tree SHALL keep the focus with the moved node active.

#### Scenario: A file moved into a folder by the keyboard

- **WHEN** `README.md` is active below the closed `docs`, and the person presses Space, ArrowUp and Enter
- **THEN** `OnMoved` receives a `TreeMove` whose `Node` is `README.md` and whose `Parent` is `docs`, and the focus is on
  the tree with `README.md` active
