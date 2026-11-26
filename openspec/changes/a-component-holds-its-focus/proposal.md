## Why

Closes #1565, a sub-issue of #194 (pointer, gestures and focus), itself under the block catalog #190.

A component cannot hold the focus and move it from code. `docs/FLUTTER-PARITY.md` §5 reads `FocusNode`,
`FocusScope`, `FocusManager` as **PARTIAL**: "there is no focus object to hold or move imperatively". What
exists instead is four counters that each reach one kind of stop (`Navigable.FocusVersion`, the code and
sheet surface models' `FocusVersion`), two autofocus flags, one focus callback on a text field, and two
traversal orders that nothing shares: Photon walks `RealizeResult.FocusStops`, the web leaves Tab to the
browser and each module (`dom/focus-trap.ts`, the table viewport's walk, the sortable's refocus) queries the
DOM with a selector of its own. A dialog's trap is therefore written twice (`focus-trap.ts`,
`PhotonHost.ModalFocus.cs`), and nothing at all moves the focus by direction.

The general capability: **the focus is an object a component holds, in a tree of its own, and one traversal,
written once, decides where it goes on every target.** This change is the first slice of that capability
(design.md, "Slices"): the object, the manager, the reading-order traversal both targets run, and the
modal layer's Tab walk folded into it.

## What Changes

- **New vocabulary, written once in `eQuantic.UI.Primitives/Focus/` and transpiled into the runtime as the
  gesture arena is (#1563):**
  - `FocusNode`: `RequestFocus()`, `Unfocus()`, `HasFocus`, `HasPrimaryFocus`, `CanRequestFocus`,
    `SkipTraversal`, `NextFocus()`, `PreviousFocus()`, and `event Action? Changed`, Flutter's names.
  - `FocusScopeNode : FocusNode`: `FocusedChild`, `TraversalEdgeBehavior`.
  - `TraversalEdgeBehavior` (`ClosedLoop`, `ParentScope`, `Stop`), append-only like every vocabulary enum.
  - `FocusManager`: the one object per host a realizer drives (Flutter's `FocusManager` with its
    `WidgetOrderTraversalPolicy`, which walks the tree's order and reads no geometry): it is handed the frame's stops in traversal order, each with the focus
    nodes and scopes it stands under, and answers where Tab, Shift+Tab, `NextFocus`, `PreviousFocus` and
    `RequestFocus` land, and which nodes hear `Changed`.
  - Two capability nodes, each around any child (PRINCIPLES 15): `Focus(child, focusNode?, autofocus,
    onFocusChange, canRequestFocus, skipTraversal)` and `FocusScope(child, node?, autofocus,
    onFocusChange, traversalEdgeBehavior)`, with their factories `UI.Focus` and `UI.FocusScope`.
- **One traversal on both targets.** Photon's `MoveFocus` asks the manager instead of walking the stops
  itself. The web stops leaving Tab to the browser: one driver (`dom/focus-manager.ts`, which absorbs the
  FOCUSABLE selector and the Tab handler of `dom/focus-trap.ts`) hands the manager the stops in document
  order and moves the focus where it answers; at the root's edge it lets the browser's Tab leave the page,
  as Flutter's web embedding does.
- **The modal layer's Tab walk is a scope's.** A modal, open layer stands in the manager's tree as a
  closed-loop scope, so the trap's Tab half (#1066, #1833) is no longer code of its own on either target.
  The trap's restore half (#1813, #1829) and the initial focus into a layer stay as they are in this slice
  and are folded by slice 3 (design.md).
- **BREAKING: `Navigable.FocusVersion` goes.** A composite takes the keyboard back through a `FocusNode`:
  `Focus(focusNode: _focus, child: Navigable(...))` and `_focus.RequestFocus()`. `TreeView` is the one
  caller in the tree and moves; `AdoptCompositeFocus` (Photon) and the version half of
  `focusCompositeAfterRender` (web) go with it. Migration line: *replace `FocusVersion = ++_version` on a
  `Navigable` by a `FocusNode` the component holds, wrapped around the composite with `Focus`, and call
  `RequestFocus()` on it.*
- **The dashboard** gains a Focus screen (`/focus`) whose search field and results move the focus from code
  (Arrow Down from the search to the first result, Escape back, Next and Previous from a toolbar that takes
  no focus), proved in Chromium and through the Photon host.

## Capabilities

### New Capabilities

- `focus-traversal`: the focus object, its tree, the manager's reading-order traversal and requests, the
  `Focus` and `FocusScope` nodes, and their realization on the web and on Photon.

### Modified Capabilities

- `modal-focus`: "A modal layer holds the Tab walk on every target" is now stated as a closed-loop scope of
  the one traversal.
- `navigable-composites`: "A composite takes the keyboard back on request" is now a request on a held
  `FocusNode` instead of a version.

## Impact

- **Primitives** (`eQuantic.UI.Primitives`): the `Focus/` folder, two nodes and their visitor members, node
  kinds and coverage registrations; `Navigable.FocusVersion` removed. Its unshipped file of this branch
  (`PublicAPI.Unshipped.feat-a-component-holds-its-focus.txt`) moves: the additions, and `*REMOVED*` for
  `Navigable.FocusVersion.get`/`.init`.
- **Components** (`eQuantic.UI.Components`): `UI.Focus`, `UI.FocusScope` factories; `TreeView` holds a
  `FocusNode`. Its unshipped file of this branch moves (the two factories).
- **Runtime** (`eQuantic.UI.Runtime`): the transpiled twins under `shared/components/`, the vocabulary
  mirrors in `shared/vocabulary.ts` and `shared/nodes.ts`, the two lowerings in `shared/lowering.ts`,
  `dom/focus-manager.ts` (new) and `dom/focus-trap.ts` (its Tab handler and selector move out).
- **Web realizer** (`eQuantic.UI.Web`): the C# lowering of `Focus` and `FocusScope` beside the TypeScript
  one (`WebLoweringVisitor`); its unshipped file moves only if the visitor's members are public.
- **Photon** (`eQuantic.UI.Native.Components`): `FocusStop` carries its focus chain; `PhotonHost` drives one
  `FocusManager`; `MoveFocus`, the trap's walk in it and `AdoptCompositeFocus` fold in. `FocusStop` widens,
  so its experimental surface moves (`[EQX0001]` lines in its unshipped file). No shell changes: the macOS
  accessibility bridge keeps reading `FocusedId`.
- **eqc**: nothing new is expected; a construct the twins need and eqc refuses is filed and fixed
  (PRINCIPLES 14).
- **Developer surface** (`developer-surface.baseline.txt`): does not move (no MSBuild property, setting or
  template parameter).
- **Docs**: the `FocusNode` row of `docs/FLUTTER-PARITY.md` is rewritten (it stays PARTIAL until slice 4,
  naming what is left), the wiki's Components page in every language, a ledger entry citing #1565.

## Principles

Follows 4 (one manager, written once, transpiled; one fixture holds both targets), 5 (Flutter's neutral
names; no DOM word), 6 (Flutter's three trees answer where the focus tree lives, design.md), 9 (the
structural fix: one traversal instead of three walks; the fixture fails), 13 (`Navigable.FocusVersion`
goes, no forwarder), 15 (`Focus` and `FocusScope` are nodes around any child, never a flag on a
component). Bends none; slice 1 leaves the overlaps of slices 3 and 4 in place for one release at most,
each named in design.md with the slice that removes it.
