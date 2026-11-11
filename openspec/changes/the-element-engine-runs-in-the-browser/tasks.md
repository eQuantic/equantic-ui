## 1. The engine in the runtime

- [x] 1.1 `Primitives/Elements` leaves `[ServerOnly]`, joins the model sources, and its modules are generated and pinned.
- [x] 1.2 The browser's twins: `mountedAt` and taking and leaving a position on the stateful component, `SetState` marking positions, `AdoptConfig`'s default on `UiComponent`, `InFlow.current`, `AdaptiveNode.resolve`, a child slot typed `VisualNode`.
- [x] 1.3 `Place`/`Leave` become `EnterPosition`/`LeavePosition`; eqc's list of runtime members regenerated; a case requires EQ2011 for an app method of either name, failing with the old list.
- [x] 1.4 A library module extending a sibling imports it from its module.
- [ ] 1.5 The runtime's `tsc` accepts the modules once #1396 lands.

## 2. Proof

- [x] 2.1 `ElementEngineTwinTests`: eleven cases on both engines, traces equal.
- [ ] 2.2 The served runtime's budget measured and recorded with its number.
- [ ] 2.3 The runtime's suite, and the native, web, server, Compiler and Conformance suites through the suite lock.

## 3. Documentation

- [x] 3.1 `docs/ELEMENT-TREE-PLAN.md`, E3a: E3a landed.
- [x] 3.2 One ledger entry under `docs/ledger/`, citing #1301.
