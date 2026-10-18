# Tasks

## 1. Measured before

- [x] 1.1 `PerfHarnessTests` gains a frame after a press on the stateful dashboard, measured on main's
  code with the other scenes, the numbers in design.md

## 2. The engine

- [x] 2.1 `Element.Id`, assigned at mount from the owner's count; `NodeElement.PartId`, from the same
  count and kept on the position; `ComponentElement.MarkNeedsBuild` public
- [x] 2.2 Engine tests: ids are unique in a tree, kept across a rebuild, new for a node at another's
  place; a part keeps its id while its position lives

## 3. The host addresses elements

- [x] 3.1 The regions and `FocusStop` carry the element (and a stop its id); a linked run is named by
  its paragraph's position and its part id; the live region mark carries the element
- [x] 3.2 `PressScope` and the paint walk track the press, the focus, the field and the hover by
  element and id
- [x] 3.3 `PhotonHost`: the press, the focus, the field being edited, the hover chain and the canvas by
  element and id; containment by walking parents; the text input session; `FocusedId`, `Activate`,
  `Adjust`, `Focus` with the element
- [x] 3.4 The announcer's memory and the code reveal as state on their elements
- [x] 3.5 `SemanticNode.Id`; the semantics walk names nodes and linked runs by id; the macOS, iOS and
  Android bridges, and Android's input connection, by id
- [x] 3.6 Check: the native suite, the goldens unchanged, the solution with the shells builds

## 4. The path goes

- [x] 4.1 The measure walk carries the element alone; `Position`, `LayoutNode.Path`, the path cache,
  the probe path and the realizer's layer names go
- [x] 4.2 Check: the native suite and the goldens

## 5. A frame after input builds what it reached

- [x] 5.1 Input asks for a frame that builds only what is marked; input routed into a field, a code
  surface or a spreadsheet marks the nearest stateful component above it; invalidation, resize,
  density and hot reload rebuild everything
- [x] 5.2 Host tests, each failing on main: a press builds the pressed card alone; a key typed into a
  code editor builds the editor alone; focus, a press and a hover do not pass to a node that takes
  their control's place; and one that passes on both: a keyed row keeps its focus when a row is
  inserted before it
- [x] 5.3 Check: the native suite; `PhotonDesktop`'s gallery pictures, pixel for pixel against main;
  the frame after a press measured, its ceiling set

## 6. Documentation

- [x] 6.1 `docs/ELEMENT-TREE-PLAN.md`: E2 complete, what it measured, the non-goals recorded for E3
- [x] 6.2 `docs/FLUTTER-PARITY.md` rows (the Element, `SemanticsNode`), and the docs and comments that
  describe paths as identity
- [x] 6.3 The wiki pages that describe identity by path (Photon, Styling), in their five languages, on
  the wiki branch named like this one
- [x] 6.4 `./scripts/public-api.sh update`, the diff read
- [x] 6.5 One ledger entry under `docs/ledger/`, citing #976
- [x] 6.6 Check: `./scripts/check-openspec.sh`, `DocsIndexTests`, the wiki guards
