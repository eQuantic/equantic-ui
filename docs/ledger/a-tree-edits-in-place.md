- **2026-11-16 · A tree edits in place, and its row is the developer's**: F2, or the app's `TreeEditRequest`, puts a
  field over a node's label with the label selected, and a request that creates puts a new item's field first under
  a node. Enter or leaving the field hands the name to the app's task, during which the node is busy, and the tree
  reads the changed folder again when it ends; Escape, an empty name or the same name cancel, and the keys give the
  keyboard back to the tree. `MenuBuilder` opens a menu per node, `BusyKeys` dims what the app works on, and
  `TreeReloadRequest` reads a folder again after a change of the app's own. `RowBuilder` builds a row's content and
  `Style` lays the list family's `ListTheme`, which gains the tree's measures, over the theme's (#1545). Under it,
  for any composite: the keyboard's menu request reaches the active item, a field's keys stay the field's,
  `Navigable.FocusVersion` takes the keyboard back, and `TextEntry.SelectOnFocus` selects as it arrives
  ([#1532](https://github.com/eQuantic/equantic-ui/issues/1532)). Proposed and archived through OpenSpec
  (`openspec/specs/tree-view`, `openspec/specs/navigable-composites`).
