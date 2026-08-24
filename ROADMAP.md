# eQuantic.UI — Roadmap & Honest State Assessment

> Last updated: 2026-06-10. This document is an honest, evidence-based assessment of where
> eQuantic.UI stands against its vision, and a prioritized roadmap to close the gaps.
>
> **Update (2026-06-10): Phase 1 (transpiler correctness & conformance) is essentially complete.** The
> correctness net now exists — a **492-case conformance harness** (emitted JS run via embedded Bun and
> compared to .NET), **fail-on-unsupported** diagnostics (no silent miscompiles), and a documented
> supported subset (`docs/DOTNET-COVERAGE-PROGRAM.md` + wiki `SupportedFeatures`). The "honest state"
> below describes the pre-hardening starting point; the transpiler rows are updated inline. The forward
> path is Phases 2–7.

## The Vision

eQuantic.UI aims to be a **true 100% C# UI SDK**:

1. **Zero JavaScript knowledge required** — authors write C#; the SDK compiles to optimized JS.
2. **Performant, with Bun behind the scenes** — embedded Bun does the bundling; no Node/npm required of the user.
3. **Flutter-like authoring** — declarative `Build(context)` component model.
4. **Rich, ready-made components** — shadcn-like semantics and polish.
5. **Pluggable CSS engine** — choose your preferred engine, or use an embedded one.

## Current State (honest)

eQuantic.UI is a **technically real, ambitious project at an advanced-alpha stage** — not vaporware.
What genuinely exists and works: a Roslyn-based C#→JS transpiler (**118 conversion strategies**),
SSR, Server Actions with authorization, a keyed-LIS reconciler, a theming/StyleBuilder system,
Tailwind integration, embedded Bun, and large auto-generated icon packages.

What the state honestly reflects: a **young project with the right foundation that has not yet been
through a hardening phase.** A single review pass surfaced **24 real bugs**, several of them silent
miscompilations (integer division producing floats, `Math.Truncate` crashing at runtime, enums
representing as a number in one path and a string in another, `.ToString("F2")` dropping its format,
`Text` dropping its content, SVG created in the wrong DOM namespace, source-map columns all wrong,
hydration mis-aligning on whitespace). These have been fixed, and their *nature* was the real signal —
so the correctness net has since been built (Phase 1): a 530-case conformance harness, fail-on-unsupported
diagnostics, and a documented supported subset. That was the linchpin; it is now in place.

### Evidence snapshot

| Area | State |
|------|-------|
| Transpiler | 120+ strategies; **correctness net in place** — 530-case Bun-vs-.NET conformance harness, fail-on-unsupported diagnostics (`EQ2001/2002/21xx/1001/1002`), documented supported subset; the silent fallbacks are resolved to support or explicit diagnostics |
| Components | **77** component files (Inputs 14, Overlays 11, Display 11, Navigation 8, Layout 8, Surfaces 6, Feedback 5, Forms 3 + primitives); some were half-implemented |
| Client routing | **Shipped** — link-driven client router (`src/router`), layout preserved across navigation by reconcile-on-navigate. A typed programmatic `Navigator` API is still ahead |
| Forms & validation | **Shipped** — `FormController`/`FormField`/`Rules` in Primitives (write-once, quiet-until-touched, cross-field, conditional via `Rules.When`/`relevantWhen`), `FormInput`/`FormSubmit` surface, async submit through Server Actions with `ApplyServerErrors`, and the `[FormModel]` DataAnnotations bridge (build-time, no second engine) |
| State management | Component-local `SetState` only; **no global state / signals / context** |
| Hot reload | Reload-based, but the runtime **captures live page state and replays it** through the ordinary SSR-hydration mechanic — save a file, the UI updates, the state survives. Module hot-swap without a reload is the v2 fence |
| CSS engine pluggability | The framework now ships **one** styling engine: typed C# lowered to deduplicated atomic classes, byte-identical between SSR (C#) and hydration (TS) and cross-pinned by a shared fixture. Authoring is CSS-free; external CSS a consumer brings is their own build concern |
| Bun | Embedded per-platform; build works without Node/npm ✅ |
| Debugging | Source maps were broken (now fixed); error overlay exists |

