## Why

Closes #1532, a user story of #1528 (the TreeView feature), under the epic #1510. eQuantic Space's sidebar renames a
container where it stands, creates one under a node, and opens a menu per node, as every file tree does (VS Code's
explorer, Finder, Windows Explorer). `TreeView` (#1530) only shows and walks a tree; its row is fixed fields and its
look is not the developer's to change, which #1545 (every ready-made component takes the developer's own content,
builders and style) settled for this slice.

## What Changes

- **Rename in place.** F2 on the keyboard's node, or the app's `TreeEditRequest`, puts a text field where the
  node's label is, holding the label, selected. Enter or leaving the field hands the name to `OnRenamed`; Escape,
  an empty name or the same name cancel. Enter and Escape give the keyboard back to the tree.
- **Create in place.** A `TreeEditRequest` with `Creates` opens a node (loading it if it must) and shows a field
  under it, at its children's indentation, with the request's icon. Enter or leaving it hands the name to
  `OnCreated`; Escape or an empty name drop it.
- **The app's task, and the tree's patience.** `OnRenamed` and `OnCreated` return a task. While it runs the node
  (for a creation, its parent) is busy; when it ends, the children the tree loaded for the parent are read again,
  so the change shows without the app doing anything more. `TreeReloadRequest` reads the children of the nodes it
  names again, for what the app changes itself (a deletion from its menu).
- **A menu per node.** `MenuBuilder` gives the `MenuItem`s of a node; the tree opens them on the node's context
  request (a right-click, a finger's hold, the menu key or Shift+F10 on the keyboard's node), anchored to the row,
  and reports the choice through `OnMenuSelect`. What an item does is the app's.
- **Busy.** A node named in `BusyKeys`, or whose rename or creation is running, is dimmed and takes no press, no
  menu and no edit.
- **The row is the developer's.** `RowBuilder` builds what a row shows after its chevron, from a `TreeRowState`
  (the node, its level, whether it is open, selected and busy); the tree keeps the row itself (indentation,
  chevron, fill, focus ring, and what a reader hears). `Style` is the list family's `ListTheme`, laid over the
  theme's, which gains the tree's measures (row height, indentation, padding, gap, shape, icon colour).
- **Vocabulary, for every composite, not only the tree:**
  - the keyboard's menu request in a composite whose focus is its active item (a tree, a grid) reaches that
    item's target, on the web through `aria-activedescendant` and on Photon through the active cell;
  - a key typed into a field inside a `Navigable` is the field's: the composite moves and searches only on keys
    its own host receives;
  - `Navigable.FocusVersion` takes the keyboard back to the composite's one stop;
  - `TextEntry.SelectOnFocus` selects the entry's text as it takes the keyboard.
- **Sample.** The dashboard's `/files` tree renames, creates files and folders, deletes, and opens its menu.

## Capabilities

### New Capabilities

- `navigable-composites`: a composite's one stop takes the keyboard back on request, and leaves the keys of a field
  inside it to the field.

### Modified Capabilities

- `tree-view`: rename and create in place, a menu per node, busy nodes, a reload, a row builder and a style.
- `context-requests`: the keyboard's request in a composite reaches its active item.
- `text-entry`: an entry may select its text as it takes the keyboard.

## Impact

- **Vocabulary**: `Navigable.FocusVersion`, `TextEntry.SelectOnFocus`, the tree's members of `ListTheme`; additions
  only in `PublicAPI.Unshipped.txt`.
- **Components**: `TreeView`'s new members, `TreeRowState`, `TreeEditRequest`, `TreeReloadRequest`; the `UI.TreeView`
  factory takes each new member as an optional parameter, as it takes the loader, so the declarative surface reaches
  them. The tree's own SDK string, `TreeItemName`, names its fields.
- **Web**: `dom/context-request.ts` follows the active descendant; the lowering's composite leaves a field's keys to
  it and honours a focus version; the entry selects on focus.
- **Photon**: the keyboard's context request follows the active cell; a focus version moves the host's focus; a
  field selects on focus.
- **eqc, SDKs, templates**: nothing.
- **Docs**: the wiki's Components page in every language (the TreeView and ContextMenu sections), a ledger entry.

No break. Rules of `docs/PRINCIPLES.md` followed: 4, 5, 14, and the composite rule.
