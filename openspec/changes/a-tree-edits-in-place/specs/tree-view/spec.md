## ADDED Requirements

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
