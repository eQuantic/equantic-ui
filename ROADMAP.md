# eQuantic.UI — Roadmap & Honest State Assessment

> Reviewed against the tree on 2026-10-14, at 0.2.0-preview.63: every row of the state below was
> measured that day, and none of them quotes a count that moves with each release. A count names
> where it lives (the suite, the board, the 1.0 epic), what shipped names the release or the issue
> that carries it, and what is ahead names its issue. This document is an evidence-based assessment
> of where eQuantic.UI stands against its vision, and a prioritized roadmap to close the gaps.

## The Vision

eQuantic.UI aims to be a **true 100% C# UI SDK**:

1. **Zero JavaScript knowledge required** — authors write C#; the SDK compiles to optimized JS.
2. **Performant, with Bun behind the scenes** — embedded Bun does the bundling; no Node/npm required of the user.
3. **Flutter-like authoring** — declarative `Build(context)` component model.
4. **Rich, ready-made components** — shadcn-like semantics and polish.
5. **Styling with no CSS to write** — typed C# styles over one first-party engine; a pluggable
   engine was weighed and declined (pillar 5).

## Current State (honest)

eQuantic.UI is in **preview**: its version carries a prerelease label (read `Directory.Build.props`),
and by rule a preview breaks its public surface whenever a shape is wrong (CLAUDE.md, "No member
survives to keep an old shape alive"), each break listed in the release's notes and on the wiki's
Upgrading page. The next milestone is **1.0, the web SDK stable with the native track experimental**
([#898](https://github.com/eQuantic/equantic-ui/issues/898)). What 1.0 promises is a contract rather
than features: an app written against it keeps compiling and keeps answering the same through the
1.x line. Its exit criteria, and the measurements they start from, live in the epic's body. When it is likely
to land is the board's Roadmap view, which `scripts/roadmap.py` forecasts from the pace the board
measures ([docs/ROADMAP-FORECAST.md](docs/ROADMAP-FORECAST.md)).

What exists, held by suites that fail rather than warn:

- **Write-once components**: a screen is authored once against the vocabulary
  (`eQuantic.UI.Primitives`) and realized as DOM and atomic CSS on the web, and as GPU pixels by
  Photon on macOS, iOS, Android and Windows.
- **eqc, the C#→JavaScript transpiler**: Roslyn, one strategy per C# construct, writing an IR with
  one writer per level. A differential conformance suite runs every case under .NET and under Bun,
  and what eqc cannot translate faithfully fails the build with a diagnostic instead of answering
  differently.
- **The web stack**: SSR with hydration, Server Actions with authorization, server events, a client
  router, forms and validation, localization through `.resx`, hot reload that keeps a page's state,
  and C# in the browser's debugger.
- **The SDK**: `<Project Sdk="eQuantic.UI.Sdk">` and nothing else. Bun is embedded per OS and
  architecture, so no Node or npm, and every package ships at one version.
- **The release contract**: every shipping assembly names its class (contract, tool or
  experimental, #899), the C# surface is held by `PublicAPI.*.txt` and the MSBuild and
  configuration surface by a baseline (#322), and the rules of a stable line follow the version
  (#323).

What is not there yet, each with its issue: shared state beyond a component (#289), routes as types
for programmatic navigation (#292), a component test harness an app can use and the variant matrix
it carries (#294), the security review and a CSP the SDK writes (#306), and edit-to-view measured
under a second (#900). Three decisions could change a shape an app writes, and Edgar took them on
2026-10-14, each 1.0 work: the Element is built before the release (#169), the spreadsheet's engine
leaves Primitives as the code editor's did (#185, done: it is `eQuantic.UI.Sheet`), and
per-component theme slots ship in it (#186). The design of the first and the last is
[`docs/ELEMENT-TREE-PLAN.md`](docs/ELEMENT-TREE-PLAN.md).

The bugs are part of the state. They are on the board, the transpiler's fidelity under #164, #565,
#845 and #849, and 1.0 reads them as an exit criterion: no bug open where code compiles and answers
differently, and the new ones a week falling two weeks in a row.

### Evidence snapshot

| Area | State |
|------|-------|
| Transpiler | A strategy per C# construct over an IR (`CodeGen/Ir`, one writer per level). The differential conformance suite runs every case under .NET and under Bun (the suite counts them); what eqc cannot translate faithfully is a build error, listed on the wiki's Diagnostics page, and the subset is on its Supported Features page. Open fidelity bugs: the board, under #164, #565, #845 and #849 |
| Components | The write-once catalog (index: the wiki's Components page), realized on both targets. Owed: the variant matrix and an accessibility checklist per component, through a harness an app author can use too (#294) |
| Client routing | **Shipped** — a link-driven client router (`src/router`), layout kept across navigations, route guards, prefetch on hover and focus, one module per route, scroll restoration; `Navigator.Go` navigates from code. Routes as types instead of strings: #292 |
| Forms & validation | **Shipped** — `FormController`/`FormField`/`Rules` in Primitives (write-once, quiet-until-touched, cross-field, conditional via `Rules.When`/`relevantWhen`), `FormInput`/`FormSubmit` surface, async submit through Server Actions with `ApplyServerErrors`, and the `[FormModel]` DataAnnotations bridge (build-time, no second engine) |
| State management | Component-local `SetState`, and a page's state survives a hot reload. Shared state (signals, context): #289 |
| Server | SSR with hydration, Server Actions behind `[Authorize]`, server data before the first paint (`IServerPrefetch`), and server events: typed topics over one Server-Sent Events stream per page (#291) |
| Hot reload | **Shipped on the web and on the desktop** — on the web a rebuild captures the page's fields and reloads, and the new code renders client-side with those fields handed back (module hot-swap without the reload is the v2 fence); a Photon desktop app applies an edit in process under `dotnet watch`. On a simulator or an emulator it is ahead: redeploy-on-save first, in-process reload across the device boundary later (Track W's W7). Edit-to-view under a second is not measured yet (#900) |
| Styling | The framework ships **one** styling engine: typed C# lowered to deduplicated atomic classes, byte-identical between SSR (C#) and hydration (TS) and cross-pinned by a shared fixture. Authoring is CSS-free; external CSS a consumer brings is their own build concern |
| Debugging | C# in the browser: statement-level source maps, so a thrown error's frame names its C# statement (#293); `EQuanticSourceMaps` writes them with the C# in Debug, none elsewhere, and without the C# for an error reporter (#352); the development error overlay remaps a stack to C# frames |
| Performance | The served runtime's gzip budget fails the suite, each page module's size is reported on every pull request, and the Photon frame has allocation ceilings (#290). Not yet: hydration-time benchmarks, tree-shaking verification |
| Bun | Embedded per OS and architecture; the build needs no Node or npm ✅ |
| Native (Photon) | **Experimental** — every native assembly carries `[Experimental("EQX0001")]` (#324). Shells for macOS, iOS, Android and Windows; its bar for stable (the goldens, a frame with no allocation, physical devices) is the native track's own, after 1.0 |

---

## Gap Analysis by Pillar

### 1. Zero JavaScript — **the linchpin, now largely closed**
The "0 JS" promise is worth exactly as much as the transpiler is **complete and correct**. The bugs
fixed were silent miscompilations — the worst failure mode for a C#-only developer, because the symptom
appears in the browser with no C# vocabulary to debug it. The transpiler-correctness gaps are now done:
- ✅ A **defined, documented supported subset** of C# (`DOTNET-COVERAGE-PROGRAM.md` + wiki).
- ✅ **Compile-time errors for unsupported constructs** — never silent wrong output (`EQ20xx/21xx/10xx`).
- ✅ A **conformance suite that executes the emitted JS (via Bun) and compares to .NET** — every case runs on both sides; the number lives in the test run, not here.
- ✅ **C# in the browser's debugger**: statement-level source maps, so a thrown error's frame names
  its C# statement (#293).

Most browser-only concerns now have a C# shape, realized on the web: focus (`RequestFocus`, initial
focus, focus scopes), keyboard (`Shortcut`, `KeyModifiers`), positioned overlays (`Overlay`,
`Popover`), storage (`IAppStorage`), time (`IClock`, `IFrameTicker`) and the clipboard
(`ITextClipboard`). Still missing on the web:
- **File upload**: `IFileDialogs` is the desktop shells', and the phones' document picker is #782.
- **Observing an element**: whether it is visible, and its size as it changes. A scroll view
  reports its viewport, and nothing more general exists.

### 2. Performance with Bun
Build works without Node, and the measured part is gated (#290): the served runtime has a gzip
budget the suite enforces (`ServedRuntimeBudgetTests`), each page module's size is reported on every
pull request, and the Photon frame has allocation ceilings for a dense scene and for each open layer
(`PerfHarnessTests`). Routes split by the module graph: every page is its own module, which
`boot.ts` imports on the first navigation to it, and what two pages share is a chunk bun splits out
once. Still missing: hydration-time benchmarks and tree-shaking verification.

### 3. Flutter-like DX
The `Build(context)` model is genuinely Flutter-like, and hot reload keeps state on the web and on
the desktop (Phase 3; the simulators and emulators are W7's). Missing the rest of the productivity
multipliers: edit-to-view measured under a second (#900), module hot-swap without a page reload on
the web, richer/more predictable layout components, and layout/diagnostic tooling.

### 4. shadcn-like components
The catalog (the wiki's Components page is its index) is a respectable start, but shadcn's value is **polish + accessibility + variants**.
The accessibility half is done on the web and on the three native shells with a bridge (Phase 5;
UI Automation on Windows is owed). What remains is proof per component: the variant matrix and the
checklist, through a harness an app can use too (#294). Quality > quantity here.

### 5. Styling — **one engine, resolved by decision, not by plugging**
The first-party engine shipped (Track S): typed C# styles lowered to deduplicated atomic classes,
generated at build time from the shared `Primitives` tokens — the same design system Photon paints —
and byte-identical between SSR and hydration. Pluggability is deliberately NOT pursued: the framework
ships exactly ONE styling engine, and any external CSS a consumer brings is their own build concern.
The raw-HTML/CSS escape hatch (`HtmlElement`, `ClassBuilder`) stays for web-only pages.

### Cross-cutting maturity (framework → SDK)
- **Compiler diagnostics**: actionable messages with C# `file:line` + suggestions.
- **Component test harness**: the repository's suites render a component through fixtures an app
  cannot reference, so app authors cannot unit-test theirs (#294).
- **Docs & examples**: essential for a "0 JS" audience — the supported subset and recipes.
- **SSR/hydration robustness**: the build derives the hydration contract (#515), and the classes SSR
  writes are cross-pinned with the ones hydration computes. `tests/e2e` proves adoption, live events
  and client navigation on a real browser, but no CI job runs it: the headless-browser instrument is
  an open decision (#286).
- **Security**: hardened (deserialization allow-list, payload cap); needs a formal hardening review
  and a CSP the SDK writes (#306). **Consent before cookies (2026-09)**: `IConsent` as
  a capability, the `CookieConsent` card, and `UseGtm(...).WithConsent()` — Consent Mode defaults to
  denied and the container is fetched only on a granted answer, on the first paint and live.
- **Server events (2026-10)**: a component hears what the server publishes to a typed topic
  (`ServerTopic<T>`, the `IServerEvents` capability) over one Server-Sent Events stream per page, each
  topic authorized by route templates and ASP.NET Core policies, with seams for the backplane and the
  lifecycle; the empty SignalR hub and the CDN script nothing read are gone (#291). Ahead: a streaming
  Server Action, presence, replay, a WebSocket transport, a Redis backplane package, the Photon
  realization.
- **Server-only code in a web project**: `[ServerOnly]` on a CLASS (2026-09) says the type never
  crosses; before it, a Roslyn service or a hosted warm-up in the web project failed the build on its
  first server-only call, and only another assembly could say otherwise.
- **The declarative surface says everything a screen needs (2026-09)**: factory tails for semantics
  (`label`, `selected`, `expanded`, `role`…) and layout (`width`/`height` on the containers), each an
  init-only property the mirrored constructor deliberately does not carry — reported by the first
  real consumer after migrating its whole UI, counted (49 places), and closed by a contract rule
  rather than an exemption.
- **A real consumer program**: the OS Cleaner (Track W's first consumer) builds against the SDK from
  LOCAL packages (wiki: Build Flow → "Consuming an unreleased SDK from local packages"), renders
  byte-identically from packages and from source, and files every first-contact friction in the
  ledger at the end of `docs/DESKTOP-PLAN.md` — the input a rename or an editor hint needs.

---

## Prioritized Phases

> Rationale: nothing is stable until the transpiler is trustworthy, so it comes first. Then the
> missing SPA primitives (routing, forms, state), then DX (hot reload) and component/CSS polish.

- **Phase 1 — Transpiler correctness & conformance** *(linchpin)* — **✅ complete**:
  supported-subset spec, fail-on-unsupported diagnostics, conformance harness (emitted JS via
  Bun vs .NET) all in place, and the last polish, a thrown error's frame naming its C# statement,
  landed with #293. What it left is a stream of fidelity bugs, read by 1.0's exit criteria.
  → the plan is retired into `docs/LEDGER.md` (2026-06 entries).
- **Phase 2 — Client router** — **✅ complete**: navigation without reload, typed route params, persistent
  layout (reconcile-on-navigate), route guards, `<Link>` with hover/focus prefetch, route-based
  code-splitting, scroll restoration. Demonstrated end-to-end by `samples/DefaultUIDashboard`.
  → the plan is retired into `docs/LEDGER.md` (2026-06 entries).
- **Phase 3 — Hot reload with state preservation** — **✅ complete on the web and the desktop**: the
  web captures the page's fields on a rebuild, reloads, and renders the new code client-side with
  those fields handed back (`HotReloadService`, an SSE channel with ping and reconnect), and a Photon
  desktop app applies an edit in process under `dotnet watch` (`PhotonHotReload`, with
  per-generation caches on the render thread). The piece that took the longest was not the reload
  but WAKING the window. On a simulator or an emulator the loop is a hand recipe today, and hot
  reload there comes with the developer loop (Track W's W7).
- **Phase 4 — Forms & validation engine** — **✅ complete**: the model in Primitives (dirty/touched
  as separate questions, `Error` vs `VisibleError`, cross-field and conditional rules), the thin
  `FormInput`/`FormSubmit` surface, async submit through Server Actions with the server's verdict
  returned onto fields, and `[FormModel]` reading DataAnnotations at build time into the same
  `Rules` — no second validation engine. → wiki `Forms` page.
- **Phase 5 — Component polish & accessibility**: the WEB accessibility half is **✅ complete**
  (the 2026-08 pass: state as attribute for checks/switches/tabs/options, full menu/combobox
  patterns with `aria-activedescendant`, tooltip keyboard reveal + Escape dismissal per WCAG
  1.4.13, dialog names/alertdialog/safe-action initial focus, `aria-expanded` on triggers — all
  pinned on both producers), and the NATIVE half matches on three shells: each has its bridge over the
  one target-neutral semantics tree (macOS `NSAccessibility`, iOS `UIAccessibility` elements,
  Android's virtual `AccessibilityNodeInfo` hierarchy), each proved by asking the platform rather
  than the tree. 2026-08-15 closed the last named gap: a navigation now says WHERE YOU ARE
  (`PressableRole.Destination` / `Link.Current` → `aria-current="page"`, the Selected trait on both
  mobiles), which no existing role could express. Remaining: variant coverage and a component test
  harness (#294), and UI Automation on the Windows shell.
- **Phase 6 — First-party embedded CSS engine** — **✅ shipped** (Track S: atomic classes deduplicated
  by a hash cross-pinned between C# and TypeScript, theme tokens as custom properties with
  fallbacks, one engine and no provider contract by decision — see pillar 5). **Normative
  (2026-07-03): the embedded CSS follows exactly the same design system as mobile** — the Photon
  Design System, generated at build time from the shared `eQuantic.UI.Primitives` tokens (single
  source of truth; no hand-maintained CSS values, parity tested). No other engine is integrated:
  CSS a consumer brings is their own build concern. → `docs/SHARED-COMPONENTS-PLAN.md`.
- **Phase 7 — Global state** (signals/context, #289) + the rest of the performance pillar: the
  budgets landed with #290, and hydration-time benchmarks and tree-shaking verification remain.

## Track I — Editor intelligence (`CodeEditor`)

> **The plan now lives in [`docs/CODE-EDITOR-PLAN.md`](docs/CODE-EDITOR-PLAN.md)** (2026-09-23): the
> editor an IDE is built on (`../equantic-code`), measured against VS Code and Rider, with the
> defects found by driving the current one and the slices that fix and extend it. What follows is
> the track's original framing, kept because the latency argument still holds.

The framework ships a real code editor — document model, selection, history, highlighting, bracket
matching, find, and squiggle decorations — and it is currently a good editor that cannot help you
write. The missing half is intelligence, and the piece that makes it possible is already proven:
Roslyn answers **semantic** completions over the very compilation the
[playground](https://ui.equantic.tech/playground) builds
(`context.` offers `Theme`; `Col` offers `Column`, because the `using static` surface is part of
the compilation). A keyword list would be easy and worthless; this is the real thing.

Three layers, in the order they have to land:

- **I1 — the completion model**, in `Primitives` beside `CodeEditorController`: items, selection,
  the span a commit replaces, and open/filter/move/accept/dismiss. Write-once, and testable without
  a browser.
- **I2 — keys and the list**, in `Components`. The hard part is already solved: the find bar proves
  the `Stack` + `Positioned` overlay pattern, and `metrics` gives `ContentTop`/`LineHeight`/
  `ContentLeft`/`ColumnWidth`, so the list is placed at the caret from numbers the editor already
  has rather than invented geometry.
- **I3 — signature help**: the parameter panel on `(`, reusing I1's plumbing and I2's placement.

**The real problem is latency, not completions.** A round trip per keystroke is unusable, so the
shape is: fetch on a trigger (`.` or Ctrl+Space), filter locally while typing continues, and cancel
stale requests. The source is per-host — a server action on the web; whatever the host offers on
native.

Live diagnostics — squiggles as you type, without pressing Run — are the same shape and are already
proven end to end in the playground: the decoration kind existed, and nothing had ever handed it
anything to draw.

## Track N — Native mobile via proprietary GPU engine ("Photon")

A **parallel track**, independent of web Phases 3–7 (different runtime, different team profile).
Strategic decision (2026-06-10): mobile starts **directly with a proprietary GPU backend** — native
Metal (iOS/macOS) + native Vulkan (Android), offline-precompiled shaders (Slang → SPIR-V/metallib),
SDF-first rasterization for the UI primitive set — with **no Skia tier** (the engine is the product
differentiator; a Skia tier would bake foreign canvas semantics into the framework, the trap Impeller
had to dig Flutter out of). On native, C# runs via AOT — the C#→JS transpiler is web-only; what
carries over is the component authoring model, the keyed-LIS reconciler algorithm, theming concepts
(as typed styles — no CSS plane on native), and the embedded-toolchain/SDK packaging philosophy
(shader compiler embedded like Bun). Guarded from day 0 by a **golden-image harness** on a physical
device farm — the conformance culture applied to pixels. Milestones M0 (triangle + harness, months
0–3) through M5 (v1 preview, months 15–18); 2–3 senior graphics engineers, not a side quest.
→ full plan: `docs/NATIVE-GPU-ENGINE-PLAN.md`. **Architecture decision (2026-07-03):** components are
authored ONCE in a shared assembly (abstract nodes + typed tokens in `eQuantic.UI.Primitives`) and
lowered per target by realizers — DOM/CSS on web, Photon pixels on native — so the two platforms are
written practically identically → `docs/SHARED-COMPONENTS-PLAN.md`.

## Track D — A phone's capabilities in C#

Added 2026-10-11, from Edgar's questions: does every capability of a phone (the camera, the
gyroscope, augmented reality, the fingerprint, the face, sharing with other apps) have its C#
equivalent, written once for Android and iOS, and how does C# reach what each year's releases and
their AI bring? The first round, D1–D7 (closed 2026-08-04), made capabilities services: a contract
in `Primitives`, `builder.Capabilities.UseX("reason")` written into the Info.plist and the manifest
by the SDK, a realization per shell, and `null` where a host has none. The model is right; its
coverage of a phone is not. Twelve contracts, eight realized on iOS and seven on Android; no camera
on Android; nothing for sharing, notifications, haptics, contacts, sound, barcodes or augmented
reality; four permissions declared that no API uses; nothing yet run on a physical device.

Measured against everything the platforms offer a developer today — 355 iOS 26/27 capabilities and
323 Android 16/17, Jetpack and Play services ones, 129 of them AI, each with how C# reaches it —
half of iOS and 60% of Android are already bound by the .NET workloads and need only a contract. The
second round, in four groups:

- **The contracts that need no bridge** (D8–D25): the camera on Android, the clipboard and deep
  links on the phones, opening a link, sharing, notifications, a document picker, device and power
  information, the full camera, barcodes, faces and text, saving to the library, contacts, sound,
  the other sensors, haptics (#345), the orphan permissions, and the decisions on augmented reality
  and the physical-device pass.
- **The bridge** (D26–D27): Swift connectors the SDK compiles into a weak-linked xcframework, and
  Kotlin connectors into an `.aar`, both shipped inside the SDK's own packages. The developer's
  machine needs nothing new, and nothing comes from MAUI.
- **On-device AI and agents** (D28–D31): one contract over Apple's Foundation Models and Gemini
  Nano; speech, translation and text tools; App Intents and AppFunctions from one C# declaration;
  Home Screen and Lock Screen surfaces, Live Activities and Live Updates fed from C#.
- **Upkeep** (D32–D34): reaching Android 17's API 37 from .NET 10, the catalog refreshed after each
  WWDC and Google I/O, and this section kept on the roadmap.

Two defects came first on iOS, and both are fixed: an app built with the iOS 27 SDK did not launch
(#796: the shell runs under the scene life cycle), and the Apple callbacks were not AOT-safe on a
device (#795: static entry points that carry their context; the run on a physical iPhone is #792's). **Fence, with the reason**: what the system
draws in another process (WidgetKit, the Dynamic Island, Glance) and what exists only at compile time
(App Intents, `@Generable`, KSP, Compose) cannot run C#; the plan names the route for each.

→ epic [#774](https://github.com/eQuantic/equantic-ui/issues/774); the plan, the routes counted and
the order: `docs/PHONE-CAPABILITIES-PLAN.md`; the catalog: `docs/platform-catalog/`.

## Track F — Form factors (tablet, watch, TV)

Added 2026-08-14 (Edgar's question: "and apps for watch, tablet?"). The framework's own answer to a
form factor is NOT a second project — the same C# already runs on macOS, iOS and Android from one
`equantic-native` — so this track is about the two things that genuinely differ: what a shell looks
like at a given width, and what a device can do that a phone cannot.

- **Tablet: shipped.** `WindowSizeClass` (Compact <600dp / Medium 600-839 / Expanded ≥840) and
  `AdaptiveNode` were already realized on both targets (build-time media queries on web, re-layout
  on Photon); 2026-08-14 added the missing half of the vocabulary, `NavigationRail`, so one
  `NavItem` list feeds a bar under a phone and a rail down the leading edge of a tablet with no
  listener and no second state. Two goldens pin both. 2026-08-15 closed the rest: `ListDetail` is
  the two-pane pattern as a COMPONENT (the app owns the data, the component owns the rule — the
  first component in the catalog that is itself adaptive), and the rail's alignment knob landed now
  that a component's enum property crosses to the twin as a union rather than a bare string.
- **F1 — pre-built shells in the templates: SHIPPED (2026-08-15).**
  `dotnet new equantic-native --shell blank|tabs|drawer|list-detail` and
  `equantic-app --shell blank|topnav|dashboard`, as a `dotnet new` CHOICE parameter on the existing
  two templates rather than one template per shape (a template per form factor would deny the
  write-once thesis on the first screen a developer sees). Every shape is adaptive, which
  demonstrates the promise in the first 30 seconds of a new project, and the shape lives in ONE
  file: `Program.cs` says `UseRoot<AppShell>()` whichever was picked, and on the web a shell is an
  ordinary component with a generated factory. `--shell list-detail` scaffolds the two-pane
  shape, and calls the `ListDetail` component above rather than reimplementing it.
  A compile guard per shape (`NativeTemplateShellTests`, `TemplateSourceTests`) runs the real
  Roslyn compilation with the SDK's implicit usings, so a wrong overload fails on the push instead
  of on a tag, on three operating systems, after a full pack.
- **F2 — Wear OS.** Wear is Android, so the Android shell is the base. What it needs: a round safe
  area (`isScreenRound` is a configuration answer, not a guess), a size class BELOW Compact (a watch
  is ~200dp and the smallest class today starts where a phone starts), rotary input from the crown
  or bezel, and swipe-to-dismiss. Fence to state plainly: **Tiles and Complications are not
  Activities** — they are their own surfaces with their own render path, so write-once covers the
  watch APP and not the tile.
- **F3 — Android TV.** Cheaper than it looks: the central piece, arrow-key focus navigation, exists
  since the keyboard pass. What it needs is the leanback manifest, an overscan-safe area, the
  10-foot type scale, and a focus ring readable from three metres.
- **Out of scope, with the reason.** Android Auto/AAOS makes the app assemble Google's approved
  templates instead of drawing what it wants, which is the exact opposite of this engine; it would
  need a shell that speaks their language, and at that point it is not the same product. XR sits on
  the same shelf.

## Track E — VS Code extension "eQuantic UI" (the visual editor)

Added 2026-07-31 (Edgar's directive): a first-party VS Code extension that turns the SDK into a
VISUAL development environment — live screen preview, click-to-select components in the viewer, a
property panel that EDITS the C# source, up to full visual editing. Zero third-party by
construction: the extension is plain VS Code API + the SDK itself (eqc, the dev server, the web
realizer render the preview; nothing new renders pixels).

Every pillar it needs already exists in the product:
- **Preview = the real web realizer** in a webview (SSR + runtime — the preview IS the product,
  never a lookalike), with the dev server driving rebuilds (Phase 3 hot reload compounds here).
- **Click-to-select**: the component tree + stable layout paths already power hit-testing and the
  reconciler; the same identity maps a click in the viewer to a `VisualNode`. **Correction
  (2026-08-14): the V3 source maps could not carry it** — the whole `Build` body was emitted as one
  raw string then, so the finest position a map could name was the start of the method body (a
  measured 942-character line in `Badge.ts`). The maps became statement-level with #293, and the
  decision stands: identity comes from a target-neutral `VisualNode.Origin` stamped by a design-mode
  emission, which is exact where a map is correlated, and which the native track inherits for free.
- **Property editing**: Roslyn rewrites the component's object initializers/constructor args in
  place — the same semantic machinery the compiler uses, run in reverse; typed tokens
  (Variant/Space/Radius/TypeRole) make every property a dropdown/slider, not a string.
- **Full editing (the horizon)**: palette of write-once components, insert/move/delete nodes,
  native (Photon) preview riding the SAME abstract tree.

Milestones: **E1** live preview panel (open a `[Page]`/component → rendered webview, auto-reload
on save) · **E2** inspection (click-to-select, highlight, component tree view, jump-to-source)
· **E3** property panel with two-way C# editing · **E4** full visual editing (insert/move/delete
from the component palette).

**Started 2026-08-14** → the plan, with the measurements and the corrected premises, is retired into
`docs/LEDGER.md` (phases 0–18 done; the click-to-select tiers are #215). E1 goes further than "auto-reload on save": the design host
(`src/eQuantic.UI.Design`, `eqdesign`) compiles the **unsaved editor buffer** in-process, measured at
**p50 293 ms** on the 662-line `PaymentsPage` with the full 316-assembly reference set, so nothing in
the loop touches MSBuild or the filesystem. Landed with it: the `equantic.refs.txt` truncation that
silently degraded every hot-reload compile to a model-less one, and three plain-JavaScript emission
bugs that made a module unparseable (so the playground and the conformance harness were one static
collection away from a blank frame).

**Decided 2026-08-14 (Edgar)**: the declarative form is the preferred one to AUTHOR in, so structural
insertion stays fenced to it — but the editor opens code that already exists, and real code splits
itself across helper methods and other files, so **understanding must work everywhere**. Selecting,
inspecting and editing a property therefore work at any tier and across files (the host is sent every
open buffer); only insert/move/remove require a `children: [ … ]` list, and every refusal names its
reason before the affordance is drawn. E2, E3 and the structural half of E4 have landed on that
split; dragging on the canvas and the `Grid`/`Stack` gestures are what remain.

## Track L — Localization (multi-language, zero third-party)

Added 2026-08-01 (Edgar's directive): multi-language must work with **only what .NET offers** — the
developer localizes with `.resx` and the strongly-typed accessor exactly as in any .NET app, and
never sees a JavaScript catalog or a framework DSL. The mechanical problem is that
`ResourceManager` cannot run in the browser, so eqc bridges it at build time: resource accessors
are REWRITTEN to a runtime lookup (never inlined — inlining would bake the build machine's culture
into the bundle), used keys are emitted as per-culture catalogs, and the server ships the active
culture through the same shell slot the theme already uses (`__EQ_THEME__` → `__EQ_CULTURE__`,
applied before hydration so SSR identity holds). Native needs no bridge at all — Photon runs the
same C# with satellite assemblies, which is the write-once payoff.

Full design (decisions, workstreams, milestones, fences — including the honest hard parts: the
culture-aware FORMATTING subset and 3+ form plurals) in `docs/I18N-PLAN.md`. 2026-08-12: the plan
now also covers the SDK's OWN component strings (D14/W8 — a11y announcements were hardcoded
English), the `CurrentCulture`/`CurrentUICulture` pair, and `<html lang>` from the request culture.

## Track W — Photon on the desktop

Added 2026-08-25, born from the OS Cleaner migration plan (the app's own plan maps its S1–S6 onto
W1–W6 here) — but nothing in it is app-specific: these are the fences Photon had to close to claim
DESKTOP APP on that day, each verified in code, not docs (the signing and part of the shell surface
have closed since: where it stands is below). Nine draw commands and no arbitrary paths
(`DisplayList.cs`); a deliberately PERIODIC `IClock` whose own fence names the frame clock as the
thing not yet built; rectangle-only hit-testing (`PhotonHost.cs` — `Bounds.Contains` on AABBs); no
desktop shell surface (menus, tray, file dialogs, notifications, deep links); ad-hoc signing
(`--sign -`) that breaks TCC grants every build; one window per process.

The sweep also found what SHRINKS the work: a deterministic time channel already runs through
layout (`TransitionStore`, `TransitionClock` since #975, pure in `(path, target, timeMs)`, wired at
two call sites), the 120 Hz
driver exists (as a run-loop timer — the ticker work upgrades it to a display link), two-stop
radial gradients exist, and the shader toolchain self-resolves — and **W1's first shape is
DONE**: `FillAnnularSector` shipped in 0.2.0-preview.45 as an exact SDF with Reference↔Metal↔Vulkan
parity, consumer-proven on a real 5.38-million-file sunburst; the polygon-vs-texture decision waits
on the consumer's blob benchmark (W2).

Workstreams: W1 engine shapes (annular sector + convex polygon, SDF family, NOT a path engine);
W2 frame clock + tweens/springs + `BoxStyle.Transition` honored natively + `IUiDispatcher`
(revised DOWN from the consumer's estimate — the store and time thread exist); W3 a custom drawing
node with local-coordinate pointer events (also the hit-testing seam the headless-browser
instrument has waited for); W4 the macOS desktop shell surface; W5 real packaging — signing,
entitlements, notarization, publish-based bundling, minimal updater — sequenced FIRST among the
independents because the identity unblocks TCC, notifications and updates at once; W6 the
Windows shell — its first slice landed 2026-09-05 (a real window with per-monitor DPI and
dark-mode follow, Vulkan-or-Reference presenting, DirectWrite/Direct2D/WIC, the desktop
capabilities, `WinExe` + `.ico` from the SDK; UI Automation owed) while Linux stays post-M5 as
the engine plan already decided. Landed ahead of W5's real
identity (0.2.0-preview.45): the ad-hoc bundle is rebuilt from the payload every build, `runtimes/` is
an allowlist, and a signing failure fails the build — an identity that flips between builds is how a
TCC grant evaporates, and that was happening.

W7 (added 2026-08-25, raised by Edgar): the DEVELOPER LOOP — one command per target with device
discovery embedded in `Sdk.Native` (`-t:RunIos` / `-t:RunAndroid`), `launchSettings.json` profiles
so Rider/VS run and debug in one click, hot reload staying `dotnet watch` on desktop and
redeploy-on-save as the first honest rung on simulators and emulators.

Where it stands on 2026-10-14: W5 is delivered but for one proof and one piece. The bundle is
assembled from the publish output, signed with a real identity, notarized and imaged as a DMG, but no
notarized bundle has been produced yet for want of a Developer ID, and `IAppUpdater` is not started.
W4 started with deep links, file dialogs and `IWorkspace`, and W6's Windows shell has its first
slice. The track ships as experimental in 1.0 (#324).

Full gaps-with-evidence, sequencing and per-workstream acceptance: `docs/DESKTOP-PLAN.md`.

## Track M — Email rendering

Added 2026-08-24 (Edgar's question): author an email in C# with the same components a page is
written with, and get back HTML an email client will actually render. Email is a THIRD REALIZER
beside web and Photon — a target whose engine is merely very restrictive, which is architecturally
the same shape as a target with no DOM, so nothing in the authoring layer changes.

Two pieces already exist: `WebRealizer.Lower(..., styles: null)` keeps styles INLINE instead of
atomising them into classes, and `HtmlRenderer.RenderNode` turns the lowered tree into a string.
What is missing is the medium — nested-table layout for Outlook's Word engine, literal colors
instead of custom properties, absolute image URLs, a 600px shell, and a fence over everything an
email cannot do (`ScrollView`, `Pressable`, hover, `position: absolute`).

**Status: the core SHIPPED in 0.2.0-preview.45** — `eQuantic.UI.Email` (`EmailRealizer`/`EmailRenderer`):
nested tables with spacer cells for gaps, inline styles, literal colors (translucent flattened over
the surface), absolute-URL guard, a 600px shell, a plain-text twin from the same tree, and the fences
(gradients, `MaxLines`, anything an email cannot do) as build-time refusals. Ahead: the real-client
matrix (a generated sample against Gmail, Outlook and Apple Mail) and HTML pins once the output
settles. Full design and fences: wiki **[Email Rendering](https://github.com/eQuantic/equantic-ui/wiki/EmailRealizer)**.

## Definition of "production-ready" (per pillar)

The bar 1.0 holds the web track to (#898, criterion 3); each line says whether it is met.

- **0 JS**: any unsupported C# fails the build with a clear message; conformance suite green; C#
  debuggable in the browser. Met: the diagnostics and the suite since Phase 1, the debugger by #293.
  What compiles and still answers differently is a bug, and 1.0's criterion 5 reads those.
- **Performance**: documented bundle budgets enforced in CI; code-splitting per route. Met by
  #290 for the runtime every page loads (its budget fails the suite) and by the module graph for
  the routes (a page's module loads on the first navigation to it).
- **Flutter DX**: hot reload preserves state; <1s edit-to-view. The state is kept on the web and on
  the desktop (Phase 3), and on a simulator or an emulator hot reload waits for W7; the second has
  never been measured (#900).
- **Components**: every shipped component passes an a11y checklist + variant matrix tests. Not met
  (#294).
- **CSS**: at least one first-party engine ships. Met by Phase 6; a provider contract was the
  other half of this line, and pillar 5 declined it.
- **Security**: a formal hardening review, and a CSP the SDK writes. Not met (#306).
