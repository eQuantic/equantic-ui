## 1. The web realizer lowers a frame of a live tree

- [x] 1.1 `ElementFrame`: every position of a built tree by the node it holds, through `ChildAt`, and the layer it shows
- [x] 1.2 The lowering visitor takes the frame as a field: a component lowers from what its position built, at the component visit and at a stack's positioning, and a layer is shown before it lowers
- [x] 1.3 An adaptive node's arm the element does not hold lowers as a tree drawn once
- [x] 1.4 `WebRealizer.LowerFrame`, declared in the Web project's `PublicAPI.Unshipped.txt`
- [x] 1.5 Proof: the first frame of every component parity case at every size class is `Lower`'s markup save the unmeasured mark; a nested counter keeps its count across frames and the server's lowering draws it at zero; a layer shown keeps its instance, a closed panel unmounts at the frame's end, and a component with no position is refused. Each half of the mechanism dropped in turn fails its own test

## 2. The package

- [ ] 2.1 `src/eQuantic.UI.Testing`, a `contract` package on Primitives, Components and Web, with no test framework dependency, in the solution
- [ ] 2.2 `ComponentTester`: the theme, culture, width, density and services; `Pump(root)`, `Pump()`, `HasScheduledFrame`, `Dispose`, through a live `BuildOwner` and `LowerFrame`, lowered without a style sink
- [ ] 2.3 The semantics reader: `Announcement`, the role, name and state rules, and what is hidden; `AdaptiveGates.Shows` in the Web project
- [ ] 2.4 `Find`, `Tap` and `EnterText`, failing with what is announced
- [ ] 2.5 `PumpPageAsync`: the prefetch rounds over the kept instances
- [ ] 2.6 Proof: `tests/eQuantic.UI.Testing.Tests`, which reaches only the public surface, holds a test per scenario of the `component-testing` spec, the three documented shapes among them (a stateless card, a stateful form, a page with a prefetch), each A/B'd against the mechanism it proves

## 3. The SDK's own suites read through it

- [ ] 3.1 The web tests that walk the lowered tree by hand to read a role, a name or a state read `Semantics` where they hold the same shape
- [ ] 3.2 Proof: the Web suite passes, and each converted test fails when the attribute it reads is removed from the realizer

## 4. Documentation

- [ ] 4.1 A `Testing` page on the wiki, in English and in its Portuguese, Spanish, French and German twins, on a wiki branch named like the pull request's, showing the three shapes as the tests write them
- [ ] 4.2 `docs/ELEMENT-TREE-PLAN.md`: the test host among the owners of 3.6, the prefetch rebuild of the tester beside 3.7, and what E4 inherits from `LowerFrame`
- [ ] 4.3 A ledger entry under `docs/ledger/` citing #1245
- [ ] 4.4 Proof: the docs guards pass against the wiki branch (`EQ_WIKI_DIR` on a worktree of it)

## 5. Close

- [ ] 5.1 `openspec validate --all --strict`, the Compiler and Conformance suites through the lock, the solution built with no warning, the samples built
- [ ] 5.2 `openspec archive an-app-tests-a-component-without-a-browser --yes` before the merge
