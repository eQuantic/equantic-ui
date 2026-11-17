## ADDED Requirements

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

### Requirement: A node never lands inside itself, and the app may refuse a place

A place whose new parent is the carried node or one of its descendants SHALL NOT be offered. Every other place SHALL be
offered to `CanMove` first; a place it refuses SHALL be neither shown nor dropped on.

#### Scenario: A folder over its own child

- **WHEN** `src` is open and holds `App.cs`, and the person carries `src` over the middle of `App.cs` and releases it
- **THEN** no indicator was drawn there and `OnMoved` did not run

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
- **THEN** `docs` opens and its children are shown under it while `README.md` is still carried

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