---

## Gap Analysis by Pillar

### 1. Zero JavaScript — **the linchpin, now largely closed**
The "0 JS" promise is worth exactly as much as the transpiler is **complete and correct**. The bugs
fixed were silent miscompilations — the worst failure mode for a C#-only developer, because the symptom
appears in the browser with no C# vocabulary to debug it. The transpiler-correctness gaps are now done:
- ✅ A **defined, documented supported subset** of C# (`DOTNET-COVERAGE-PROGRAM.md` + wiki).
- ✅ **Compile-time errors for unsupported constructs** — never silent wrong output (`EQ20xx/21xx/10xx`).
- ✅ A **conformance suite that executes the emitted JS (via Bun) and compares to .NET** — 492 cases.
- 🔄 **Source maps** validated (generation/decode); in-browser C# stack-trace smoke test still to add.

Still genuinely missing (UI surface, not transpiler correctness):
- C# abstractions for browser-only concerns currently requiring JS know-how: focus/keyboard,
  positioned overlays/portals, intersection/resize observers, `localStorage`, timers, file upload,
  clipboard.

### 2. Performance with Bun
Build works without Node. Missing the *measured* part: per-route bundle budgets, route-based
code-splitting, hydration-time benchmarks, tree-shaking verification, and a perf regression gate.

### 3. Flutter-like DX
The `Build(context)` model is genuinely Flutter-like. Missing the productivity multipliers:
**true hot reload with state preservation** (today it is full reload), richer/more predictable layout
components, and layout/diagnostic tooling.

### 4. shadcn-like components
77 components is a respectable start, but shadcn's value is **polish + accessibility + variants**.
Missing: serious a11y (focus management, keyboard nav, correct `aria-*`, focus-trap in overlays),
consistent variant coverage, and finishing the half-baked ones. Quality > quantity here.

### 5. Pluggable CSS engine
The abstraction exists (`IStyleProvider`). Missing: at least one **complete first-party embedded
engine** (utility parser → CSS, purge/tree-shake, design tokens) working end-to-end without Node,
plus a documented contract so third parties (UnoCSS, etc.) can implement a provider.

### Cross-cutting maturity (framework → SDK)
- **Compiler diagnostics**: actionable messages with C# `file:line` + suggestions.
- **Component test harness**: app authors currently can't unit-test components (rendering a `Select`
  throws without a registered theme).
- **Docs & examples**: essential for a "0 JS" audience — the supported subset and recipes.
- **SSR/hydration robustness**: the hydration mis-alignment shows this path is still fragile.
- **Security**: hardened in this pass (deserialization allow-list, SignalR relay, payload cap);
  needs a formal hardening review + CSP guidance.

---

## Prioritized Phases

> Rationale: nothing is stable until the transpiler is trustworthy, so it comes first. Then the
> missing SPA primitives (routing, forms, state), then DX (hot reload) and component/CSS polish.

- **Phase 1 — Transpiler correctness & conformance** *(linchpin)* — **✅ essentially complete**:
  supported-subset spec, fail-on-unsupported diagnostics, conformance harness (530 cases, emitted JS via
  Bun vs .NET) all in place; remaining polish is the in-browser source-map smoke test.
  → see `docs/IMPLEMENTATION-PLAN.md`.
- **Phase 2 — Client router** — **✅ complete**: navigation without reload, typed route params, persistent
  layout (reconcile-on-navigate), route guards, `<Link>` with hover/focus prefetch, route-based
  code-splitting, scroll restoration. Demonstrated end-to-end by `samples/DefaultUIDashboard`.
  → see `docs/PHASE-2-CLIENT-ROUTER-PLAN.md`.
- **Phase 3 — Hot reload with state preservation** — **✅ complete on both targets**: the web
  replays page state over a mounted tree (`HotReloadService`, an SSE channel with ping and
  reconnect) and the native side applies in-process under `dotnet watch` (`PhotonHotReload`, with
  per-generation caches on the render thread). The piece that took the longest was not the reload
  but WAKING the window.
