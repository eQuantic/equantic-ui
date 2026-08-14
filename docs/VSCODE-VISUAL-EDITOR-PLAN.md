# Track E — VS Code extension: the visual editor

> Plan doc for the track the ROADMAP opened on 2026-07-31. Started 2026-08-14.
> Every claim carrying a `file:line` was opened and read; every number was measured on this machine.

## What this is

A first-party VS Code extension that turns the SDK into a **visual** development environment: live
preview of a screen, click a rendered element to select the C# that produced it, a property panel
that edits that C#, and eventually insert/move/delete from a component palette.

Zero third-party by construction. The preview is the **real web realizer** running the **real
compiled module** in a webview — never a lookalike. Nothing new renders pixels.

---

## The premise the ROADMAP had, and what is actually true

The ROADMAP's Track E entry says click-to-select would work "via the V3 source maps eqc already
emits". **That path is dead**, and it was worth finding out before building on it.

The whole `Build` body is converted to one flat string and emitted through a single call —
`c.Raw(jsBody, component.BuildMethodNode.Body)` at `TypeScriptEmitter.cs:252` (and `:387`, `:949`).
So the finest position a source map can name is *the start of the method body*. Measured:
`__transpiled__/Badge.ts` line 26 is **942 characters**. Teaching ~150 conversion strategies to emit
newlines would take weeks and still only reach a statement, never an element.

Two other premises checked out and one more did not:

| Premise | Verdict |
|---|---|
| The compiler can be driven in-process, from a buffer | ✅ `ComponentCompiler.CompileSource(text, path)`, `ComponentCompiler.cs:148` |
| A live compile loop is fast enough | ✅ **measured p50 293 ms** on the 662-line `PaymentsPage`, warm |
| Source maps give element identity | ❌ dead, see above |
| Real screens are declarative trees a canvas can represent | ❌ **they are imperative** — see the open decision below |

### Real screens are imperative, and that caps what a canvas can do

| Screen | lines | `new X(` | `.Add(` | `children:` |
|---|---:|---:|---:|---:|
| `samples/WalletMobile/WalletApp.cs` | 686 | 208 | 126 | **0** |
| `samples/PhotonDesktop/Studio/Gallery.cs` | 1070 | 206 | 171 | **0** |

Repo-wide, only `samples/DefaultUIDashboard/Screens/DeclarativeScreen.cs` and two template files use
the declarative `children:` form the docs teach. The authoring surface the framework advertises is
**not** the one its own screens are written in.

