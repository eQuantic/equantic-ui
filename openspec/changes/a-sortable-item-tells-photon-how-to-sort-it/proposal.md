## Why

Closes #1596, a task of #1534 (sortable lists and drag and drop), under the epic #1510 (the components eQuantic Space
needs). On the web a focused sortable item describes how to sort it: the list's instructions ("Press Space to pick it
up, the arrow keys to move it, and Space again to drop it.") are the focused control's `aria-describedby`, which a
screen reader reads after its name. On Photon nothing describes the keys: `SemanticNode` has no hint, so no shell can
carry one, and #1564 left it out for that reason.

## What Changes

- **A node carries a hint.** `SemanticNode.Hint` is what a control does when it is used, or how to use it, read after its
  name and value. Flutter's semantics name it `hint`. It is appended to the record, as every field before it was.
- **A sortable item's stops are described.** The semantics walk gives every keyboard stop inside an enabled
  `SortableItem` (a pressable's, a link's, an adjustable's) the instructions of the list the item belongs to, from the
  node's `Announcements` (#1564), so an app's own words reach it as the SDK's do. A field inside an item keeps its own
  keys and gets none, as on the web, and a list with no words gives none.
- **Each shell carries it in its platform's word.** macOS sets it as the element's `accessibilityHelp`, iOS as its
  `accessibilityHint`, and Android as its node's tooltip text (from API 28, the platform's floor for it), which TalkBack
  reads after the node. Windows carries it when its UI Automation provider lands (#310).

Parts reached: the vocabulary (`SemanticNode`), the Photon semantics walk, and the macOS, iOS and Android
accessibility bridges. The web does not change. `SemanticNode`'s constructor widens with an optional parameter. The
developer surface does not move.

Principles followed: 1 (nothing for the developer to write: the words are the list's), 4 (one walk decides the hint and
every shell only carries it), 6 (Flutter's `SemanticsProperties.hint` names it), 11 (the item stays a wrapper: the hint
lands on the stops inside it).

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `native-accessibility`: a semantic node may carry a hint, which each shell sets in its platform's word.
- `sortable-lists`: on Photon a sortable item's keyboard stops are described by the list's instructions, as on the web.

## Impact

- `src/eQuantic.UI.Primitives`: `SemanticNode.Hint`.
- `src/eQuantic.UI.Native.Components`: the semantics walk (`SemanticsVisitor`).
- `src/eQuantic.UI.Native.Shell.MacOS`, `.iOS`, `.Android`: each accessibility bridge.
- Docs: the ledger, `docs/FLUTTER-PARITY.md`'s sortable row (its PARTIAL named #1596), the wiki's Sortable section.
