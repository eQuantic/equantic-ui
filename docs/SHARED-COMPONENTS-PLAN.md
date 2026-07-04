# eQuantic.UI — Shared Component Architecture (write once, web + native)

> Decision recorded 2026-07-03. Components are authored **once**, in a **shared assembly**, against an
> abstract visual vocabulary + typed design tokens; a per-target **realizer** lowers the abstract tree
> to the concrete one — DOM + CSS on web, Photon display lists (pixel rendering) on native. This
> supersedes the "sibling component sets, convergence later (Track N2)" stance in
> `docs/NATIVE-GPU-ENGINE-PLAN.md` (D12 is re-scoped accordingly): the AUTHORING layer unifies now;
> sibling trees remain only at the REALIZATION level.

## The layering

```
┌──────────────────────────────────────────────────────────────────┐
│ AUTHORING — written once, one shared assembly                    │
│                                                                  │
│  eQuantic.UI.Components (shared)                                 │
│    Button, Card, Badge, List, AppBar, …                          │
│    composed ONLY from the abstract primitives below              │
│                                                                  │
│  eQuantic.UI.Primitives (zero dependencies)                      │
│    · abstract visual nodes: Box · Row · Column · Stack ·         │
│      ScrollView · Text · Image · Icon · Pressable                │
│    · TYPED styles + design tokens (the Photon Design System)     │
│    · value types: Color, EdgeInsets, CornerRadii, …              │
│                                                                  │
│  eQuantic.UI.Core — StatefulComponent/SetState/RenderContext     │
│    (the existing model; becomes target-neutral)                  │
├───────────────────────────┬──────────────────────────────────────┤
│ WEB REALIZER              │ NATIVE REALIZER                      │
│ abstract → HtmlElement/   │ abstract → Photon primitive tree →   │
│ DOM + CSS                 │ display list → Metal/Vulkan          │
│ (eqc transpiler + TS      │ (AOT + engine — Track N continues    │
│  runtime + reconciler,    │  unchanged underneath)               │
│  all unchanged)           │                                      │
└───────────────────────────┴──────────────────────────────────────┘
```

Only the **closed set of primitive nodes** needs a per-target implementation; the entire component
library is composition above it, in shared code.

## Why this fits what already exists

1. **"One assembly, two compilers" already ships.** `eQuantic.UI.Components` today embeds
   `tools/source/*.cs` for the eqc transpiler alongside the compiled assembly. The shared library uses
   the same packaging: assembly → native (AOT) + SSR; sources → transpiler. No new build technology.
2. **The web realizer lowers to `HtmlElement`** — so shared components compose naturally with the
   legacy web component set (contain / be contained), making migration genuinely incremental.
   `HtmlElement` keeps its 1:1 DOM-mirror rule; it stops being the authoring surface and becomes the
   web realizer's TARGET vocabulary.
3. **The typed token layer is already target-neutral.** `ColorToken`, `Space`, `Radius`, `TypeStyle`,
   `VariantColors`, the Button style resolver — implemented from the Photon Design System — live in
   `eQuantic.UI.Primitives` and are the single styling plane for both targets.
4. **The reconciler algorithm is the same on both sides** (keyed-LIS); each realizer diffs its own
   concrete tree.

## Styling: one canonical plane

- **Typed styles + tokens are canonical.** Native consumes values directly; web lowers them to CSS.
- Web lowering v1: **inline styles / CSS custom properties** generated from typed styles — simple,
  SSR-friendly, no Tailwind dependency for shared components.
