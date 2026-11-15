## Context

- `Navigable` (`src/eQuantic.UI.Primitives/Nodes/Navigable.cs`) is the vocabulary's two-dimensional
  composite: one Tab stop, a keyboard read through `NavigableKeys` into `NavigableMove`s on both
  targets, rows declared as a structural claim, and the active cell named by `ActiveCell`, which the web
  lowers to `aria-activedescendant`. Its only role is `Grid`; the calendar is its one user.
- `Pressable.Role` says what an item IS inside a composite (`Radio`, `Tab`, `MenuItem`, `Option`,
  `GridCell`…). The web realizer lowers each role in `LowerPressable` (C#) and `lowerPressable` (TS);
  Photon's `SemanticsVisitor` maps each to a `SemanticRole` in a switch with no default arm, and
  `NativeRole.Of` maps each `SemanticRole` to AppKit, UIKit and Android, also without a default arm.
- `SemanticNode` already carries a table cell's place (`Place`, `Extent`) and a heading's level; the
  macOS bridge already writes `AXDisclosureLevel` for a heading.
- `ListView` is the precedent for a component that owns its `ScrollView` and computes positions from a
  fixed item extent.

What eQuantic Space's two trees do today is inventoried in #1528: whole trees fetched per Space and
lazy directories in the file tree; a press on a folder opens or closes it; the current route marks a
row and opens its ancestors; no keyboard and no tree semantics at all.

## How Flutter solves it

Flutter's `TreeSliver` (with `TreeSliverNode<T>` and `TreeSliverController`) lays the VISIBLE nodes out
as one flat sliver, each row built by a `treeNodeBuilder` from its node and its depth, indented by an
`indentation` the tree owns, and reports `onNodeToggle`. Expansion is the controller's state, started
from each node's `expanded` flag. It loads nothing lazily (an app swaps the node's children) and adds no
tree semantics of its own: its rows are whatever the builder returns.

What this change keeps from it: the flat list of visible rows (it is also what virtualization and the
web's own large trees, VS Code's among them, are built on), the tree owning indentation and expansion,
and the toggle report (`OnToggled`). Where it does better: the tree states itself to a reader on every
target, it loads children through an async loader with loading and error rows, and it carries the
keyboard of the WAI-ARIA tree pattern.

`docs/FLUTTER-PARITY.md` gains a row: `TreeSliver` against `TreeView`.

## Goals / Non-Goals

**Goals:** the vocabulary can state a tree, its items, their level and their place; a `TreeView` that
an app fills from data it has or loads, walked by the keyboard as the WAI-ARIA tree pattern, read the
same on the web and on Photon.

