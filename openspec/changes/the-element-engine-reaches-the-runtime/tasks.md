## 1. Measure first

- [ ] 1.1 One element-tree case through the conformance harness, the engine runtime-provided: that the harness reaches the transpiled `BuildOwner` at all, before any case is written.
- [ ] 1.2 The baselines this slice moves, on main: the served runtime's size (`served-runtime.json`), and Photon's frame on the dense scene and the stateful dashboard.
- [ ] 1.3 Check: the probe (the engine through eqc, `[ServerOnly]` lifted) reports exactly the four refusals the proposal lists.

## 2. Close what eqc refuses

- [ ] 2.1 `ChildSlots` as one switch over `NodeKind`, a typed read per arm, a default that throws naming the kind.
- [ ] 2.2 The executing coverage test: every concrete `VisualNode` in Primitives built by `FactoryArguments` and mounted through `BuildOwner`, the unbuildable ones in a baseline that only shrinks. A/B: an arm removed fails it by name.
- [ ] 2.3 `BuildOwner` handed how a component builds; `ComponentElement` asks the owner; the wiring to `ComponentBoundary` beside it, `[ServerOnly]`, used by Photon and the server; `PublicAPI.Unshipped.txt`.
- [ ] 2.4 `NodeElement`'s message names `NodeKind`.
- [ ] 2.5 Check: the probe reports no diagnostic; the native suites, goldens and the web and server suites unchanged; Photon's frame measured against 1.2.

## 3. Into the runtime

- [ ] 3.1 `Primitives/Elements` leaves `[ServerOnly]` and joins `SharedComponentTranspilationTests`' model sources; the modules regenerated (`EQ_UPDATE_TRANSPILED=1`), read, and committed.
- [ ] 3.2 The served-runtime budget measured, and moved with its number in the pull request.
- [ ] 3.3 The cases `ElementTreeTests` holds the C# engine to, written once as conformance cases and executed on both sides.
- [ ] 3.4 Check: Compiler and Conformance through the suite lock with `--blame-hang`, the runtime's suite (`-t:TestRuntime`), and the samples built.

## 4. Documentation

- [ ] 4.1 `docs/ELEMENT-TREE-PLAN.md`, slice E3: E3a landed, with what it measured and any deviation, as E1 and E2 record theirs.
- [ ] 4.2 The wiki, in every language a page is written in, where a page says where the engine runs.
- [ ] 4.3 One ledger entry under `docs/ledger/`, citing #1127.
