## Why

Closes #1529 and #1530, user stories of #1528 (the `TreeView` feature), under the epic #1510 (the
components eQuantic Space needs).

eQuantic Space draws two trees today in React, the Space tree of its shell and a repository's files,
and both move to eQuantic.UI. No component of the SDK draws a tree, and the gap is deeper than a
component: **nothing in the vocabulary can tell a reader that something is a tree**. `NavigableRole`
knows only `Grid`, `PressableRole` has no tree item, and no node says where an item stands in a
hierarchy, its level and its place among its siblings, which a flat list of visible rows cannot let a
reader infer. An app that composed a tree from `Column`s and `Pressable`s today would be read as a
list of buttons, and would have to invent its own keyboard.

## What Changes

- **Vocabulary** (`eQuantic.UI.Primitives`), three additions, nothing removed:
  - `NavigableRole.Tree`, appended: the composite's one Tab stop is a tree, and its rows are its visible
    items in reading order.
  - `PressableRole.TreeItem`, appended: one item of a tree, out of the Tab order as a grid cell is,
    picked through `Selected` and open or closed through `Expanded`.
  - `TreePlace(int Level, int PositionInSet, int SizeOfSet)`, a value type named after .NET's own
    `AutomationProperties.Level`, `PositionInSet` and `SizeOfSet`, carried by `Pressable.TreePlace`.
  - `Navigable.OnTextSearch`: a printable character typed while the composite holds the focus, which
    is how a tree, a list or a menu jumps to an item by its first letters (WPF's `TextSearch`).
  - `SemanticRole.Tree` and `SemanticRole.TreeItem`, appended, and `SemanticNode.TreePlace`, so Photon
    announces what the web does.
- **Component** (`eQuantic.UI.Components`): `TreeView`, and the record `TreeNode(Key, Label)` it is made
  of (an icon, whether it is a leaf, its children when the app has them). A node whose children are not
  given is loaded when it first opens, through an async loader the app hands the tree, with a loading
  row and an error row that retries. Open and closed nodes are the tree's own state, started from the
  keys the app names, and `RevealPath` opens the ancestors of a node and puts the keyboard on it. The
  current node is the app's (`SelectedKey`); a press or Enter reports the node, and opens or closes it
  when it has children. The keyboard is the WAI-ARIA tree pattern: Up and Down, Right and Left, Home and
  End, Page Up and Page Down, Enter, and type-ahead. Given a height, the tree scrolls and keeps the item
  the keyboard is on in view.

What a developer writes, the Space's file tree under this design:

```csharp
TreeView(
    roots: [.. entries.Select(e => new TreeNode(e.Path, e.Name)
    {
        Icon = e.IsDirectory ? Icons.Folder : Icons.File,
        IsLeaf = !e.IsDirectory,
    })],
    selectedKey: _openPath,
    onPressed: node => Navigator.Go($"/code/{node.Key}"))
{
    LoadChildren = node => Repository.ListAsync(node.Key),
    Label = Strings.Files,
    Height = SizeValue.Fill,
}
```

What the browser does: one `role="tree"` element is the Tab stop and names the active item with
`aria-activedescendant`; each visible item is a `role="treeitem"` carrying `aria-level`,
`aria-posinset`, `aria-setsize`, `aria-expanded` on a node with children and `aria-selected`. What the
native shell does: VoiceOver on macOS hears an outline of rows with their disclosure level, and the
mobile bridges hear each item, its state and its place.

## Capabilities

### New Capabilities

- `tree-view`: the `TreeView` component, its nodes, its lazy loading, its keyboard and its reveal.

### Modified Capabilities

- `screen-reader-parity`: a tree and its items are read with their level and their place on every
  target.

## Impact

Parts reached, in the order of the work:

- **Vocabulary** (`eQuantic.UI.Primitives`): `NavigableRole.cs`, `PressableRole.cs`, `Pressable.cs`,
  `Navigable.cs`, `TreePlace.cs` (new), `Contracts/SemanticNode.cs`. `PublicAPI.Unshipped.txt` moves,
  additions only.
- **Components** (`eQuantic.UI.Components`): `TreeView.cs`, `TreeNode.cs` (new), `UI.cs` (the
  `TreeView` factory), `SdkStrings.cs` and its `.resx` files (the loading and error rows).
  `PublicAPI.Unshipped.txt` moves, additions only.
- **eqc**: a delegate of two or more type arguments (`Action<TreeNode, bool>`, the tree's `OnToggled`)
  is annotated as that many parameters (`(arg1: TreeNode, arg2: boolean) => void`), as `Func<…>` already
  was; it was read as one type named `TreeNode, bool`, which the runtime's own type check refused. The
  transpiled twins of `TreeView`, `TreeNode`, `TreeViewRow` and `UI` are regenerated.
- **Web realizer and its TypeScript twin**: `WebLoweringVisitor.Interaction.cs` (`LowerNavigable`,
  `LowerPressable`) with `lowering.ts`, `vocabulary.ts`, `nodes.ts` and the value-type twin of
  `TreePlace`. The served runtime grows; `ServedRuntimeBudgetTests` decides whether its budget moves.
- **Photon**: `SemanticsVisitor.Interaction.cs`, `NativeRole.cs` (two rows), `PhotonHost.cs` (text
  search reaches a focused composite), the macOS bridge (the disclosure level of an outline row).
- **Testing** (`eQuantic.UI.Testing`): a tree and a tree item are announced in the vocabulary's words,
  with their place.
- **SDKs, templates, `appsettings`**: nothing. `developer-surface.baseline.txt` does not move.
- **Samples**: the dashboard gains a files screen with a lazily loaded tree.
- **Docs**: the wiki's components page in every language it is written in, a row in
  `docs/FLUTTER-PARITY.md`, one ledger entry under `docs/ledger/`.

One break, binary only and in a server-only type: `SemanticNode`, which only the Photon walk and its
bridges build, gains a trailing optional `TreePlace`, so its constructor and its `Deconstruct` change
shape (two `*REMOVED*` lines in `eQuantic.UI.Primitives`). Migration line: none in source; recompile.
Every other member is added, and every enum value is appended (`EnumValueAbiTests`).

Rules of `docs/PRINCIPLES.md` followed: 2 (a tree for every app, the Space's two included, not the
Space's tree), 3 (`TreePlace` and `OnTextSearch` take .NET's own names), 4 (written once in
`TreeView`, realized by both targets), 5 (`Tree`, `TreeItem` and `TreePlace` pass "would it exist
without the web": UIA, AppKit and Flutter all say tree), 6 (Flutter's `TreeSliver` answered first),
9 (the enums' exhaustive switches name the new values in every realizer, so a realizer that forgets
fails the build), 14 (the gap is filled in the SDK, not in the Space). None is bent.
