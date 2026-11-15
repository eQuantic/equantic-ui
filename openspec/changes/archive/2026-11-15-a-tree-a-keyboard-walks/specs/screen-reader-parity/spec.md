## ADDED Requirements

### Requirement: A tree and its items are read with their level and place on every target

A `Navigable` whose role is `Tree` SHALL be announced as a tree named by its `Label`, after which its
items SHALL be read in order. A `Pressable` whose role is `TreeItem` SHALL be announced as a tree item,
with its `Expanded` and `Selected` states and, when it carries a `TreePlace`, its level, its position in
its set and the set's size: on the web a `role="treeitem"` element (never a `<button>`) with
`aria-level`, `aria-posinset`, `aria-setsize`, `aria-expanded` and `aria-selected`, inside a
`role="tree"` host whose items are not wrapped in rows; on Photon a `SemanticRole.TreeItem` node carrying
the same `TreePlace`, after a `SemanticRole.Tree` node. On macOS the item SHALL be an outline row whose
disclosure level is its level less one.

#### Scenario: A two-level tree read on both targets

- **WHEN** a `Navigable` with role `Tree` labelled `Files` holds a tree item `src` (level 1, 1 of 1,
  expanded) and a tree item `App.cs` (level 2, 1 of 1, selected), and is read on the web through
  `ComponentTester` and on Photon through `PhotonHost.Semantics()`
- **THEN** both readings are `Tree "Files"`, `TreeItem "src"` (level 1, 1 of 1, expanded) and
  `TreeItem "App.cs"` (level 2, 1 of 1, selected), in that order

#### Scenario: The server and the browser write the same tree

- **WHEN** the same tree is lowered by the server's realizer and by the browser's twin
- **THEN** both write a `role="tree"` host with `tabindex="0"`, `aria-activedescendant` naming the
  active item's id, and `div` tree items carrying the same ids and attributes