- **NORMATIVE — one design system, both targets (recorded 2026-07-03):** the web's **embedded
  first-party CSS** (web Phase 6 — no preference for Tailwind or any other engine) must follow
  **exactly the same design system as mobile**: the Photon Design System
  (`docs/design/Photon-Design-System.dc.html`), whose single source of truth is the C# token layer in
  `eQuantic.UI.Primitives`. Concretely:
  - Every CSS artifact the embedded engine ships — custom properties per theme mode, utility classes,
    component classes — is **GENERATED at build time from the Primitives tokens**. Hand-maintained CSS
    copies of token values are forbidden; there is no second palette, type scale, radius scale,
    elevation ramp or motion table to drift.
  - Parity is **tested**, not promised: token → generated-CSS value tests (the same pins + recomputed
    WCAG checks that guard the C# side), and the cross-target visual/layout harnesses compare the two
    realizations of the same component.
  - A Photon DS change therefore lands in one place (Primitives) and reaches native values and web CSS
    in the same build.
- **`ClassName`/Tailwind is a web-only escape hatch** on shared components: honored by the web
  realizer, ignored by native (analyzer warning). Web-only components keep full Tailwind power —
  but Tailwind is interop/compat, never the source of the design system.

## Anti-lowest-common-denominator (first-class escape hatches)

The React Native / Flutter-web lesson: "identical everywhere" must not become "mediocre everywhere".

- `ctx.Platform` (`Web` / `Ios` / `Android` / …) for behavioral forks in shared code.
- **Per-target component substitution**: a shared component may register a target-specific
  implementation; the realizer picks it up (e.g. a web `Select` keeps a native `<select>`, the native
  one opens a bottom-sheet picker — same authored API).
- Target-only props are ignored (with a warning) on the other target: `ClassName` (web),
  hit-slop tuning (native).
- Deeply DOM-specific components simply stay web-only; native equivalents follow the design system.

## Layout parity (the hard part, fenced)

Shared components use a **flex SUBSET** with identical semantics on both targets: direction, gap,
padding, main/cross alignment, grow/shrink, min/max constraints, absolute-in-Stack. On web it lowers
to CSS flex; on native to Photon's layout engine. Parity is enforced by a **cross-target layout
conformance harness**: the same abstract tree is laid out by the native engine and by a headless
browser, and the resulting geometries are compared numerically in CI — the conformance culture,
applied to layout. Anything outside the subset is a per-target concern by definition.

## Where abstract becomes concrete on web

The lowering runs in the **TypeScript runtime** (abstract node objects → `HtmlNode` just before the
reconciler): transpiled component code stays target-agnostic and the reconciler is untouched. (The
alternative — lowering in C# before emit — would specialize the transpiled output per target and is
rejected.)

## Package/assembly layout

| Package | Contents | Depends on |
|---|---|---|
| `eQuantic.UI.Primitives` | Color & layout value types, design tokens, typed styles, abstract nodes | — (zero) |
| `eQuantic.UI.Core` | Component model (`StatefulComponent`, `SetState`, `RenderContext`) | Primitives |
| `eQuantic.UI.Components` | The shared component library (assembly + `tools/source` for eqc) | Core, Primitives |
| `eQuantic.UI.Web` (realizer) | abstract → `HtmlElement`/DOM + CSS; SSR | Core, Components |
| `eQuantic.UI.Native.Engine(.…)` | Photon engine + backends (unchanged) | Primitives |
| `eQuantic.UI.Native.Components` | Native realizer: abstract → Photon (+ component renderers) | Primitives, Engine |

## Migration path (incremental, design-system first)

1. ✅ **Done:** `eQuantic.UI.Primitives` extracted — `Color` + the full token/style layer moved out of
   the engine/native packages; the engine depends on Primitives (inverted).
2. ✅ **Done:** abstract node vocabulary (Box/Row/Column/Stack-pending/Text/Pressable + Flexible/Spacer)
   + C# flex layout (`eQuantic.UI.Native.Framework`) + native realizer; shared component model
   (`UiComponent`/`StatelessComponent`/`StatefulComponent`+`SetState`) with Button/Card in
   `eQuantic.UI.Components.Shared` and the `PhotonHost` frame loop (native Counter end-to-end).
3. 🔄 **Web realizer** — slice 1 ✅ (2026-07-04): `eQuantic.UI.Web` lowers the SAME trees to
   `HtmlElement`/DOM server-side (SSR): Box→div (border-box = inside-border parity), Row/Column→flex
   (Flexible → `flex: n 1 0%` matching native leftover-by-weight; `min-width:0` preserves the
   truncation contract), Text→role-classed span, Pressable→neutralized button; colors lower as
   `light-dark()` values straight from tokens, so the DOM is MODE-FREE like the abstract tree (theme
   switch = `color-scheme`). `PhotonCssGenerator` emits the NORMATIVE stylesheet (custom properties,
   type-role classes, elevation shadows, motion vars) — parity with the C# tokens is tested per value.
   Slice 2A ✅ (2026-07-04): the TypeScript-runtime lowering (`src/shared/lowering.ts` over the
   transpiled node shapes in `nodes.ts`) mirrors the WebRealizer rule-for-rule, with a CROSS-PINNED
   byte-exact style-string literal asserted by BOTH suites (hydration parity is a tested contract,
   including the canonical CSS property order). Remaining slice 2B: eqc transpilation of the shared
   C# sources + boot/component-render integration (theme registry, vocabulary classes in the runtime).
4. Legacy web components migrate progressively as they're touched; mixing is safe throughout.
5. Layout conformance harness lands with step 2 and gates every layout feature after it.
