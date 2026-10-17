# Tasks

## 1. The engine

- [ ] 1.1 `Element`, `ComponentElement`, `NodeElement` and `BuildOwner` in `eQuantic.UI.Primitives`
  (`Elements/`, one type per file, `[ServerOnly]`): mount, update by Flutter's `canUpdate`, keyed and
  indexed children as the paths match them, unmount, the dirty list built by depth, deferred slots
  swept at the end of a frame, unmounts flushed before mounts, a structural mode with no hooks
- [ ] 1.2 `ChildSlots`, the visitor that names each node kind's children by slot, with the arm of an
  `AdaptiveNode` and the deferred slots of `Overlay` and `Anchored`
- [ ] 1.3 `StatefulComponent` holds its element: `SetState` marks it dirty (the dispatcher marshalling
  kept), `StateInvalidated` removed, `NotifyMounted`/`NotifyUnmounted` internal;
  `ComponentInstanceStore` removed; the boundary's nesting bound read from the element's depth
- [ ] 1.4 Engine tests: retention across a rebuild, keyed rows that swap, a type change, unmount
  before mount, a structural build with no hooks, dirty elements built ancestors first, the bound

## 2. Photon lays out what was built

- [ ] 2.1 The measure state carries the position (path and element); every child path site forms the
  child's position; `LaidOutChildren` resolves arms for both
- [ ] 2.2 `MeasureComponent` and intrinsic sizing read the element's built subtree; the `InFlow`
  ambient moves to the build; a node measured with an element that does not hold it throws
- [ ] 2.3 `LayoutEngine.Layout` takes the root element, and builds one structurally when it is not
  handed one; `LayoutNode.Element`; `LayoutContext.Instances` removed
- [ ] 2.4 Check: the native suite and its goldens pass unchanged

## 3. The realizer and the host

- [ ] 3.1 `PhotonRealizer.Realize` takes a `BuildOwner`, runs the build phase before the page's
  layout, reconciles an overlay's child and an anchored panel when their layer is realized (the panel
  adopted inside the synthetic layer), and ends the frame on the owner
- [ ] 3.2 `PhotonHost` owns a live `BuildOwner`, listens to it once, lets it mount and unmount the
  root, and tears it down in `Unmount`
- [ ] 3.3 Frames drawn only for motion build only what is dirty; every other request for a frame
  rebuilds every component
- [ ] 3.4 Host tests: #935 (a hundred frames, one `SetState`, one request), a loop's frames build
  nothing, a `SetState` builds its component alone, an anchored panel's component mounts when it
  opens and unmounts when it closes, a closed overlay with motion unmounts its child, a grid's hug
  column read with the child's own state, a component built once by a box that measures twice
- [ ] 3.5 Check: the native suite, the goldens and `PerfHarnessTests`; the desktop gallery's README
  picture taken again and compared pixel for pixel; `PhotonDesktop` built and run

## 4. Measured

- [ ] 4.1 The stateful and dense scenes' pooled bytes per frame after the change, beside the baseline
  in design.md
- [ ] 4.2 What eqc refuses when it transpiles the engine's sources, recorded in design.md for E3

## 5. Documentation

- [ ] 5.1 `docs/ELEMENT-TREE-PLAN.md`: E1 done, and the deviations (`InheritedElement` with E5, the
  engine host-only until E3, input rebuilding everything until E2, deferred layer slots)
- [ ] 5.2 `docs/FLUTTER-PARITY.md` rows (`Element`, `setState`) and `FlutterParityPinTests` flipped
  in the same commit; the audit and the plans that describe the store or `StateInvalidated`
- [ ] 5.3 The wiki pages that describe a component's lifecycle on Photon, in their five languages, on
  the wiki branch named like this one
- [ ] 5.4 `./scripts/public-api.sh update`, the diff read: the store, `StateInvalidated` and the two
  `Notify` members as `*REMOVED*` lines, the engine as additions
- [ ] 5.5 One ledger entry under `docs/ledger/`, citing #950 and #935
- [ ] 5.6 Check: `./scripts/check-openspec.sh`, `DocsIndexTests`, the wiki guards