This is a product question, not an implementation detail, and it is the one open decision on this
track — see [The open decision](#the-open-decision).

---

## Architecture

```
VS Code extension (TypeScript)              eqdesign (.NET, long-lived)
──────────────────────────────              ───────────────────────────
extension.ts   activation, commands  ──┐
preview.ts     webview + host doc      │   newline-delimited JSON over stdio
sidecar.ts     JSON-RPC client       ──┴──▶ Program.cs      protocol loop
project.ts     find csproj/refs/runtime    DesignSession.cs the project compilation
                                            └─ eQuantic.UI.Compiler (as a library)
```

**Why a sidecar and not `eqc`.** `eqc` reads files and `HotReloadService` watches the filesystem, so
neither can show the text you are currently looking at — only the last text you saved. `CompileSource`
takes a string, and `SemanticModelProvider.GetSemanticModel` swaps the buffer's tree in for the file's
own by path (`SemanticModelProvider.cs:84-93`), so an unsaved edit compiles against the real project:
its other types, its global usings, its generated sources, its exact MSBuild reference set. `eqc`'s
own `--watch` also builds its compilation once before the loop and never refreshes it.

**Why stdio.** No port to choose, nothing to authorise, nothing left listening if the window dies, and
it works unchanged over Remote-SSH because the extension host runs on the remote machine anyway. The
host's stdout is the protocol and nothing else — `Console.Out` is redirected to stderr at startup
(`Program.cs`), because one stray line of prose desynchronises the reader for the whole session.

**Measured, on `samples/DefaultUIDashboard`:**

| | |
|---|---|
| `initialize` (16 files, 316 references) | **271 ms**, once |
| `compile`, cold (first, JIT) | 2226 ms |
| `compile`, warm — edit loop, distinct buffer each time | **p50 293 ms** · min 206 · max 560 |
| `diagnose` (bind only, no transpile) | **36 ms** |
| syntax error → refusal with a mark | **36 ms** |

Two cadences follow from those numbers: `diagnose` on a 150 ms debounce (feels instant, fills the
Problems panel), `compile` on 400 ms.

---

## Phases

### Phase 0 — the landmines ✅ done

Small, boring, and everything after it is wrong without it.

- **`equantic.refs.txt` truncated to 0 bytes on every hot-reload save.** `CompileEQuanticUI` consumes
  `@(ReferencePathWithRefAssemblies)`, which is produced by `FindReferenceAssembliesForReferences` and
  **not** by `ResolveReferences`. In a full build the target runs after `Compile` so the item is
  populated; invoked on its own — exactly what `HotReloadService.cs:61` runs — it was empty, and
  `Overwrite="true"` truncated a good 316-line file. eqc then fell back to a bin scan, lost the
  semantic model, and **passed named arguments through in syntactic order**: a component rendered
  with its values in the wrong slots, with no error anywhere. Reproduced 316 lines → 0 bytes, fixed
  by the dependency, seat-belted by a non-empty condition, and eqc now **fails** (EQ0002) rather than
  degrading silently.
- **Converter state never dropped between files.** `ConversionContext._cache` is keyed by `SyntaxNode`,
  so each entry keeps a whole syntax tree reachable. Measured over 200 compiles: **38.4 KB retained
  per compile before, 4.7 KB after**. Invisible in a CLI that exits; not survivable in a host that
  stays up all day.
- **`CodeWriter.CurrentLine` counted one line per call**, whatever it was handed. It feeds
  `GeneratedLine` in the source map (`TypeScriptCodeBuilder.cs:80`), so any multi-line write shifted
  every mapping after it.
- **The extension's snippets taught an API that does not exist** — `Container`, `ComponentTree`,
  `Style { BackgroundColor = "white" }`: zero hits for any of them in `src/`. Rewritten against the
  real surface.

### Phase 1 — the design host and a live preview ✅ done

`src/eQuantic.UI.Design` (`eqdesign`) + `extensions/vscode`. Compiles the **unsaved buffer**, holds
the last good render when an edit does not compile, and reports C# errors as `vscode.Diagnostic`.

Proved end-to-end in a real browser, through the same code path the webview uses (compiled module →
blob → dynamic import → `materializeTheme` → `mount`):

- `DeclarativeScreen` mounts, paints with real atomic classes, and **is interactive** — clicking `Up`
  runs `SetState` through the reconciler and the text becomes `Count: 1`.
- `PaymentsPage` — 662 lines, imperative, with dependencies — renders **complete**: sidebar, KPI
  cards, bar chart, activity feed, 1965 DOM nodes, no console errors.

**Three real bugs surfaced by doing it**, all in the plain-JavaScript emission path that the
playground, the conformance harness and this host all consume (in TypeScript mode a stray annotation
is stripped by the bundler; in plain mode the browser rejects the module at parse time, so *nothing*
runs and the only symptom is an empty frame):

1. Lazy-static backing slots emitted `static _x: T | undefined;` — `Raw` output that never asked
   about `TypeAnnotations`. Seven sibling sites had the same defect (`abstract`/`declare` members,
   annotated getters and setters).
2. `abstract class X {` emitted for plain JavaScript (`TypeScriptCodeBuilder.cs:46`).
3. Hoisted pattern variables emitted `let x: any;` (`PatternVariableScanner.cs:34`). The flag now has
   **no default** at that call site, so the next caller has to decide rather than get it wrong quietly.

Guarded by `PlainJavaScriptSyntaxTests`, where the TypeScript-mode test asserts each path is actually
exercised — so no guard can pass vacuously.

**Not yet verified:** the extension running inside a real VS Code Extension Development Host. The
render path, the protocol and the latency are proven; the webview wiring is written and type-checks
but has not been launched.

### Phase 2 — `VisualNode.Origin` (the pivotal phase) ✅ done

Identity that is **exact**, not correlated, and target-neutral so native inherits it.

1. `VisualNode.Origin` — `string?`, beside `Key`. A string and not a file/span type: this layer speaks
   no target's language, and each realizer carries an opaque string into its own world.
2. `ComponentCompiler.DesignMode`, beside `TypeAnnotations`. The stamp is applied at ONE hook —
   `CSharpToJsConverter.ConvertExpression`, where every strategy's result comes back — restricted to
   *construction* expressions (`new X(…)`, `X(…)`) whose type derives from `VisualNode`. A reference
   is never stamped: `column` where it is merely mentioned would overwrite the construction's origin
   with the span of a use, and the editor would select the wrong line.
3. `$eq.origin(node, "path|startLine:startCol|endLine:endCol")` sets the field and returns the node,
   so the wrapper never changes what an expression means.
4. `WebRealizer.LowerNode` (C#) and `lowerNode` (TS) each attach `data-eq-origin` at their single
   dispatch — one place rather than thirty branches, and symmetric by construction.
5. Native gets it free: `LayoutNode.Source` is the `VisualNode`.

Loops need no special case: a row built five hundred times in a `foreach` carries the span of the one
expression that built it — which is the only editable thing anyway.

**Proved:**
- Every origin, applied back to the source text, lands on the construction it was stamped on
  (`DesignOriginTests`) — the contract click-to-select rests on, and the one that anything merely
  *correlated* would fail on the first loop.
- A `foreach`-built node is stamped with the loop body's expression, by line number.
- Both realizers pinned in their own words: `DesignOriginAttributeTests` (C#) and
  `design-origin.spec.ts` (TS) — same attribute name, same "only when set" rule.
- **Design mode off leaves production untouched**: a real SSR render of `PaymentsPage` contains
  **zero** `data-eq-origin`, and `ssr-hydration.spec.ts` — which fingerprints every element's sorted
  classes across SSR and hydration — is green.
- End to end in a browser: 7 of 13 elements of the rendered `DeclarativeScreen` carry an origin, and
  the spans resolve to the `Box(`, the `Column(` and each `Text(` that made them. The 6 without one
  are DOM the components generate internally — which is exactly why Phase 3 walks UP to the nearest
  origin-bearing ancestor rather than assuming 1:1.

**Found while doing it:** `ssr-hydration.spec.ts` had been failing since `ca34dfb` on an assertion
nothing had updated — `SegmentedControl` deliberately became a radiogroup (`role="radio"` +
`aria-checked`, one Tab stop), and the test still looked for `getByRole('button')` + `aria-pressed`.
An explicit role overrides the implicit one, so the locator matched nothing. Updated to the contract
the component actually promises.

### Phase 3 — click-to-select

`postMessage` bridge, gated to design mode. Walk up to the nearest origin-bearing ancestor — a
`Button` is `button > div > div > span`, so 1:1 DOM↔node is wrong. Classify each node by asking
Roslyn whether its origin has a loop/conditional/lambda ancestor **within** the Build method:

| Tier | Canvas affordance |
|---|---|
| **Literal** — unconditional construction | select · inspect · edit · insert beside |
| **Derived** — inside a loop or conditional | select · inspect · edit the source expression · no structural insert |
| **Foreign** — another method or file | select · "defined in `EntryRow()` → jump" |

**Exit:** an automated test clicks ≥20 distinct elements across `PaymentsPage` and `DeclarativeScreen`
and asserts each resolved span's source text contains the expected constructor or factory name.

### Phase 4 — read-only inspector

A generated `components.manifest.json`, byte-pinned by a test the way `design-system.generated.ts`
already is. Sourced from Roslyn over the component **sources** the SDK already ships at
`tools/source/*.cs` — which is why `GenerateDocumentationFile` is *not* needed: doc comments, parameter
names and defaults all come off the source symbols, richer than an XML file would be.

Seed the categories from `Gallery.cs:44-80` — the only human-decided component taxonomy in the repo.
Close the **22 components with no factory** (Accordion, AppBar, BottomSheet, Breadcrumb, CodeBlock,
CodeEditor, DataTable, FormInput, FormSubmit, List, ListItem, ListView, Menu, PageIndicator,
Pagination, Popover, PullToRefresh, RadioGroup, SegmentedControl, Spreadsheet, SwipeableRow, Table),
so the palette does not emit two different insertion forms depending on what you drag.

### Phase 5 — property write-back, fenced

`DocumentEditor.ReplaceNode`, one `WorkspaceEdit` per gesture so one Ctrl+Z reverses it and it lands
in the document's own undo stack. Never `fs.writeFile`.

`Microsoft.CodeAnalysis.Workspaces.Common` is already on the restore graph via
`eQuantic.UI.Compiler.csproj:13`, so this needs no new dependency decision.

**`BoxStyle` first**: it is a `readonly record struct` with no positional parameters, so every
appearance edit is a pure initializer-member add or replace with no ordinal arithmetic.

**No form transformation in v1.** Setting `Width` on `Column(gap: 12, children: [...])` is impossible
without rewriting the call into `new Column(...) { Width = ... }` — a different authoring form the
whole factory surface exists to avoid. The inspector shows such properties **disabled, with the
reason**. Honest beats clever.

**Exit:** applying a no-op edit to all sample screens produces byte-identical files.

### Phase 6 — insert from the palette, fenced

Insert only into a `children: [...]` collection expression — the one structurally uniform slot.
Refuse imperative `.Add()` bodies with a clear message rather than corrupting a method.

---

## The open decision

Phase 6's fence is worth almost nothing on today's code: **no real screen in this repo is written in
the declarative form it can edit.** Three ways out, and it is a product call:

1. **Hold the fence.** Drag-and-drop works on declarative screens only. Safe, and nearly unusable on
   the existing codebase until screens are rewritten.
2. **Statement-level insertion into imperative bodies.** Covers 100 % of real screens, and is genuinely
   hard: which local is the parent, which statements mutate it, whether an insertion crosses a
   declaration it depends on. Getting it wrong corrupts a file rather than annoying someone.
3. **Normalise on touch.** The first structural edit converts the touched container to declarative
   form. Covers everything, at the cost of rewriting code the author did not ask to have rewritten.

Phases 2–5 are unaffected: selection, inspection and property editing all work identically either way.
The decision is only needed before Phase 6.

---

## Fences for v1

| Fence | Why |
|---|---|
| No structural editing of imperative screens | Statement-level dataflow; 85–90 % of real code, so wrong is catastrophic rather than annoying |
| No factory ⇄ `new`+initializer transformation | Changes the authoring form the factory surface exists to avoid — a product decision, not the editor's |
| No native/Photon preview | Identity is already solved there; only transport is missing, so nothing is foreclosed |
| No CSS/style panel | Style is typed `BoxStyle` with tokens; a CSS panel would teach the wrong model and never round-trip to native |
| No raw colour picker | Trees hold **tokens**, never resolved colours — that is the theming invariant |
| No framework `CodeEditor` in the webview | It has no IME/composition events, no paste handler and no SSR path. Use VS Code's own editor |
| No `[ServerAction]` execution in preview | Action ids come from a startup assembly scan, so an ad-hoc preview class is never registered |
| No marketplace publish yet | Needs a version-derivation decision and a publisher identity; ship the `.vsix` as a release asset |

## Known limitation

The preview renders under the framework **baseline** theme (`MaterialTheme.Instance`). An app picks
its theme by calling `AddUI(…).UseTheme(…)` at startup, and reading that back means running the app's
composition root. Shapes and layout are exact; a rebranded palette is not yet reflected.
