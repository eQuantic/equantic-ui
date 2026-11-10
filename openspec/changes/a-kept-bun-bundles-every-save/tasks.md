## 1. The kept bundler

- [x] 1.1 `BundleWorker.js`: one request a line, `Bun.build` in memory with `bun build`'s naming, a file written only when it changed, a map's sources named from the folder it is written to
- [x] 1.2 `ModuleBundler` an instance: the worker started by the first bundle and handed every later one, restarted after it dies; the prune by what the bundle produced; only the maps written composed
- [x] 1.3 `AppBuild` takes the bundler a caller keeps, and starts and stops its own otherwise; the served eqc keeps one for its life
- [x] 1.4 Proof: `AKeptBundlerTests`, through bun, a test per scenario, each failing without its mechanism; the bundler's map, folder and stack-frame tests pass on the instance

## 2. Measurement and docs

- [ ] 2.1 `scripts/measure-edit-to-view.cs web` before and after on the dashboard sample; the web's ceiling in `scripts/edit-to-view.budget.json` lowered by what it measured
- [x] 2.2 A ledger entry under `docs/ledger/` citing #1280

## 3. Close

- [ ] 3.1 `openspec validate --all --strict`, the Compiler and Conformance suites through the lock, the solution built with no warning, the samples built
- [ ] 3.2 `openspec archive a-kept-bun-bundles-every-save --yes` before the merge
