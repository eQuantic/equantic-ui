## 1. The component is a node

- [x] 1.1 `UiComponent` in the runtime between the web `Component` and the two kinds, `nodeKind` at run time, the node's members declared; the runtime exports it.
- [x] 1.2 The type test: `UiComponent` assignable to `VisualNode`, its kind a `NodeKind`. A/B: a member added to `VisualNode` fails `tsc` at the test.
- [x] 1.3 The lowering: a component without state takes the `component` arm; a bare lowering context builds it with the ambient one; the seam's comments say what is left for it.

## 2. Proof

- [x] 2.1 A mixed page names every component as the server does. A/B: passes with the old routing put back, fails when the arm stops naming a component without state.
- [x] 2.2 A client navigation onto a page with a nested component without state reads the new payload. A/B as 2.1.
- [x] 2.3 The boundary contains a real component without state and reports its name.
- [x] 2.4 eqc's list of runtime members regenerated (`EQ_UPDATE_RUNTIME_MEMBERS=1`), and the test that pinned the old routing rewritten to the new truth.
- [ ] 2.5 The runtime's suite and `tsc`; the server suite (DashboardDocumentTests, the served runtime's budget); the web suite; Compiler and Conformance through the suite lock.

## 3. Documentation

- [x] 3.1 `docs/ELEMENT-TREE-PLAN.md`, E3a: the slice and what it measured (its archive path takes the date the change is archived on).
- [x] 3.2 One ledger entry under `docs/ledger/`, citing #1301.
