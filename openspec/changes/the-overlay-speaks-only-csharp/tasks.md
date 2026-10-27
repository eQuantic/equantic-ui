# Tasks

## 1. The runtime carries a map to the SDK's C#

- [ ] 1.1 `SourceMapComposer.ComposeFile` composes a map on disk with the maps beside its sources, optionally naming every source from a folder and carrying no text; `ModuleBundler` uses it. Verify: `SourceMapComposerTests`
- [ ] 1.2 `eqc compose-map <map> --names-from <dir>`. Verify: a test runs eqc's verb over a map on disk
- [ ] 1.3 `SharedComponentTranspilationTests` writes and pins `<Name>.ts.map` beside each runtime module. Verify: the pin fails on a stale map, and passes regenerated
- [ ] 1.4 `BundleRuntime` writes and composes `wwwroot/runtime.js.map`; the Server embeds it and answers it in Development alone. Verify: a Server test reads the embedded map at a position of `SheetController`'s code and lands in `eQuantic.UI.Spreadsheets/SheetController.cs`, and the route answers 404 outside Development

## 2. The overlay speaks only C#

- [ ] 2.1 Name every frame by its owner and place, keeping the generated URL through the remap. Verify: `frame-owner.spec.ts`
- [ ] 2.2 Render no JavaScript: the stack appears once named, a failed remap names frames without maps. Verify: `error-overlay.spec.ts`
- [ ] 2.3 Title by what was raised, word an unobserved asynchronous exception in C#'s terms, ignore the ResizeObserver notice. Verify: `error-overlay.spec.ts`
- [ ] 2.4 Say when the fault is the SDK's and offer the report, with the SDK's version from the configuration in Development. Verify: `error-overlay.spec.ts`, and a Server test that the version reaches the configuration in Development only
- [ ] 2.5 The served runtime's budget. Verify: `ServedRuntimeBudgetTests`, the record moved only by what the overlay's code adds

## 3. Proof against the real thing

- [ ] 3.1 On `samples/DefaultUIDashboard` in a browser, in Development: an exception thrown in a page's C#, one inside a transpiled SDK component, one inside the hand-written runtime, and a window resize

## 4. Documentation

- [ ] 4.1 The wiki's Debug page in its five languages, on the pull request's wiki branch, and a ledger entry under `docs/ledger/` citing #1108
