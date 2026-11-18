## 1. The value and the nodes

- [x] 1.1 `BoxConstraints` in `Primitives/Layout`, with Flutter's members; an unbounded maximum is `float.PositiveInfinity`
- [x] 1.2 `LayoutBuilder` in `Primitives/Nodes`; its visit on `IVisualNodeVisitor`; a deferred slot in `ChildSlots`
- [x] 1.3 `ConstrainedBox` in `Components`, a `Box` with the constraints' bounds; `LayoutBuilder` and `ConstrainedBox` factories in `UI.cs`; `./scripts/public-api.sh update`, additions only
- [x] 1.4 Check: the solution builds with zero warnings, and the vocabulary's enumerating tests (closed hierarchy, factories, child slots, Flutter parity, email refusal) name the new node

## 2. Photon

- [x] 2.1 `MeasureBox` takes the box's style and child; the `LayoutBuilder` door projects the constraints, builds when the node or the room changed, shows the slot every frame and measures the child as a filling box's
- [x] 2.2 The fill readings (`FillsOf`, `DeclaredSizeKind`, the intrinsic fill height) and the intrinsic widths answer for the node; emit and semantics pass through
- [x] 2.3 Check: layout tests on the engine (a fixed box, a column sized by content, a resize, the same room building nothing, a nested builder, components under the builder keeping their state across a rebuild), each failing without the behaviour it names

## 3. The web

- [x] 3.1 C#: the server writes the filling box with nothing in it
- [x] 3.2 TS: the `BoxConstraints` twin, cross-pinned by the primitive values fixture; the node's twin; node kinds and transpiled modules regenerated
- [x] 3.3 TS: the lowering writes the empty box until the position holds a measurement, then shows the builder's child in the slot; the pass's commit measures each marked box under size containment, observes its resizes, and marks the component above to build when the constraints changed
- [x] 3.4 Check: vitest drives a builder in a page through its first measurement, a narrower room, an unchanged one and another component building alone (#1575), each failing without containment or without the room's comparison; the server's box is the empty filling box the browser's first lowering writes, and the parity spec skips the client-only mark

## 4. Both targets, one answer

- [ ] 4.1 A dashboard sample page whose card lays its fields out by the room it has
- [ ] 4.2 Check: the e2e suite reads the constraints the builder was handed in Chromium at two widths, and a Photon test lays out the same tree at the same widths and asserts the same numbers

## 5. Docs

- [x] 5.1 `docs/FLUTTER-PARITY.md`: the `LayoutBuilder` and `BoxConstraints` rows read SAME, with their probes; the wiki's WriteOnceComponents page in all five languages; a ledger entry citing #1555

## 6. Archive

- [ ] 6.1 `./scripts/check-openspec.sh` passes, then `openspec archive a-component-builds-its-child-against-its-box --yes`
