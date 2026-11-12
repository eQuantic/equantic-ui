## 1. Measure before

- [x] 1.1 The e2e suite in a real browser on the slice's base (bc42a5648): 15 of 15
- [ ] 1.2 The runtime's suites (`tsc`, `vitest run`) on the base, as the baseline the slice keeps

## 2. The browser builds through the engine

- [x] 2.1 A frame of a built tree beside the lowering, with the C# frame's three questions and its refusal
- [x] 2.2 The pass holds a page's owners, one per root slot, with the build callback (boundary and server state)
- [x] 2.3 `lowerVisualNode`, the component arm and the stack's component child lower from the frame
- [x] 2.4 The adaptive node's arms and the deferred layers ask the frame where the C# visitor does
- [x] 2.5 A render the owner asked for lowers the page's last tree; any other builds the page and rebuilds all
- [x] 2.6 `EndFrame` at the pass's end, `UnmountAll` when a page leaves, and the store and the invalidation hook removed

## 3. Server data by position, and every arm a position (#1477)

- [x] 3.1 The engine records `Element.Slot`, reads `SlotsBelow`, and says which position is `Building`
- [x] 3.2 The web's owners hold every arm (`holdsEveryArm`); the frameless arm and the threshold listener go
- [x] 3.3 The browser adopts by `r{tree}/{type}@{slots}` on every build of a fresh instance
- [x] 3.4 The server names by the frame's key, draws a bridge through `MarkupHost.DrawOnce`, puts back before a build, and measures a page's keys below it (`ForPage`)
- [x] 3.5 The engine regenerated, a twin case for slots, and the payload's tests read keys by type

## 4. Proof

- [x] 4.1 The specs that asserted the store assert the same behaviour on elements, and the scenarios of this change have a spec each
- [x] 4.2 The runtime's suites, the element engine's twins and the hydration contract pass (with #1477 in: twins and hydration crossing 25 of 25, Server 625 of 625)
- [x] 4.3 The e2e suite in a real browser after the change: 15 of 15, as before it (with #1477 in)
- [ ] 4.4 The served runtime's budget, re-recorded in a commit of its own if it moved
- [x] 4.5 `docs/ELEMENT-TREE-PLAN.md`, the ledger and `FlutterParityPinTests` where a row closes (the `Element` row's instance-handling half; the row stays partial for E5b)
