# Tasks

## 1. The vocabulary says which value is data

- [x] 1.1 Add the attribute (working name `PlainDataTwin(string reason)`) to `eQuantic.UI.Primitives/Contracts`, put it on `Color` with its reason, and declare it in `PublicAPI.Unshipped.txt`. Verify: `dotnet build src/eQuantic.UI.Primitives` passes with no RS0016
- [x] 1.2 Give eqc one predicate for it, read by symbol, beside `IsRuntimeProvided` in `TypeSymbolExtensions`. Verify: a compiler test answers yes for `eQuantic.UI.Primitives.Color` and no for an app's own `Color` record
- [x] 1.3 Check it against the runtime: a conformance test loads the runtime bundle and asserts each vocabulary value type's export is an object exactly when the attribute marks it. Verify: the test passes, and fails when the attribute is removed from `Color`

## 2. Every member lowers through the rule

- [x] 2.1 Lower an instance method of a marked type to the companion's static with the value first, a method group included as an arrow over the same call. Verify: a compiler test pins `Color.withOpacity(value, …)` and `Color.midpointWith(value, other)`, and fails against `main`'s strategy
- [x] 2.2 Write a marked value's text through `ValueFlow`'s text rule and `ToString()` to a runtime helper that writes .NET's record text, with its vitest. Verify: the runtime test prints `Color { R = 1, G = 2, B = 3, A = 4 }`, and `dotnet build src/eQuantic.UI.Runtime -t:TestRuntime` passes
- [x] 2.3 Build a marked type's construction from its positional members by symbol, and remove the name test from `ObjectCreationStrategy`. Verify: a compiler test builds `new Color(1, 2, 3, 4)` as `{ r: 1, g: 2, b: 3, a: 4 }` and an app's own `Color` record as the app's class
- [x] 2.4 Run the conformance suite over the marked types: every public instance method and the text, built from sample values, executed on both sides, with the scenarios of the spec as named cases. Verify: the suite passes, and the `WithOpacity` and `MidpointWith` cases fail against `main`
- [x] 2.5 Prove it in a browser: a screen of the dashboard sample draws `Color.FromRgb(0xF8, 0x71, 0x71).WithOpacity(0.8f)` and its text, reached by a client navigation. Verify: the colour and the text match the server's, and on `main` the same steps throw

## 3. Documentation and the pull request

- [x] 3.1 Update the wiki page that describes how vocabulary values reach the browser, in English and Portuguese, in one commit on a wiki branch named like this one. Verify: the docs guards pass with `EQ_WIKI_DIR` on that branch
- [x] 3.2 Add one `docs/LEDGER.md` line citing #494, and file the `GetHashCode` gap as its own issue under #164. Verify: `./scripts/check-openspec.sh` passes and the issue is on the board
- [ ] 3.3 Run the suites each alone and read their exit codes (Compiler, Web, Server, Conformance, the runtime's `TestRuntime`), and `dotnet build samples/DefaultUIDashboard`. Verify: every one exits 0
