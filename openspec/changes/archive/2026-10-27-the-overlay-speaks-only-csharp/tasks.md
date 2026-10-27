# Tasks

## 1. The runtime carries a map to the SDK's C#

- [x] 1.1 `SourceMapComposer.ComposeFile` composes a map on disk with the maps beside its sources, optionally naming every source from a folder and carrying no text; `ModuleBundler` uses it. Verify: `SourceMapComposerTests`
- [x] 1.2 `eqc compose-map <map> --names-from <dir>`. Verify: the Server's build runs it, and `RuntimeSourceMapTests` reads what it wrote
- [x] 1.3 `SharedComponentTranspilationTests` writes and pins `<Name>.ts.map` beside each runtime module. Verify: the pin fails on a stale map, and passes regenerated
- [x] 1.4 `BundleRuntime` writes and composes `wwwroot/runtime.js.map`; the Server embeds it and answers it in Development alone. Verify: a Server test reads the embedded map at a position of `SheetController`'s code and lands in `eQuantic.UI.Sheet/Editing/SheetController.cs` at the line the module's own map gives, and the route answers 404 outside Development

## 2. The overlay speaks only C#

- [x] 2.1 Name every frame by its owner and place, keeping the generated URL through the remap. Verify: `frame-owner.spec.ts`
- [x] 2.2 Render no JavaScript: the stack appears once named, a failed remap names frames without maps. Verify: `error-overlay.spec.ts`
- [x] 2.3 Title by what was raised, word an unobserved asynchronous exception in C#'s terms, ignore the ResizeObserver notice. Verify: `error-overlay.spec.ts`
- [x] 2.4 Say when the fault is the SDK's and offer the report, with the SDK's version from the configuration in Development. Verify: `error-overlay.spec.ts`, and a Server test that the version reaches the configuration in Development only
- [x] 2.5 The served runtime's budget. Verify: `ServedRuntimeBudgetTests`, inside the record without moving it

## 3. Proof against the real thing

- [x] 3.1 On `samples/DefaultUIDashboard` in a browser, in Development: an exception thrown in a page's C#, one inside a transpiled SDK component, one inside the hand-written runtime, and a window resize

## 4. Documentation

- [x] 4.1 The wiki's Debug page in its five languages, on the pull request's wiki branch, and a ledger entry under `docs/ledger/` citing #1108
