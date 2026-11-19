## Why

Closes #1531, a user story of #1528 (the TreeView feature), under the epic #1510, and needed far beyond the tree:
no node of the vocabulary can say that a person asked for the menu of something. A right-click on a component
opens the browser's own menu, a long press does nothing, and on Photon no shell delivers a right-click at all.
eQuantic Space opens a menu per node of its sidebar's tree, per card of its board and per row of its lists.

## What Changes

- **A block that asks for a menu**: `ContextTarget(child, onRequested)` wraps ANY node and reports a
  `ContextRequest` when the person asks for its menu: a right-click (or a Control-click on a Mac), a long press
  on a touch screen, or the keyboard's menu key or Shift+F10 with the focus inside it. The request says where,
  in the target's own box, or nothing when the keyboard asked. The nearest target around the pointer or the
  focus answers. A block rather than a `Pressable` member, by the composite rule (docs/PRINCIPLES.md): a row,
  a paragraph or a canvas asks for a menu as well as a button does.
- **A panel at a point**: `Anchored.At` places the panel at a point of the anchor's box instead of along one of
  its edges, so a menu opens where the pointer asked for it.
- **`ContextMenu(child, items, onSelect)`**, the ready-made composition of the two over the `Menu`'s own
  panel, keyboard and dismissal; an app that wants another look composes the blocks itself.
- **Web**: one document-level controller answers `contextmenu` (a right-click, Shift+F10, the menu key) over a
  target, where the browser's own menu then does not open, and a finger's hold of 500 ms, which iOS Safari
  reports no `contextmenu` for. A hold that a sortable item takes to lift it is the lift's.
- **Photon**: `PhotonHost.ContextRequest(x, y, kind)` for a shell's secondary click; the host counts a finger's
  hold itself, so every touch shell has the long press; the menu key arrives as `ContextMenu` and Shift+F10 as
  itself. The macOS shell routes the right button and Control-click, the Windows shell the right button and
  the Applications key, the Android shell a mouse's secondary button and the menu key, and the iOS shell a
  pointer's secondary click.
- **Sample**: the dashboard's `/files` screen opens a menu on the open file by right-click, long press and the
  menu key. The tree's menu per node, with the menu key reaching the tree's active item, is #1532's.

## Capabilities

### New Capabilities

- `context-requests`: a node asks for its menu, by the pointer, the finger or the keyboard, on every target.

### Modified Capabilities

- `anchored-panels`: a panel may open at a point of its anchor.

## Impact

- **Vocabulary**: `ContextTarget`, `ContextRequest`, `Anchored.At`; one visitor member. `PublicAPI.Unshipped.txt`
  moves, additions only. The node count of the coverage suites grows by one.
- **Components**: `UI.ContextTarget`, `ContextMenu`, the `Menu`'s panel shared with it.
- **Web realizer and its twin**: the target's mark and its event, the panel's point; `dom/context-request.ts`.
- **Photon**: the paint walk's context regions, the host's request, hold and keys, the four shells' secondary
  input and menu key.
- **eqc, SDKs, templates**: nothing.
- **Docs**: the wiki's Components page in every language, a parity row, a ledger entry.

No break. Rules of `docs/PRINCIPLES.md` followed: 4, 5, 14, and the composite rule.