**Non-Goals (later stories of #1528):** editing in place and a menu per node (#1532, after the context
request of #1531), dragging a node (#1533, after #1535), several selected nodes, virtualization of very
large trees, and a theme slot of its own.

## Decisions

### 1. A tree is a ROLE of the composite and of its items, not two new nodes

The keyboard model of a tree is the composite model `Navigable` already has: one stop, the moves
reported, the active item named without the focus leaving. A `TreeView` node pair would duplicate that
machinery in both realizers. So the tree is `NavigableRole.Tree`, and its items are
`PressableRole.TreeItem`, each appended (the enums are an ABI, `EnumValueAbiTests`).

The moves keep their names, and the component gives each its tree meaning, as the calendar gives
`NextPage` the meaning of a month: `PreviousRow`/`NextRow` are Up and Down, `PreviousItem`/`NextItem`
are Left and Right (close or go to the parent; open or go to the first child), `RowStart`/`RowEnd` are
Home and End, and a page moves by a viewport's worth of rows.

Alternatives: a `Tree` node of its own (rejected: a second composite keyboard in each realizer, for no
behaviour the composite lacks); nesting `role="group"` elements so a browser computes the level
(rejected: the rows are a flat list on both targets, and a nested DOM is what makes a long tree
impossible to window later).

### 2. Where an item stands is stated, with .NET's names

A flat list of visible rows cannot let a reader infer an item's depth or its place among its siblings,
so the item states them: `TreePlace(Level, PositionInSet, SizeOfSet)`, all three 1-based, the names and
the base of `System.Windows.Automation.AutomationProperties`, which is how .NET itself says the same
three facts to UI Automation. ARIA's `aria-level`, `aria-posinset` and `aria-setsize` are the same three
numbers, and AppKit's `AXDisclosureLevel` is the level less one.

It rides `Pressable.TreePlace` and `SemanticNode.TreePlace`, null for anything that is not a tree item.
A role other than `TreeItem` ignores it: ARIA admits `aria-level` on a tree item and not on a button,
and a member that says it is for trees is honest where a general "place in a set" would promise more
than any realizer does.

### 3. A tree item is not a button on the web

ARIA in HTML does not allow `role="treeitem"` on a `<button>`, so a `TreeItem` pressable lowers to a
`div` carrying the click, `tabindex="-1"`, and `aria-disabled` rather than `disabled`. Its keyboard is
the tree's, on the host, so the button's own Enter and Space are not missed. The server and the browser
write the same element (the parity fixture compares them).

### 4. The rows of a tree are not wrapped in rows

A grid's cells must sit in `row`s, which is why `LowerNavigable` wraps each declared row in a
`display:contents` `role="row"` element. A tree's items must NOT: a `row` inside a `tree` is invalid. A
tree's declared rows are lowered as they are, and the tree items inside them are numbered for
`aria-activedescendant` by the walk that numbers a grid's cells, with the same id scheme.

### 5. Type-ahead is text search on the composite

WAI-ARIA recommends type-ahead for a tree, a listbox and a menu, and WPF calls it `TextSearch`
(`ItemsControl.IsTextSearchEnabled`). `Navigable.OnTextSearch` reports a printable character typed while
the composite holds the focus, without a modifier other than Shift and never Space, and the component
decides what it means: `TreeView` gathers the characters typed within 700 ms of each other and moves to
the next visible item whose label starts with them, case-insensitively, wrapping around. The web reads
`keydown` with a one-character `key`; Photon routes `TextInput` to a focused composite that has no field
being edited.

### 6. Focus moves with the keys, selection is the app's, a press opens a parent

The Space's tree is a NAVIGATION tree: the selected node is the current route, and moving through the
tree must not navigate. So the arrows move the ACTIVE item only (`ActiveCell`); `SelectedKey` is the
app's, and is the one item with `aria-selected="true"` (the others say `false`, which ARIA's
single-select tree asks for). Enter and a press report the node through `OnPressed`, and a node with
children also opens or closes, as the Space's folders do and as VS Code's explorer does on a single
click. A press also makes the pressed item the active one.

### 7. Lazy children through an async loader, keyed by the node's key

A node whose `Children` is null and that is not `IsLeaf` asks `LoadChildren` when it first opens. While
the task runs, a "Loading…" item stands under it; when it fails, an item names the failure and retries
on a press or Enter; when it succeeds, the children are kept by the node's KEY, so a parent that builds
its nodes again on every frame keeps what was loaded. Children the app gives win over what was loaded,
which is how an app that re-reads its tree after a change (the Space) shows the change. The loading and
error items are tree items one level down, so the keyboard reaches them and a reader hears them in
place.

### 8. The tree scrolls only when it is given a height

Without a height the tree's rows flow in the page and the page scrolls. With one, the tree wraps its
rows in a `ScrollView` and, when the keyboard moves the active item out of the viewport, asks the view
to bring it back (`ScrollRequest`), computed from the fixed row height the tree draws with and the
offset and viewport the view reports, as `ListView` does. `RevealPath` uses the same request.

### 9. Visuals from the theme's tokens

Rows are `ControlMetrics.Height(context, SizeVariant.Small)` tall, indented by `Space.S4` per level,
with a chevron, the node's icon and its label; hover and the selected row use the surface tokens the
list and the menu use. The active row wears the focus ring's colour once the KEYBOARD has moved it,
and loses it on a press, the modality rule `:focus-visible` follows: a pointer user sees the selected
row, a keyboard user sees where the arrows are. A `TreeSlot` in `IAppTheme` is left for when a design
system asks for one (non-goal).

## Risks / Trade-offs

- [The active row keeps its ring after the focus leaves the tree by Tab, because no node reports a
  composite's focus yet] → the ring follows the keyboard's last move, which is where the focus returns;
  a focus report on `Navigable` can come when a second composite needs it.
- [Children loaded by key go stale when the app changes them without giving them] → the app gives the
  children it re-read, which win; a refresh by key is left for when an app needs it.
- [Text search on Photon depends on the shell sending `TextInput` for a key press outside a field] →
  `KeyboardTests` drive it through the host, which is what every shell calls.

## Migration Plan

Nothing to migrate: every member is added, every enum value appended.

## Open Questions

None left for this change.
