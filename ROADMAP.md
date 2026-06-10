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
so the correctness net has since been built (Phase 1): a 492-case conformance harness, fail-on-unsupported
diagnostics, and a documented supported subset. That was the linchpin; it is now in place.

### Evidence snapshot

| Area | State |
|------|-------|
| Transpiler | 120+ strategies; **correctness net in place** — 492-case Bun-vs-.NET conformance harness, fail-on-unsupported diagnostics (`EQ2001/2002/21xx/1001/1002`), documented supported subset; the silent fallbacks are resolved to support or explicit diagnostics |
| Components | **77** component files (Inputs 14, Overlays 11, Display 11, Navigation 8, Layout 8, Surfaces 6, Feedback 5, Forms 3 + primitives); some were half-implemented |
| Client routing | **None found** — no client-side router/navigation |
| Forms & validation | `FormField` exists (displays errors) but **no validation engine** |
| State management | Component-local `SetState` only; **no global state / signals / context** |
| Hot reload | "HMR" is actually **full-page live-reload** (`BroadcastUpdateAsync("reload")`) |
| CSS engine pluggability | `IStyleProvider` + `StyleProviderRegistry` **scaffolded**; only Tailwind is real today |
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
widgets, and layout/diagnostic tooling.

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
  supported-subset spec, fail-on-unsupported diagnostics, conformance harness (492 cases, emitted JS via
  Bun vs .NET) all in place; remaining polish is the in-browser source-map smoke test.
  → see `docs/IMPLEMENTATION-PLAN.md`.
- **Phase 2 — Client router** — **✅ complete**: navigation without reload, typed route params, persistent
  layout (reconcile-on-navigate), route guards, `<Link>` with hover/focus prefetch, route-based
  code-splitting, scroll restoration. Demonstrated end-to-end by `samples/DefaultUIDashboard`.
  → see `docs/PHASE-2-CLIENT-ROUTER-PLAN.md`.
- **Phase 3 — Hot reload with state preservation**: replace full reload with module/state-preserving
  reload; sub-second feedback loop.
- **Phase 4 — Forms & validation engine**: declarative C# validation (ideally `DataAnnotations`),
  form state (dirty/touched), async submit wired to Server Actions.
- **Phase 5 — Component polish & accessibility**: finish existing components, add focus/keyboard/aria,
  variant coverage, a component test harness — before adding new components.
- **Phase 6 — First-party embedded CSS engine** + documented provider contract.
- **Phase 7 — Global state** (signals/context) + performance budgets & benchmarks.

## Definition of "production-ready" (per pillar)
- **0 JS**: any unsupported C# fails the build with a clear message; conformance suite green; C#
  debuggable in the browser.
- **Performance**: documented bundle budgets enforced in CI; code-splitting per route.
- **Flutter DX**: hot reload preserves state; <1s edit-to-view.
- **Components**: every shipped component passes an a11y checklist + variant matrix tests.
- **CSS**: at least one first-party engine ships; provider contract documented.
