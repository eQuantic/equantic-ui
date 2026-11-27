## 1. The vocabulary

- [ ] 1.1 `CustomMultiChildLayout`, `LayoutId`, `MultiChildLayoutDelegate` (`GetSize`, `PerformLayout`, `Layout`, `LayoutChild`, `PositionChild`, `HasChild`, `ShouldRelayout`), `CustomSingleChildLayout` and `SingleChildLayoutDelegate` in `Primitives`, with their visitor methods, node kinds and factories
- [ ] 1.2 The refusals: a child laid out twice, an unknown or duplicated id, an infinite size on an unbounded axis, each with a message naming the delegate

## 2. Photon

- [ ] 2.1 The measure doors: the room as `FillingRoom`, the delegate bound to the children for one `Layout`, each child measured under the bounds it is given and placed at its offset
- [ ] 2.2 Emit, hit-testing and semantics follow the placed children; the reading order is the children's
- [ ] 2.3 Prove it: a radial and a masonry layout pinned to exact boxes, each refusal, and a test that fails without the door

## 3. The browser and the server

- [ ] 3.1 The runtime's twins of the nodes and of the two delegate bases, the server's lowering (children in wrappers at the origin, the box hidden) and the browser's (the same markup, hydrated)
- [ ] 3.2 The commit after the pass: the room read as a builder's, the delegate run, each child bounded, measured and placed, the box sized on its content-sized axes and revealed; observers that run it again on a resize
- [ ] 3.3 eqc translates an app's delegate class, its protected calls and its overrides; a conformance case runs a delegate on both sides
- [ ] 3.4 Prove it: the same two layouts pinned to the same boxes in a browser (e2e), the server's markup, hydration keeping the children, and a resize laying out again

## 4. Email, samples and parity

- [ ] 4.1 Email refuses both nodes
- [ ] 4.2 The dashboard's layout page and the Photon gallery lay out a masonry grid and a radial menu; the README captures they appear in are taken again
- [ ] 4.3 `docs/FLUTTER-PARITY.md`'s two rows read SAME, the widening stated; the parity pins follow

## 5. Documentation

- [ ] 5.1 The wiki's layout page in the five languages, on the wiki branch named like this one
- [ ] 5.2 One ledger entry under `docs/ledger/`, citing #1556

## 6. Proof against the real thing

- [ ] 6.1 The full local suites, the runtime's `TestRuntime`, the dashboard sample in a browser and the desktop gallery on Photon, each showing the two layouts
- [ ] 6.2 Archive with `openspec archive a-component-lays-out-its-children-in-csharp --yes` before the merge
