## 1. The package

- [x] 1.1 `src/eQuantic.UI.Native.Testing`, an `experimental` package on the Photon host and the reference backend, with no test framework dependency, in the solution
- [x] 1.2 `PhotonTester`: the theme, culture, width, height, density, mode and type scale; `Pump(root)`, `Pump()`, `HasScheduledFrame`, `Dispose`, through a headless `PhotonHost` with the deterministic measurer
- [x] 1.3 `MeasuredNode`: the node, its box, its text's measurement and its children, copied from the frame; `Root` and `Layers`
- [x] 1.4 `Find(name)` and `Find(role, name)` through Photon's semantics tree, failing with what is announced
- [x] 1.5 `ReadPixelsSrgb()` through the reference backend
- [x] 1.6 The API declared in the project's `PublicAPI.Unshipped.txt`, with the `[EQX0001]` prefix
- [x] 1.7 Proof: `tests/eQuantic.UI.Native.Testing.Tests`, which opts in to EQX0001 as an app's test project would and reaches only the public surface, holds a test per scenario of this change, each A/B'd against the mechanism it proves

## 2. The SDK's own suites use it

- [x] 2.1 The wave-2 gallery painted through the package matches its golden, in the native suite
- [x] 2.2 `AssemblyLayeringTests` holds both testing packages to their references, and `docs/ARCHITECTURE-AUDIT.md` says why
- [x] 2.3 Proof: the native suite passes, the golden case fails when the package measures with another text measurer, and the layering case fails when the web half gains a native edge

## 3. Documentation

- [x] 3.1 The wiki's `Testing` page gains one example of a Photon measurement, in English and in its Portuguese, Spanish, French and German twins, on a wiki branch named like the pull request's
- [x] 3.2 A ledger entry under `docs/ledger/` citing #1246
- [ ] 3.3 Proof: the docs guards pass against the wiki branch (`EQ_WIKI_DIR` on a worktree of it)

## 4. Close

- [ ] 4.1 `openspec validate --all --strict`, the Compiler and Conformance suites through the lock, the solution built with no warning, the samples built
- [ ] 4.2 `openspec archive a-test-measures-a-component-as-photon-lays-it-out --yes` before the merge
