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
   including the canonical CSS property order).
   Slice 2B ✅ (2026-07-04): eqc transpiles the REAL shared components and the runtime executes them.
   Compiler: metadata value-type object initializers survive as config objects (were silently
   dropped), C# ctor parameter defaults become JS defaults, and Primitives types route to
   `@equantic/runtime` imports via SEMANTIC discovery (namespace-based, enums/interfaces excluded —
   no fixed lists; also fixed the ordering bug that left the parser's semantic provider null since
   inception). Runtime: vocabulary classes (`shared/vocabulary.ts` + `value-types.ts`) ARE the wire
   shapes AND self-lower via `render()` with the ambient theme, so abstract trees slot into the
   existing component pipeline with ZERO reconciler changes (and web components mix into abstract
   trees through the same seam); `design-system.generated.ts` carries every token/theme/size-table
   value GENERATED from the C# single source (byte-pinned like the CSS); `RenderContext.theme` feeds
   transpiled `context.theme` reads. Proof: the committed transpiled Button/Card fixtures (pinned to
   the live compiler output) render in vitest to the SAME values the C# WebRealizer tests pin.
   Slice 2C ✅ (2026-07-04): shared STATEFUL components run on web + SDK wiring. The Primitives
   stateful shape (fields on the component + direct `SetState`, no `CreateState` split) transpiles —
   the parser routes a base that RESOLVES to `eQuantic.UI.Primitives.StatefulComponent` (semantic,
   not name-based) to the runtime's new `SharedStatefulComponent` (deliberately parallel to the Core
   `StatefulComponent`; the Core unification consolidates them). En-route compiler fixes, each one a
   silent-wrong-code class: named arguments now REORDER to the constructor's real parameter
   positions with skipped parameters filled from their C# defaults (JS has no named args — before,
   `onPressed:` landed in the `variant` slot); fields without initializers get C#'s implicit value-
   type default (`private int _count;` → `= 0`, not `undefined`→NaN); and eqc's Roslyn compilations
   now enable NULLABLE annotations (without it `Action?` parses as `Nullable<Action>`, overload
   binding fails, and every semantic path silently degrades — fixed in ProjectCompilationHelper and
   SemanticModelProvider). Proof: the `SharedCounter` fixture (the SAME authoring shape as the
   native CounterAppTests component, composing Button with a named argument) is real eqc output
   pinned in CI and EXECUTED in vitest — mount to DOM, click the lowered button, `setState` → rAF →
   re-render (`Count: 0` → `3`), mirroring the native tap → SetState → rebuild golden. SDK wiring:
   `Components.Shared` ships sources at `tools/source`; the SDK adds them to the eqc scan behind the
   OPT-IN `<EnableEQuanticSharedComponents>true</…>` — the shared library intentionally reuses names
   (Button, Card…), so a default-on scan would collide with the standard web components until the
   unification swaps them in. Default builds are byte-identical (sample-verified).
   Unification slice 1 ✅ (2026-07-04): the Core⇄Shared SSR bridge. `eQuantic.UI.Web.VisualNodeComponent`
   is an `IComponent` adapter over `WebRealizer.Lower` — a Core page composes write-once components
   (`new VisualNodeComponent(new Card(...))`) anywhere an IComponent fits, server-rendered against
   `PhotonTheme.Instance` (or an explicit theme). Client-side the SAME call resolves to the runtime's
   mirror class, lowering with the ambient theme — the hydration-parity pair. Routing is powered by
   the new `[RuntimeProvided]` attribute (Primitives): any type carrying it imports from
   `@equantic/runtime`, extending the namespace rule to runtime-backed adapters living elsewhere.
   Boot-time theming needed no new machinery: `setPhotonTheme` inside the existing `__registerTheme`
   hook swaps what the ambient lowering resolves (spec-tested).
   Unification slice 2 ✅ (2026-07-05): the shared library is RUNTIME-PROVIDED and the first live
   page shipped. Instead of per-origin module namespacing, the transpiled library components
   (Button/Card — already byte-pinned to eqc output) EMBED in runtime.js (`shared/components/*`,
   same pin with the import source rewritten to an internal aggregator, avoiding a self-referential
   package import) and export from `@equantic/runtime`. eqc routes the `eQuantic.UI.Components.Shared`
   namespace to the runtime — so the deliberate name reuse against the standard web components
   resolves SEMANTICALLY: `using eQuantic.UI.Components` → `./Button` (per-app module),
   `using eQuantic.UI.Components.Shared` → runtime import (disambiguation is CI-tested). The 2C SDK
   scan gate is GONE (nothing to scan: the library ships in the runtime; user-authored write-once
   components flow through the primary app scan), and the SDK references Components.Shared + Web by
   default (zero-config). Routing now covers every emission path via `RuntimeProvidedTypeScanner`
   (components, STATE classes, STATIC HELPERS — the last two were gaps the live sample exposed).
   More silent-wrong-code fixes en route: expression-bodied `Build` in Core state classes emitted a
   dead `new Container({})` fallback (the whole page tree vanished); static classes with a `Build`
   method were misdetected as components (losing their parameters). **The live proof:**
   `samples/DefaultUIDashboard` `/shared` — a Core stateful page driving a write-once subtree
   (Card/Buttons/type-scale from the generated Photon CSS) through `VisualNodeComponent`, verified
   in a real browser: SSR → hydration → click → `Count: 3`, with the dark-mode `light-dark()` tokens
   resolving to the spec values (`#5ca2e8` Primary dark, 40dp Medium row). State is HOISTED to the
   page (v1: nested component instances rebuild per pass; positional state retention arrives with
   the reconciler slice).
   Unification slice 3 ✅ (2026-07-05): WRITE-ONCE PAGES. A Primitives `StatefulComponent` with
   `[Page]` is a full page with no Core wrapper: the server's SSR scan accepts `UiComponent` types
   and bridges them through `VisualNodeComponent`/WebRealizer (the Server package now references the
   Web realizer — the natural dependency); the client mounts the transpiled class directly
   (`SharedStatefulComponent.mount/hydrate`, already in place since 2C). Metadata unwraps to the real
   page instance (`IHandleMetadata` through the bridge). v1 fence: initial state = field defaults (no
   server-driven state serialization for the shared shape). Also fixed: runtime-provided names no
   longer seed the per-app dependency resolver (a vocabulary `Row` pulled the WEB Row's `Flex` chain
   into pages that never used it). **Live proof:** `DefaultUIDashboard` `/counter-shared` —
   `data-ssr="true"` with `Count: 0` in the raw server HTML, hydrated, clicks re-render to
   `Count: 2`; the page class is verbatim PhotonHost-compatible. SSR covered by
   `WriteOncePageSsrTests` (scan + bridge + token output).
   Migration wave 1 ✅ (2026-07-05): six write-once components authored per the design handoff —
   **Divider** (A7: 1dp Border hairline, leading/middle 16 insets, vertical), **Badge** (B7: dot 8dp,
   count pill 16dp/10-700/"99+", Destructive default + neutral + status pairs; inline until Stack),
   **Chip** (B8: 32dp/Radius.Full/13-600, Filter selected = Primary-subtle + border, Input remove as
   "✕" text until Icon, Tag = Subtle pairs), **ProgressBar** (B14: SurfaceSubtle track + variant fill,
   4/8dp, fill fraction as flex weights round(v·1000) — identical leftover-by-weight on both
   realizers; determinate only until animation), **Avatar** (B6: 24/32/40/56 tiers, initials on a
   deterministic Subtle tint until Image/gradients), **Banner** (B18: status Subtle fill, Radius.Lg,
   12/14 padding, bold lead-in as two Texts until rich spans, ≤2 actions). All: web realizer pins
   (Wave1ComponentTests), native gallery goldens light+dark (Wave1GoldenTests — visually inspected),
   transpiled fixtures + runtime embeds + `@equantic/runtime` exports, vitest execution pins, and the
   `/shared` showcase updated (validated live: Badge/ProgressBar react to the counter state).
   Library: 8 write-once components; ~54 legacy web components remain (waves 2-3 gated on
   Stack/Image/Icon/ScrollView, the interaction system, the reconciler, and text input).
   Primitive: Stack/Positioned ✅ (2026-07-05, spec A3) — the first wave-2 gate opened. Z-order
   composition across all three realizers: the stack sizes to its largest NON-positioned child
   (explicit W/H override), non-positioned children follow the 9-way alignment, Positioned children
   anchor with SIGNED offsets (badge overlay = top −4 / end −4, golden-tested). Native = layout-engine
   pass (paint order = child order); web = single-cell CSS grid (`grid-area: 1/1` per child,
   `place-items` alignment) + absolute anchors on a relative frame, cross-pinned byte-exact with the
   TS lowering. En route: C# switch-expression precedence bug (`(int)a % 3 switch` binds the switch
   to the literal) and the runtime's `Stack` name collision (the vocabulary owns the bare export;
   the data structure is `CollectionStack` + `$eq.collections`). Primitive: Icon ✅ (2026-07-05, spec A10) — the asset pipeline decided and built: a curated `Icons`
   enum (16 glyphs) + `Icon` node with the §07 size WHITELIST enforced at construction (16/20/24/32 —
   arbitrary sizes throw, per spec); glyph path data lives ONCE in the C# `IconRegistry` (24×24
   single-path alpha masks) — the web realizer emits inline `<svg fill=currentColor>` (tint rides
   the color token exactly like text; decorative icons aria-hidden), and the TS lowering consumes
   `icons.generated.ts` (byte-pinned via IconTsGeneratorTests — client path data is never
   hand-written). Native renders a tinted 30% disc placeholder until the W4 atlas (the text-bars
   pattern); the atlas will rasterize from the same registry. RealizedElement gained RawAttributes
   (verbatim, no data- prefix — SVG needs viewBox/d as-is). Primitive: Image ✅ (2026-07-05, spec A11) — explicitly sized slot (layout can't infer extent from
   undecoded sources), Contain/Cover/Stretch fits, per-corner rrect clip, alt semantics (empty =
   decorative). Web = sized `<img>` with object-fit + border-radius (RawAttributes carry src/alt);
   native = SurfaceSubtle placeholder box under the radius until engine texture upload (M4); TS
   mirrors complete. v1 fences: NineSlice, loading/error states, decode crossfade (asset + animation
   systems). Primitive: ScrollView ✅ (2026-07-05, spec A6 v1) — the child lays out UNBOUNDED on the scroll
   axis and clips to the viewport (the new engine clip primitive on native; browser-native
   `overflow: auto` on web, cross axis hidden — cross-pinned). Programmatic `Offset` clamps to the
   scroll extent (golden-tested with a scrolled viewport). v1 fences: platform physics, gesture
   capture, fling and the fading scrollbar pill join the native interaction system; the browser owns
   web physics. ALL FOUR wave-2 primitive gates are now open (Stack/Icon/Image/ScrollView) —
   remaining gates: the interaction system and the reconciler.
   Interaction slice 1: PRESSED ✅ (2026-07-05, spec §01) — pressed is a REAL token swap declared on
   the Pressable (`PressedBackground`), framework-applied per target with ZERO user code: web =
   mechanics in the GENERATED stylesheet (`.eq-pressable:active > :first-child` driven by a
   per-element `--eq-pressed-bg` custom property, Fast-motion transition, tap-highlight neutralized;
   values via `HtmlStyle.CustomProperties`, emitted at the style tail — cross-pinned C#/TS); native =
   `PhotonHost.PressDown/PressUp` (topmost capture, release-outside cancels, disabled swallows) with
   the realizer swapping the first descendant Box fill while held (pressed-button golden). Button
   (filled=Pressed token, Outline/Ghost=SurfaceSubtle, Link=fence) and Chip Filter wire it. Interaction slice 2: FOCUS RING ✅ (2026-07-05, spec §01) — the double ring (2dp Surface gap +
   2dp FocusRing) as an ACCESSIBILITY DEFAULT: every enabled Pressable carries `.eq-pressable`; the
   generated stylesheet adds `:focus-visible > :first-child` box-shadows from the GLOBAL token
   custom properties (keyboard-only, `outline: none`); native gains `PhotonHost.FocusNext()`
   (paint-order traversal, wraps, skips disabled) + `ClearFocus()`, with the realizer stroking the
   two rings OUTSIDE the first descendant Box, following its radius (focus-ring golden). v1 fences:
   Shift+Tab reversal and key events join the input system; hover and gestures next.
   Migration wave 2 ✅ (2026-07-05): SEVEN new write-once components + two fences closed —
   **IconButton** (A13: 32/40/48/56 with §07 icons, required label, Standard/Tonal/Filled/Outline,
   selected glyph swap outline→filled, pressed per kind), **Checkbox** (B11: 22×22 Radius.Xs,
   BorderStrong/Primary+check, whole-row target, Error border; tristate/motion fenced),
   **Switch** (B12: 52×32 track, 26dp thumb via Stack+Positioned by state; slide/drag/E1 fenced),
   **RadioGroup** (B13: 22dp circles, Primary ring+10dp dot, full-width 44dp rows, selection moves
   never clears), **ListItem/List** (B2: leading/content/trailing slots, 15/500+13, min 52/68,
   truncation contract, pressed SurfaceSubtle, LIST owns leading-inset dividers; recycling fenced),
   **Tabs** (B5 controlled: 48dp row, equal-width Fixed, 3dp Primary indicator inset 16 top-rounded,
   Bold/SemiBold weights, pressed cells; Scrollable/translation fenced), **EmptyState** (B17:
   64dp well + Xl icon, 20/600 title, ≤2-line body, actions) and **Skeleton** (B16 static shapes =
   the spec's Reduce Motion behavior; shimmer fenced). Fences closed: **Avatar image tier** (B6
   photo → circle-clipped cover Image) and **Banner dismiss** (20dp close through the Pressable).
   Compiler fixes en route (silent-wrong-code): verbatim identifiers (`@checked`) leaked the `@`
   into emitted JS (parser/identifier strategy → ValueText); the collections name-heuristic
   HIJACKED the vocabulary `Stack` when the semantic model had resolved it (semantic resolution is
   now authoritative in QueueStackStrategy). Library: 19 write-once components (~43 legacy remain,
   gated on text input, overlays, animation, reconciler). Wave-2 tail (same day): **AppBar** (B3:
   56dp bar, 20/600 title single-line, leading slot, ≤3 Standard IconButtons ENFORCED — overflow
   belongs in an ActionSheet; scrolled Surface fill owner-driven until scroll linking) and
   **BottomNavigation** (B4: 3–5 destinations ENFORCED, equal-width full-column targets, 56×26
   active Primary-subtle pill + filled glyph + 11/700 always-visible labels, Badge on the icon's
   top-end via Stack). NavItem became a positional RECORD so the compiler emits its data module.
   More silent-wrong-code compiler hardening: exception type names never import (they lower to
   `new Error`). All embedded in the runtime, gallery
   goldens light+dark inspected, spec pins on every axis.
   Positional reconciler slice 1 ✅ (2026-07-05, W6 — NATIVE): nested stateful components now keep
   their state across parent rebuilds — NO hoisting. Identity = tree PATH (threaded through the
   layout walk) + runtime TYPE + optional Key; `ComponentInstanceStore` (Primitives) retains
   stateful instances per pass (unvisited entries drop on EndPass — their position left the tree),
   and the retained instance ADOPTS the fresh one's configuration through the explicit, AOT-safe
   `UiComponent.AdoptConfig(next)` hook (no reflection — the author decides config vs state; the
   default keeps existing config). `PhotonHost` owns the store and wires retained invalidations.
   Proven: a nested counter holds taps across its parent's SetState rebuilds, adopts fresh config,
   and a key change resets identity/state. Slice 2 = the WEB mirror (store per render root in the
   TS lowering + ambient context, same identity rules) — until then web keeps state hoisting.
   Remaining on this front: reconciler slice 2 (web mirror; supersedes the old note about — unlocks nested stateful
   without hoisting), server-driven initial state for shared pages, and the eventual merge of
   Components.Shared into eQuantic.UI.Components as legacy web components migrate.
4. Legacy web components migrate progressively as they're touched; mixing is safe throughout.
5. Layout conformance harness lands with step 2 and gates every layout feature after it.
