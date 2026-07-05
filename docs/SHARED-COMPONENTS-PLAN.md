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
   and a key change resets identity/state.
   Positional reconciler slice 2 ✅ (2026-07-05, W6 — WEB mirror): the SAME identity contract
   (lowering PATH — threaded through `lowerNode` with the LayoutEngine's exact segment scheme —
   + constructor + optional key) retires state hoisting on the browser. Ownership differs from
   native by necessity (SPA navigation, many render roots): each PAGE INSTANCE owns a
   `ComponentInstanceStore` (TS) and wraps its render in an ambient PASS; nested lowerings
   (`VisualNodeComponent` bridges — rebuilt every pass, so they cannot own retention) JOIN the
   page's pass, and each lowered root takes a unique order-stable prefix (`r0`, `r1`, …) so
   bridges cannot collide on identity. `SharedStatefulComponent` gained `nodeKind='component'`
   (nested instances expand through the store instead of the legacy self-render seam), `key`, and
   an `_invalidationHook` — a retained child's SetState re-renders the HOST page, which reconciles
   back onto the same instance. Transpiled `AdoptConfig(UiComponent next)` works end to end; the
   compiler grew what the contract needs: `is T` patterns emit REAL `instanceof` for
   UiComponent-derived classes (was a bare null-check), pattern types import like constructed
   types, `UiComponent` surfaces in signatures (runtime exports it; the scanner skip now applies
   only to base-list positions). Proven by executing REAL eqc output (NestedHost/NestedChild
   fixtures) in vitest: the nested count survives host rebuilds, adoptConfig carries the fresh
   label, a key change resets state.
   Remaining on this front: server-driven initial state for shared pages, and the eventual merge of
   Components.Shared into eQuantic.UI.Components as legacy web components migrate.
   Animation slice 1 ✅ (2026-07-05, spec §06 — INDETERMINATE LOOP MOTION): the loop-motion
   building block, transform-only per the spec. Vocabulary: `LoopMotion(child, effect, fromX, toX,
   durationMs)` — a layout-transparent wrapper whose offsets are FRACTIONS OF ITS OWN WIDTH (CSS
   translateX(%) and the native offset math share the base), plus `BoxStyle.Clip` (children confine
   to the rrect; chrome never clips — engine PushClip / CSS overflow:hidden). NATIVE: frames are a
   PURE function of the injected clock — `PhotonHost.RenderFrame(builder, timeMs)` samples the
   offset (baked command transform), `RealizeResult.HasActiveMotion` keeps `NeedsRender` hot while
   a loop runs, and `PhotonHost.ReducedMotion` renders at rest AND stops requesting frames;
   deterministic goldens pin t=600ms. WEB: generated `@keyframes eq-slide-x` reads per-element
   endpoints from `--eq-loop-from/to` custom properties at the style tail; duration rides the
   animation shorthand on the `.eq-loop` div; `prefers-reduced-motion` statically disables it in
   the generated stylesheet — all cross-pinned byte-for-byte (C# realizer ⇄ TS lowering).
   CONSUMER: `ProgressBar(value: float? = null)` — null = indeterminate (spec B14): a full-width
   layer sweeps the 30% flex segment -35%→105% on the 1.2s loop inside the clipping Radius.Full
   track. Verified live in the browser (SSR + hydration + running animation at the spec velocity).
   Spinner stays fenced on engine arcs (or native Icon glyphs + a rotate effect).
   Text input ✅ (2026-07-05, spec B9/B10): the `TextEntry` PRIMITIVE — value/placeholder/
   onChanged/onSubmit/onFocusChanged/disabled/obscure + type role — lowers to a REAL chrome-less
   `<input>` on web (the browser owns caret/selection/IME; handlers ride the reconciler's
   input-value convention; `.eq-entry` mechanics generated) and to the W4 one-line placeholder-bar
   frame on native (value = TextPrimary bar, empty = TextMuted placeholder bar; the spec fixes the
   visual contract NOW — caret/IME land at M4 without re-layout). `TextInput` is the FIRST STATEFUL
   library component: focus is INTERNAL state fed by the entry's focus callbacks — SetState swaps
   the 2dp Primary border (padding compensates -1dp) and the positional reconciler retains the
   instance across the form's controlled re-renders while `AdoptConfig` carries each fresh value in
   (the reconciler + AdoptConfig's first production consumer). B9 frame: label above (Label role,
   6dp), Radius.Md container per size (40/48★/56; Small asserts), leading icon slot, helper/error
   line ALWAYS reserved (5dp; error swaps Destructive without layout shift). `SearchField` (B10):
   the 40dp Radius.Full SurfaceSubtle pill, search glyph, BodyM entry, clear-when-non-empty
   routing "" through onChanged (controlled), Enter → onSubmit. Compiler hardening en route: the
   `is not T x` GUARD IDIOM now transpiles correctly (bindings hoist to the enclosing block and
   assign inside the negation — was `fresh is not defined` at runtime), and exception constructors
   pick the semantic `message` parameter for `new Error(...)` (was throwing the param NAME).
   Proven live in the browser: real typing, focus border swap with the DOM focus SURVIVING the
   SetState rebuild (the reconciler preserves the element), clear + submit. v1 fences: keyboard
   hints/trailing slot (M4 IME), Cancel slide-in (state-transition motion), debounce (app-side),
   38% disabled opacity group (engine opacity primitive). Library: 21 components.
   Overlays slice 1 ✅ (2026-07-05, Phase C infra + spec C2): the `Overlay` VIEWPORT LAYER — zero
   in the page flow; native defers the subtree to an overlay pass painted AFTER the page
   (painter's order, hit regions registered last so topmost-last-wins routes taps to the layer;
   the reconciler pass now spans page + overlays — EndPass moved from LayoutEngine to the
   realizer, overlay subtrees lay out against the viewport on stable "ov<i>" paths); web lowers
   to the generated `.eq-overlay` fixed inset-0 stacking layer (mechanics in the stylesheet,
   composition in the component — keep overlays out of transformed subtrees). CONSUMER: `Dialog`
   (C2) — DECLARATIVE presence (`if (_confirming) … new Dialog(…)`), centered E5 card
   min(480, screen−48) via an ordinary centering Column with S6 gutters, Title + BodyM body,
   right-aligned Medium actions (Ghost first; 1-2 enforced), and the scrim as a full-viewport
   Pressable: DISABLED by default (swallows taps — the destructive-confirm contract, matching the
   native "disabled regions swallow" dispatch and a dead click on web), armed with OnDismiss when
   `Dismissible`. Compiler: array-typed properties now import the ELEMENT module
   (`DialogAction[]` minted a phantom `./DialogAction[]`). Proven end to end in the browser:
   open → scrim blocks → action resolves → overlay leaves the tree; dismissible scrim closes.
   v1 fences: enter/exit motion (state-transition system), focus trap/alertdialog (a11y system),
   scroll lock under the fixed layer. Library: 22 components + DialogAction.
   Animation slice 3 ✅ (2026-07-05, spec B15 + B14 — SPINNER + VALUE TRANSITIONS): the spec
   resolved the "arcs gate" itself — B15 is 8 rrect bars (2×5 in the 16dp em-box), opacity
   phase-staggered, 800ms/rev linear, "no arcs exist in the engine". `Spinner` is a leaf node
   (icon em-box sizes, §07 whitelist; color inherits like Icon). NATIVE: a pure f(t) — bar i
   rides the 1→0.3 sawtooth at k=((i−phase·8) mod 8)/8, rotated i·45° about the center; Reduce
   Motion drops the STAGGER only (all bars pulse in place, spec B15) and the spinner always
   reports active motion (functional indicator). WEB: an 8-rect SVG whose rotation phase IS the
   per-bar negative animation-delay over the generated 800ms fade — exact alpha parity with the
   native formula; the 400ms anti-flash appear delay and the reduce-motion delay-zeroing ship in
   the generated stylesheet. VALUE TRANSITIONS (B14): `Flexible.AnimateChanges` lowers to a
   generated-token `flex-grow var(--eq-motion-base) var(--eq-curve-standard)` transition;
   ProgressBar became STATEFUL — AdoptConfig compares each fresh value and a REGRESSION marks the
   next build to snap (forward-only honesty), re-animating afterwards; native weights still snap
   (the transition animator remains the documented fence, now consumer-ready). Compiler: `is`
   pattern bindings HOIST in every statement position (expression/return/local-declaration got
   the same `let` hoisting the if-statement had — `_snap = x is {} v && …` was a runtime
   ReferenceError), via the shared PatternVariableScanner (lambda bodies excluded: their bindings
   scope to the lambda). Native goldens pin the quarter-turn rosette in both modes.
   THE MERGE ✅ (2026-07-05): `eQuantic.UI.Components.Shared` IS now `eQuantic.UI.Components` —
   folder, project, PackageId and namespace. The legacy web set moved to
   `eQuantic.UI.Web.Components` (assembly + namespace; satellites — icon packs, charts, Material,
   Images, Server error pages — just swapped a using and keep compiling; it is deprecated surface
   that dies piece by piece, no compatibility promised). Consequences wired in: eqc's
   RuntimeProvidedTypeScanner routes `eQuantic.UI.Components[.*]` to `@equantic/runtime`; the
   name-reuse/using disambiguation is GONE (one Button); the transpilation harness dropped the
   legacy-dll exclusion — the enclosing-namespace rebinding gotcha is structurally impossible now
   (the legacy set lives outside the `eQuantic.UI.Components` chain); the SDK's defaults reference
   the new `eQuantic.UI.Components` (GeneratePathProperty feeds eqc the write-once sources at
   tools/source) plus `eQuantic.UI.Web.Components` for the transitional web set. Pinned transpiled
   fixtures stayed BYTE-IDENTICAL (namespaces never reach the emit). TailwindDashboard (legacy
   showcase, already failing restore on clean HEAD — missing .local-packages source) left the
   solution; it dies with Web.Components. All suites green; SSR smoke on both the write-once page
   and a legacy page.
   ICON PACKS WRITE-ONCE ✅ (2026-07-05): `IconGlyph(Name, Path, Style, ViewBox, StrokeWidth)` in
   Primitives — target-neutral geometry (fill = alpha mask, stroke = the 2dp-round outline family;
   per-glyph viewBox for foreign grids like FA6's 512). The curated `Icons` enum resolves through
   `CuratedIcons` (paths moved OUT of the web registry, which now just projects); the Icon node
   holds the RESOLVED glyph and accepts any pack glyph directly. Web lowers fill/stroke with byte
   parity across SSR and the TS lowering; native keeps the placeholder disc but now holds the path
   — atlas-ready (W4 rasterizes the same data). All 11 packs REGENERATED at the source:
   scripts/generate-icons.mjs (Iconify JSON) now flattens groups with attribute inheritance,
   converts basic shapes (circle/ellipse/rect/line/polyline/polygon) to path data, drops bounding
   ghosts, and emits one-line IconGlyph catalogs referencing Primitives ONLY (28,285 glyphs across
   Lucide 1805 / Heroicons 1288 / Radix 332 / Tabler 6178 / FA6 solid 1407 + regular 164 + brands
   495 / Phosphor 9161 / Simple 3720 / Bootstrap 2081 / Iconoir 1654; 57 skipped = 0.2%, logged:
   defs/transforms/mixed). The legacy per-pack component/provider/extensions died. Packs are
   OPT-IN references.
   ICON PACKS CLIENT-SIDE ✅ (2026-07-05): the client-dynamic fence closed via CROSS-ASSEMBLY
   CONSTANT INLINING in eqc — a reference to a pack's `static readonly IconGlyph`
   (`LucideIcons.Camera`) transpiles to the CONSTRUCTOR at the use site
   (`new IconGlyph('camera','M…','stroke')`), tree-shaking each pack to only the glyphs referenced
   (no per-pack JS module; the only import is IconGlyph, already runtime-provided). The initializer
   lives in the pack SOURCE, so eqc gained `--ref-sources <file>`: directories whose .cs join the
   compilation SEMANTICALLY (never transpiled) so the semantic model can reach initializers that
   metadata (via --refs) doesn't carry. `InlinedConstantStrategy` (priority above the fallback
   member access) converts the initializer with the PACK TREE's own semantic model and registers
   IconGlyph for import. The SDK auto-discovers referenced pack source dirs (package `tools/source`
   or dev project dir; standard Components dir excluded) and writes the `--ref-sources` list — zero
   consumer config. A latent RECONCILER bug surfaced and was fixed: SVG elements are `SVGElement`,
   not `HTMLElement`, so the update/cleanup/HYDRATE guards (`instanceof HTMLElement`) silently
   skipped every client-side icon — attributes never updated on re-render and icons failed
   hydration validation; all four guards widened to `Element`. Proven end to end: compiler inline
   unit tests, the SDK feeding 230 Lucide sources, inlined glyphs through the Bun bundle, SSR, and
   a vitest client re-render swapping the DOM `<path d>` + fill on setState.
   THEMING = IAppTheme; MATERIAL 3 write-once ✅ (2026-07-05, slice 1): the sustainable customization
   mechanism is that theming IS providing an `IAppTheme` — the SAME act whether you use Material or
   brand your own; the realizers already consume it generically. Reformulated `Primitives.IAppTheme`
   (additive): `Variant.Tertiary` (M3's tertiary role; Photon tones it from Info) and a
   `Shape(ShapeScale)` accessor so CORNER SHAPE is theme-driven — radii moved OUT of the static
   `Radius` class INTO the theme; every write-once component (and the Button size table's shape) now
   reads `context.Theme.Shape(scale)`, both realizers + the web CSS gen + the TS design-system gen
   source radii from the theme, and eqc transpiles `theme.shape('medium')` cleanly. Photon returns
   its own ladder (values IDENTICAL → zero golden churn, proving the refactor transparent). New
   `eQuantic.UI.Material` package (Primitives ONLY, target-neutral) = `MaterialTheme : IAppTheme`
   with the fixed M3 BASELINE (seed #6750A4): M3 color roles → VariantColors, M3 type scale →
   TypeRole, M3 shape scale → ShapeScale, M3 elevation. The entire legacy Material (Core.Theme CSS
   classes, `M3.cs`, per-component CSS themes, MaterialDashboard sample) was DELETED — it dies with
   Web.Components. Proven: the same shared components render M3 on the web realizer (primary #6750A4,
   containers, 12/16/28 shape — cross-pinned) AND as native Photon pixels (Material gallery goldens,
   light+dark — unmistakably M3, swap-the-theme only). Fences (later slices): dynamic color from a
   seed (HCT tonal palettes) and the app-wide theme-selection wiring (SSR bridge + client boot + the
   per-app theme-TS delivery so an app SELECTS Material for hydration).
   Animation slice 2 ✅ (2026-07-05, spec A1/B16 — GRADIENT + SHIMMER): `BoxStyle.Gradient` exposes
   the engine fence's exact gradient primitive — `LinearGradient(From, To, Direction)`, two TOKEN
   stops on a straight axis (ToRight/ToBottom), drawing OVER the solid background (CSS
   background-image/background-color composition; native emits a second FillRRect with
   Paint.Linear across the bounds). New theme token `SurfaceHighlight` (translucent white 55%/7%)
   flows reflectively into the generated TS/CSS. `LoopMotion.HideAtRest` completes the Reduce
   Motion story: decorative loops disappear entirely (native skips the subtree AND reports no
   active motion; web adds `.eq-loop-rest-hidden { visibility: hidden }` to the generated media
   query) while positional loops keep a still frame. CONSUMER: Skeleton gained the spec B16
   shimmer — a split 2-stop gradient glint (transparent→highlight | highlight→transparent, the
   symmetric band within the 2-stop fence) sweeping -100%→100% on the 1.4s clock inside the
   placeholder's clipping rrect; Reduce Motion = the plain SurfaceSubtle placeholder, exactly as
   speced. Goldens at t=700ms both modes; verified live in the browser (mirrored computed
   gradients, spec sweep velocity, rest-hidden class).
4. Legacy web components migrate progressively as they're touched; mixing is safe throughout.
5. Layout conformance harness lands with step 2 and gates every layout feature after it.
