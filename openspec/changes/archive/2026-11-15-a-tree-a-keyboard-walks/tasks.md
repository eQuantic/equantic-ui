Groups 1 to 3 are one build: appending to `PressableRole`, `NavigableRole` and `SemanticRole` makes
every exhaustive switch that does not name the new values fail to compile (CS8509), in the web realizer,
Photon's semantics walk and `NativeRole`. Each group's check runs once the solution compiles.

## 1. The vocabulary

- [x] 1.1 `NavigableRole.Tree`, `PressableRole.TreeItem`, `SemanticRole.Tree` and `SemanticRole.TreeItem`, appended, with XML docs saying what each is to a reader
- [x] 1.2 `Nodes/TreePlace.cs`: `public readonly record struct TreePlace(int Level, int PositionInSet, int SizeOfSet)`, 1-based, with its value-type twin; `Pressable.TreePlace` and `SemanticNode.TreePlace`, null by default
- [x] 1.3 `Navigable.OnTextSearch` (`Action<string>?`)
- [x] 1.4 `./scripts/public-api.sh update` for Primitives; the diff holds additions only
- [x] 1.5 Check: `EnumValueAbiTests` pins the appended values; the Primitives project builds with no warning

## 2. The web realizer and its TypeScript twin

- [x] 2.1 `LowerNavigable` (C#) and `lowerNavigable` (TS): `role="tree"` for `Tree`, rows not wrapped, tree items numbered by the cell walk; a `keydown` that hands a printable character to `OnTextSearch`
- [x] 2.2 `LowerPressable` and `lowerPressable`: a `TreeItem` is a `div` with `role="treeitem"`, `tabindex="-1"`, `aria-selected` when `Selected` is set, `aria-disabled` when disabled, and `aria-level`, `aria-posinset`, `aria-setsize` from `TreePlace`
- [x] 2.3 `eQuantic.UI.Testing`: `tree` and `treeitem` announce `SemanticRole.Tree` and `TreeItem`, and an announcement carries the item's `TreePlace`
- [x] 2.4 Check: `GridSemanticsTests`' twin for a tree (`TreeSemanticsTests`), the parity fixture with a tree case, and a vitest of the TS lowering driven by `keydown`, each failing with the tree lowered as a grid

## 3. Photon

- [x] 3.1 `SemanticsVisitor`: a `Tree` navigable announces `SemanticRole.Tree` as a group; a `TreeItem` pressable announces `SemanticRole.TreeItem` with `Selected`, `Expanded` and `TreePlace`
- [x] 3.2 `NativeRole`: rows for `Tree` (AXOutline) and `TreeItem` (AXRow, subrole AXOutlineRow), with the Android and UIKit words and why
- [x] 3.3 macOS bridge: a tree item's disclosure level is its level less one
- [x] 3.4 `PhotonHost.TextInput`: a character typed with no field being edited reaches a focused composite's `OnTextSearch`
- [x] 3.5 Check: `dotnet test tests/eQuantic.UI.Native.Engine.Tests` passes with a tree read through `PhotonHost.Semantics()` and text search driven through the host, and `AppKitAccessibilityTests` reads a tree item back from AppKit as an outline row with its level

## 4. The component

- [x] 4.1 `TreeNode.cs`: `public sealed record TreeNode(string Key, string Label)` with `Icon`, `IsLeaf`, `Children`
- [x] 4.2 `TreeView.cs`: visible rows, open state from `ExpandedKeys`, `SelectedKey`, `OnPressed`, `OnToggled`, `LoadChildren` with the loading and error items, the keyboard of the tree pattern, text search, `RevealPath`, and a `ScrollView` that keeps the active row in view when given a height
- [x] 4.3 `SdkStrings`: the loading and error items' words, in every `.resx`, and their TypeScript twin
- [x] 4.4 `UI.cs`: the `TreeView` factory, mirroring the constructor; the transpiled twins regenerated (`EQ_UPDATE_TRANSPILED=1`); eqc annotates `Action<TreeNode, bool>` as two parameters (`LocalTypeAnnotationTests`)
- [x] 4.5 Check: Photon tests drive the tree by the keyboard frame by frame (walk, open, close, Home, End, Enter, text search, a lazy load and its retry, a reveal); web tests read the tree through `ComponentTester`; each fails when the behaviour it names is removed

## 5. The sample and the docs

- [x] 5.1 The dashboard gains a files screen: a tree of a repository whose folders load through a server action
- [x] 5.2 The wiki's components page, in every language it is written in, on the wiki branch named like the pull request's
- [x] 5.3 A row in `docs/FLUTTER-PARITY.md` (`TreeSliver` against `TreeView`) and one ledger entry under `docs/ledger/` citing #1529 and #1530
- [x] 5.4 Check: the sample in a browser, walked by the keyboard and read by its tree; the docs guards pass against the wiki branch

## 6. Archive

- [x] 6.1 `./scripts/check-openspec.sh` passes, then `openspec archive a-tree-a-keyboard-walks --yes` before the merge