- **Phase 4 — Forms & validation engine** — **✅ complete**: the model in Primitives (dirty/touched
  as separate questions, `Error` vs `VisibleError`, cross-field and conditional rules), the thin
  `FormInput`/`FormSubmit` surface, async submit through Server Actions with the server's verdict
  returned onto fields, and `[FormModel]` reading DataAnnotations at build time into the same
  `Rules` — no second validation engine. → wiki `Forms` page.
- **Phase 5 — Component polish & accessibility**: the WEB accessibility half is **✅ complete**
  (the 2026-08 pass: state as attribute for checks/switches/tabs/options, full menu/combobox
  patterns with `aria-activedescendant`, tooltip keyboard reveal + Escape dismissal per WCAG
  1.4.13, dialog names/alertdialog/safe-action initial focus, `aria-expanded` on triggers — all
  pinned on both producers), and the NATIVE half now matches: every shell has its bridge over the
  one target-neutral semantics tree (macOS `NSAccessibility`, iOS `UIAccessibility` elements,
  Android's virtual `AccessibilityNodeInfo` hierarchy), each proved by asking the platform rather
  than the tree. 2026-08-15 closed the last named gap: a navigation now says WHERE YOU ARE
  (`PressableRole.Destination` / `Link.Current` → `aria-current="page"`, the Selected trait on both
  mobiles), which no existing role could express. Remaining: variant coverage, a component test
  harness.
- **Phase 6 — First-party embedded CSS engine** + documented provider contract. **Normative
  (2026-07-03): the embedded CSS follows exactly the same design system as mobile** — the Photon
  Design System, generated at build time from the shared `eQuantic.UI.Primitives` tokens (single
  source of truth; no hand-maintained CSS values, parity tested). Tailwind/other engines remain
  interop options, never the design-system source. → `docs/SHARED-COMPONENTS-PLAN.md`.
- **Phase 7 — Global state** (signals/context) + performance budgets & benchmarks.

## Track I — Editor intelligence (`CodeEditor`)

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
  (2026-08-14): the V3 source maps CANNOT carry it** — the whole `Build` body is emitted through a
  single `Raw(jsBody, BuildMethodNode.Body)` call (`TypeScriptEmitter.cs:252`), so the finest position
  a map can name is the start of the method body (a measured 942-character line in `Badge.ts`).
  Identity comes instead from a target-neutral `VisualNode.Origin` stamped by a design-mode emission,
  which is exact rather than correlated and which the native track inherits for free.
- **Property editing**: Roslyn rewrites the component's object initializers/constructor args in
  place — the same semantic machinery the compiler uses, run in reverse; typed tokens
  (Variant/Space/Radius/TypeRole) make every property a dropdown/slider, not a string.
- **Full editing (the horizon)**: palette of write-once components, insert/move/delete nodes,
  native (Photon) preview riding the SAME abstract tree.

Milestones: **E1** live preview panel (open a `[Page]`/component → rendered webview, auto-reload
on save) · **E2** inspection (click-to-select, highlight, component tree view, jump-to-source)
· **E3** property panel with two-way C# editing · **E4** full visual editing (insert/move/delete
from the component palette).

**Started 2026-08-14** → full plan, with the measurements and the corrected premises:
`docs/VSCODE-VISUAL-EDITOR-PLAN.md`. E1 goes further than "auto-reload on save": the design host
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

Full design, slices and fences: wiki **[Email Rendering](https://github.com/eQuantic/equantic-ui/wiki/EmailRealizer)**.
M0 MEASURES what the two existing calls already survive in Gmail, Outlook and Apple Mail before M1
builds anything — half a day, no new code, and it decides the size of the rest.

## Definition of "production-ready" (per pillar)
- **0 JS**: any unsupported C# fails the build with a clear message; conformance suite green; C#
  debuggable in the browser.
- **Performance**: documented bundle budgets enforced in CI; code-splitting per route.
- **Flutter DX**: hot reload preserves state; <1s edit-to-view.
- **Components**: every shipped component passes an a11y checklist + variant matrix tests.
- **CSS**: at least one first-party engine ships; provider contract documented.
