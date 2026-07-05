# eQuantic.UI Native — Proprietary GPU Engine Plan (codename: "Photon")

> Track N of `ROADMAP.md` (a parallel track, independent of web Phases 3–7). Same shape as
> `docs/PHASE-2-CLIENT-ROUTER-PLAN.md`: why → decisions → architecture → workstreams → milestones →
> risks → exit criteria.
>
> **Strategic decision (recorded 2026-06-10):** eQuantic.UI's mobile story starts **directly with a
> proprietary GPU backend** — native Metal (iOS/macOS) and native Vulkan (Android) with **offline,
> precompiled shaders** — *not* with a Skia-based tier. The engine **is** the product differentiator;
> a Skia tier would build the framework around a foreign canvas and force an Impeller-style
> compatibility rewrite later (the exact trap Flutter paid for). The cost of this decision is a longer
> time-to-first-pixel; the plan below is structured so that cost is survivable (golden-image harness
> and device CI from day 0, ruthlessly scoped v1 primitive set).

## The Vision

Bring the eQuantic.UI authoring model — C# components, `Build(context)`, `SetState`, theming — to
**mobile (iOS/Android first)**, rendered by our own GPU engine:

1. **100% C#, zero transpiler** — on native targets C# runs as native code (AOT). The C#→JS transpiler
   is a *web* concern; native reuses the authoring surface, not the compilation pipeline.
2. **Proprietary rendering, Impeller-class principles** — precompiled shaders (zero runtime shader
   compilation, zero first-frame jank), a small fixed set of pipeline state objects, a purpose-built
   rasterizer for *UI* (not a general 2D graphics library).
3. **Same product philosophy as the web SDK** — `<Project Sdk="eQuantic.UI.Native.Sdk">`, embedded
   toolchain (the shader compiler ships inside the SDK packages exactly like Bun does today), zero
   config, no Node/npm/Xcode-scripting rituals beyond what Apple/Google force.
4. **Conformance culture carried over** — the web transpiler is guarded by a Bun-vs-.NET conformance
   harness; the engine is guarded by a **golden-image harness** (render → compare pixels vs reference,
   per backend, on real devices in CI) from the very first triangle.

### What we are explicitly NOT building (v1 scope fence)

- Not a general 2D vector graphics library (no SVG parity, no path booleans, no dashed strokes in v1).
- Not a game engine (no 3D scene graph, no physics; 2D UI with 2.5D transforms only).
- Not a Skia/HTML/canvas compatibility layer.
- Not desktop-first (macOS comes almost free via Metal; Windows/D3D12 is a later decision).
- Not text shaping from scratch (HarfBuzz) nor font rasterization from scratch (FreeType) — the
  proprietary part is everything *above* those: atlas, layout, paragraphs, and the GPU pipelines.

## Prior art — what each proof point teaches us

| Project | What it proves | Lesson we take |
|---|---|---|
| **Impeller** (Flutter) | Precompiled-shader UI renderer at scale on Metal+Vulkan | The thesis (no runtime shader compilation; small fixed PSO set; render-pass-oriented design). Also the cautionary tale: it was born shackled to Skia semantics — *we avoid that by having no Skia tier*. |
| **GPUI** (Zed) | ~2 engineers built a bespoke Metal UI engine in ~1 year | SDF-first rasterization covers the UI-90% with very few pipelines; a UI engine is much smaller than a graphics library. |
| **Rive Renderer** | Small team, bespoke GPU vector renderer | The *general* vector problem is the expensive one — keep it out of v1. |
| **Slug** (Lengyel) | GPU text by ~1 person | Text on GPU is tractable when scoped; quality tail is in shaping/fallback (which we don't rewrite). |
| **Vello** | Compute-centric analytic AA | State of the art but research-adjacent; not v1. Revisit for v2+ paths. |
| **Avalonia** | Full C# UI framework, own compositor | C# is viable for the whole stack; layout/composition in C# is a solved problem. (They sit on Skia — the tier we are deliberately skipping.) |

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│ Authoring (C#) — UNCHANGED MODEL                                    │
│   StatefulComponent/StatelessComponent · Build(context) · SetState  │
│   IAppTheme theming · DI/services                                   │
├─────────────────────────────────────────────────────────────────────┤
│ SHARED components — eQuantic.UI.Components + .Primitives (once!)    │
│   Box/Text/Image/Button/… authored against abstract nodes +         │
│   typed tokens; realized per target (SHARED-COMPONENTS-PLAN.md)     │
├─────────────────────────────────────────────────────────────────────┤
│ NATIVE REALIZER — eQuantic.UI.Native.Components                     │
│   abstract nodes → Photon primitives · typed styles → values        │
│   Layout: flex/stack/absolute (own C# implementation)               │
├─────────────────────────────────────────────────────────────────────┤
│ Render tree + Reconciler — eQuantic.UI.Native.Framework             │
│   Primitive tree (RRect/TextRun/Image/Shadow/Clip/Layer/Transform)  │
│   Keyed-LIS reconciler (C# port of the web algorithm)               │
│   Compositor: layers, damage/dirty-region tracking                  │
├─────────────────────────────────────────────────────────────────────┤
│ ENGINE CORE ("Photon") — eQuantic.UI.Native.Engine        ★ the IP  │
│   Display list → render passes → draw batches                       │
│   SDF pipelines (rrect/border/shadow), gradient, image, glyph       │
│   Atlas manager (glyphs, images) · PSO registry (fixed, precompiled)│
├─────────────────────────────────────────────────────────────────────┤
│ HAL / RHI — IDevice·ISwapchain·ICommandList·IPipeline·IBuffer·ITex  │
│   ┌───────────────┬────────────────┬──────────────────────────────┐ │
│   │ Metal backend │ Vulkan backend │ Reference backend (CPU)      │ │
│   │ iOS/macOS     │ Android        │ golden tests only, NOT Skia  │ │
│   └───────────────┴────────────────┴──────────────────────────────┘ │
├─────────────────────────────────────────────────────────────────────┤
│ Shader toolchain (build-time, embedded like Bun)                    │
│   Slang sources → SPIR-V (Vulkan) + MSL/metallib (Metal), offline   │
├─────────────────────────────────────────────────────────────────────┤
│ Platform shells — eQuantic.UI.Native.{Ios,Android}                  │
│   iOS: UIWindow+CAMetalLayer, CADisplayLink, UITouch, NativeAOT     │
│   Android: SurfaceView/ANativeWindow, Choreographer, MotionEvent    │
│   Text input (IME), lifecycle, accessibility bridge                 │
├─────────────────────────────────────────────────────────────────────┤
│ Text stack: HarfBuzz (shaping) + FreeType (raster) → glyph atlas    │
└─────────────────────────────────────────────────────────────────────┘
```

### Package layout (mirrors the existing self-contained packaging)

- `eQuantic.UI.Native.Engine` — engine core + HAL interfaces (pure C#, no platform code).
- `eQuantic.UI.Native.Engine.Metal` / `.Engine.Vulkan` / `.Engine.Reference` — backends.
- `eQuantic.UI.Native.Framework` — primitive tree, reconciler, compositor, layout.
- `eQuantic.UI.Primitives` — SHARED: value types, design tokens, typed styles, abstract nodes (zero deps).
- `eQuantic.UI.Native.Components` — the NATIVE REALIZER (abstract → Photon) + component renderers; the
  component library itself is the shared `eQuantic.UI.Components` (see `SHARED-COMPONENTS-PLAN.md`).
- `eQuantic.UI.Native.Ios` / `.Android` — platform shells.
- `eQuantic.UI.Native.Toolchain.{Osx64,OsxArm64,Win64,Linux64}` — the **embedded shader compiler**
  (Slang + SPIR-V tools), zipped per platform exactly like `eQuantic.UI.Runtime.*` embeds Bun today.
- `eQuantic.UI.Native.Sdk` — MSBuild SDK: compiles `.slang` → `metallib`/`.spv` at build, packs shader
  libraries as embedded resources, wires AOT settings. Consumer experience: one `Sdk=` line.

## Key technical decisions

- **D1 — Native Metal + native Vulkan; no MoltenVK, no ANGLE, no Skia.** Two first-class backends.
  Every translation layer reintroduces exactly the indirection this track exists to remove.
  macOS rides the Metal backend nearly for free (dogfooding + fast local iteration).
- **D2 — SDF-first rasterization** for the v1 primitive set: rounded rects (per-corner radii),
  borders, and shadows are evaluated analytically in the fragment shader (signed distance + coverage),
  giving perfect AA with a handful of pipelines. Box shadows use the analytic Gaussian-of-rrect
  approximation (Evan Wallace's technique — closed-form, no blur pass for the common case).
  Arbitrary paths (stencil-then-cover tessellation) are **v2**, not foundation.
- **D3 — Slang as the single shader source language**, compiled **offline at build time**:
  Slang → SPIR-V for Vulkan; Slang → MSL → `metallib` (via `metal`/`metallib` toolchain or
  runtime-compiled-once-at-**build** on CI) for Metal. Zero runtime shader compilation, zero
  first-run jank. The compiler binaries ship inside `Native.Toolchain.*` packages (embedded-Bun
  philosophy). Fallback path if Slang misbehaves: HLSL + DXC → SPIR-V → spirv-cross → MSL (the
  boring, battle-tested pipeline Impeller-adjacent projects use).
- **D4 — One main render pass per frame** + bounded offscreen passes only where semantics force them
  (saveLayer with opacity/blend, genuine Gaussian blur via dual-Kawase). A deliberately tiny usage
  surface of each GPU API is the #1 mitigation against the Android driver zoo.
- **D5 — Fixed, enumerable PSO registry.** Target **≤ 24 pipelines total** in v1 (rrect/solid,
  rrect/gradient-linear, rrect/gradient-radial, border, shadow, image, image-9slice, glyph-alpha,
  glyph-color(emoji), clip variants, layer composite, blur up/down). All created at engine init from
  precompiled libraries; creating a pipeline at draw time is a **bug by definition** (asserted).
- **D6 — Text = HarfBuzz shaping + FreeType rasterization + our atlas.** FreeType everywhere (not
  CoreText/platform rasterizers) so **the same text renders the same pixels on every backend** — a
  hard requirement for the golden-image harness. Alpha-mask glyphs at discrete sizes in v1 (UI text
  is 8–40px; atlas pressure is manageable); MSDF is a v2 upgrade if scale-independence pays for
  itself. Color emoji via COLR/bitmap strikes → color atlas page. Both libs statically linked,
  P/Invoked via thin C shims (HarfBuzzSharp exists and is usable; FreeType gets a slim binding).
- **D7 — Reference CPU backend behind the HAL** — renders the same display list scalar-slow on CPU.
  It exists **only** for golden tests and CI-without-GPU; it is test infrastructure, not a product
  tier, and it is not Skia. (This is how we keep "no Skia tier" honest while still being able to
  bisect "engine bug vs driver bug".)
- **D8 — Golden-image conformance harness from day 0.** Every primitive/feature lands with golden
  cases (rendered per backend, fuzzy-compared with per-channel tolerance + dssim-style metric to
  absorb GPU rounding). Device farm CI (a small physical rack is fine initially: 2 iPhones, 3–4
  Androids covering Adreno/Mali/Xclipse) runs the suite on merges. This is the same philosophy as
  `eQuantic.UI.Conformance.Tests`, applied to pixels.
- **D9 — C# with NativeAOT on iOS** (required — no JIT on iOS) and **NativeAOT-preferred on Android**
  with Mono AOT as the M0 decision gate fallback (measure startup/size/stability on .NET 10 and pick;
  the engine code is identical either way). Engine hot path is **allocation-free in steady state**:
  structs + `Span<T>`, arena-pooled command lists, ring-buffered (triple) per-frame GPU buffers,
  zero boxing, zero LINQ in the frame loop. CI enforces it: an allocation-regression test asserts
  0 bytes allocated across N steady-state frames.
- **D10 — Threading model v1: platform thread + render thread.** Input/lifecycle on the platform
  thread; build/diff/layout/encode/submit on one render thread (GPUI-style simplicity). Splitting
  build vs raster into two threads (Flutter-style) is a v2 optimization gated on real traces, not
  adopted preemptively.
- **D11 — Typed styles, not CSS.** Tailwind/ClassName is a *web* styling plane and does not exist on
  native. Native components take typed style props (`Style { Padding, Background, CornerRadius, … }`);
  `IAppTheme`/variants/sizes carry over **conceptually** (theme objects returning typed styles instead
  of class strings). `StyleBuilder`(string CVA) stays web-only. This is the largest authoring-surface
  divergence and is stated here so nobody "discovers" it in month 9.
- **D12 (revised 2026-07-03) — One shared authoring layer; sibling trees only at REALIZATION.**
  Components are written ONCE, in a shared assembly, against an abstract visual vocabulary + the typed
  tokens (`eQuantic.UI.Primitives`); per-target REALIZERS lower the abstract tree — web to
  `HtmlElement`/DOM + CSS (HtmlElement keeps its 1:1 DOM mirror as the realizer's target vocabulary),
  native to Photon primitive trees. This PROMOTES the old "Track N2 façade (later)" to the foundation —
  see `docs/SHARED-COMPONENTS-PLAN.md` for the full architecture (styling plane, escape hatches,
  layout-parity harness, migration).

## v1 primitive set (the exact fence)

**In:** solid rect · rrect w/ per-corner radii · borders (uniform width) · linear/radial gradients ·
images (contain/cover/stretch, 9-slice) · box shadows (analytic rrect Gaussian) · clip rect/rrect ·
opacity groups (saveLayer) · Gaussian blur (dual-Kawase, for backdrop/overlay use) · 2D affine
transforms (translate/scale/rotate) · scroll containers (translated clip + fling physics) · shaped
text runs (LTR + basic RTL via HarfBuzz), color emoji · hit-testing for all of the above.

**Out (v2+):** arbitrary paths & strokes (dash/join/cap), path booleans, mesh/custom-shader user API,
backdrop filters beyond blur, non-src-over blend modes (except the few composite ops layers need),
3D perspective transforms, video surfaces (platform-composited later via overlay strategy).

## Frame pipeline (steady state)

```
vsync (CADisplayLink / Choreographer)
  → drain input queue → dispatch events (hit-test cached tree)
  → rebuild dirty components (Build) → reconcile primitive tree (keyed-LIS)
  → layout (flex/stack; only dirty subtrees)
  → compositor: damage rects, layer invalidation
  → encode display list → batch by pipeline → write ring buffers
  → 1 main pass (+ bounded offscreen passes) → submit → present
```

**Budget (120 Hz = 8.33 ms):** input+build+diff ≤ 2 ms · layout ≤ 1 ms · encode ≤ 1.5 ms · GPU ≤ 3 ms ·
slack ≥ 0.8 ms. Budgets are CI-tracked on reference devices from M2 onward (a perf harness scene set,
regression-gated like the test suites).

## Workstreams

- **W1 — HAL + backends.** Define the RHI (`IDevice`, `ISwapchain`, `ICommandList`, `IPipeline`,
  `IBuffer`, `ITexture`, `ISampler`, fences). Metal backend (Obj-C interop via generated bindings or
  thin C shims), Vulkan backend (P/Invoke via Silk.NET-style bindings or our own slim layer — decide
  at M0 spike; owning the binding is preferred long-term), Reference backend (scalar CPU).
- **W2 — Shader toolchain + SDK.** Slang sources in-repo; MSBuild targets compile to SPIR-V + metallib
  at build; toolchain binaries packed per-platform (`Native.Toolchain.*`, embedded-Bun pattern);
  shader libraries embedded as resources; `Native.Sdk` orchestrates + wires AOT flags.
- **W3 — Engine core.** Display list format (flat, struct-of-arrays, arena-allocated), batcher,
  PSO registry, SDF pipelines (rrect/border/shadow), gradients, images, atlas manager (shelf packing,
  page eviction), layer/offscreen management, dual-Kawase blur.
- **W4 — Text.** HarfBuzz + FreeType integration (static link + shims), font discovery per platform
  (system fonts + bundled fonts), glyph atlas (alpha + color pages), run/paragraph layout (wrapping,
  ellipsis, max-lines; bidi via minimal UBA subset with ICU-less bidi lib decision at M1), caret/
  selection metrics (needed for inputs at M4).
- **W5 — Platform shells.** iOS host (CAMetalLayer sizing/rotation, DisplayLink pacing, touch,
  keyboard/IME hooks, app lifecycle, NativeAOT project template), Android host (NativeActivity or
  thin Java host + ANativeWindow, Choreographer, MotionEvent, IME hooks, lifecycle/surface loss —
  Vulkan swapchain recreation is a first-class test case).
- **W6 — Framework + shared components (native realizer).** C# port of the keyed-LIS reconciler over
  the primitive tree; flex/stack/absolute layout engine (own C#, Yoga-binding as fallback plan only);
  gesture system (tap, drag, fling with platform scroll physics curves); the NATIVE REALIZER for the
  shared abstract vocabulary (`docs/SHARED-COMPONENTS-PLAN.md`): Box, Text, Image, Button, ScrollView,
  TextInput(M4), List with recycling(M3) — authored once in the shared `eQuantic.UI.Components`,
  lowered here to Photon primitives; typed styles + tokens come from `eQuantic.UI.Primitives`.
- **W7 — Golden harness + device CI.** Golden runner app (renders case manifests, captures via
  readback, compares), fuzzy diff metrics, per-backend golden storage, device farm wiring, allocation
  regression tests, frame-budget perf harness.
- **W8 — Samples + dogfood.** Native Counter (M2), native DefaultUIDashboard subset (M3), a real
  small app dogfooded internally (M4+). Each sample doubles as an integration test target.

## Milestones

- **M0 — "Triangle + harness" (months 0–3).** RHI defined; Metal + Vulkan + Reference backends clear
  the screen and draw a solid/SDF rect **on physical devices**; Slang toolchain compiles offline and
  is embedded + invoked by `Native.Sdk`; golden harness runs the first 10 cases on all 3 backends in
  CI; NativeAOT iOS app boots; Android AOT decision made (gate: startup < 400 ms, package delta
  acceptable, zero AOT-related crashes in a 24 h monkey run).
  - Exit: `[ ]` same 10 golden images pass on Metal, Vulkan, Reference within tolerance.
- **M1 — Primitive set v1, static (months 3–6).** All v1 primitives except text; clips, layers,
  transforms, blur; compositor with damage tracking; ~150 golden cases; batching keeps a full static
  dashboard mock under 30 draw calls.
  - Exit: `[ ]` a pixel-faithful static replica of the DefaultUIDashboard home renders identically
    (per goldens) on both GPU backends.
- **M2 — Text + motion (months 6–9).** Shaped text w/ wrapping + emoji; images; scroll containers
  with fling at 120 Hz on reference devices; frame-budget harness in CI; **native Counter sample
  interactive** (touch → SetState → reconcile → render).
  - Exit: `[ ]` Counter runs on iPhone + 2 Android GPUs with zero steady-state allocations and no
    frame > 8.33 ms across a scripted 60 s interaction.
- **M3 — Framework (months 9–12).** Reconciler + flex layout complete; gestures; recycling list;
  Dashboard subset (nav + cards + table + counter page) running from the **same component authoring
  style** as web (`Build`/`SetState`).
  - Exit: `[ ]` Dashboard subset demo on both platforms; layout conformance suite (flex cases vs
    expected geometry) green.
- **M4 — Hardening (months 12–15).** TextInput + IME basics; accessibility bridge v1 (semantics tree
  → UIAccessibility / AccessibilityNodeInfo: labels, roles, focus order, tap actions); lifecycle
  (backgrounding, surface loss, memory pressure → atlas/page eviction); device matrix widened
  (Adreno/Mali/Xclipse/PowerVR); crash-free monkey runs.
  - Exit: `[ ]` VoiceOver + TalkBack can navigate and activate the Dashboard demo.
- **M5 — v1 preview (months 15–18).** SDK packaging polish (`Sdk=` one-liner, templates), docs +
  wiki (architecture, styling divergence D11, supported set), perf budgets published, preview NuGets.
  - Exit: `[ ]` an external developer builds & runs a new native app from template in < 10 minutes
    without touching Xcode/Gradle beyond signing.

## Risks & mitigations

| Risk | Severity | Mitigation |
|---|---|---|
| Android Vulkan driver zoo (Adreno/Mali quirks) | High | D4 tiny API surface; fixed PSO set; device farm from M0; Reference backend to bisect engine-vs-driver; workaround registry per-GPU. |
| Text quality/complexity tail (bidi, IME, emoji) | High | Don't rewrite shaping (HarfBuzz); FreeType for identical pixels; v1 fences (no subpixel, discrete sizes); IME deferred to M4 and scoped to "good", not "perfect". |
| Time-to-first-pixel morale (no product visible for months) | Medium | Golden harness makes progress *visible and demoable* from week 2; macOS Metal dogfood window early; M2 Counter is the "it's real" moment. |
| Skill concentration (needs real graphics engineers) | High | This plan assumes 2–3 senior graphics/systems engineers; do not staff it as a side quest of the web track. The RHI/PSO/atlas design reviews are where seniority pays. |
| Scope creep toward "general 2D library" | High | The v1 fence in this doc is normative; anything path/filter/blend-exotic goes to the v2 list by default. |
| NativeAOT edge cases (interop, trimming) | Medium | Compile-first philosophy already avoids reflection; interop via source-generated P/Invoke; trim-safe from day 0; Mono AOT fallback documented for Android. |
| Web/native authoring divergence confuses users | Medium | D11/D12 stated up front; docs present Native as a sibling target with the same component *model*; convergence façade is an explicit later track (N2), not an implicit promise. |
| Golden flakiness across GPUs | Medium | Fuzzy compare (tolerance + perceptual metric), per-backend goldens, quantized color ramps in test scenes. |

## Effort & sequencing honesty

2–3 strong graphics/systems engineers. W1+W2 serialize first (M0); W3/W4 parallelize after; W5 starts
at M0 (shells are needed to run on devices); W6 can start as pure-C# work immediately (reconciler/
layout/components against the Reference backend). 12–18 months to M5 preview is realistic **only** with
the v1 fence enforced; the classic failure mode is spending month 4 on dashed strokes.

## What carries over from today's codebase — and what doesn't

**Carries over:** the component authoring model (`Build`/`SetState`, props/children patterns), the
keyed-LIS reconciler *algorithm* (TS → C# port), `IAppTheme`/variant/size concepts (as typed styles),
the DI/service model, the conformance-harness culture (reborn as golden images), the embedded-toolchain
packaging pattern (Bun → Slang/SPIR-V tools), the SDK-as-single-line consumer experience.

**Does not carry (by design):** the C#→JS transpiler and `$eq` runtime (web-only), `HtmlElement`/DOM
mirroring (web-only, per the existing 1:1-DOM design rule), Tailwind/ClassName styling plane (D11),
Bun and the JS bundling chain, the TypeScript runtime.

## Open questions (tracked decisions, not surprises)

1. Engine codename/branding — "Photon" is a placeholder; decide before public docs.
2. GPU binding strategy — Silk.NET vs owned slim bindings (M0 spike decides; owning is preferred).
   **Metal side answered (2026-07-04):** the spike drives the whole pipeline through ~100 lines of
   typed `objc_msgSend` P/Invoke (`LibraryImport`, one extern per call shape — the arm64 ABI needs
   typed trampolines, incl. HFA `MTLClearColor` and by-reference `MTLRegion`) with **no** binding
   framework, no C shims, and ±1 LSB parity vs the Reference. Owned slim bindings are confirmed for
   Metal; Vulkan (C ABI, simpler interop than Obj-C) inherits the same approach unless the Android
   spike surfaces a blocker. Lifetime management (autorelease pools, retain/release) is the remaining
   binding-layer work item — the spike deliberately leaks per-process.
3. Android AOT flavor — NativeAOT vs Mono AOT (M0 gate, criteria above).
4. Bidi implementation — minimal UBA subset in C# vs small C lib (M1).
5. MSDF glyphs — v2 candidate, only if zoom/scale-independent text proves needed.
6. Desktop targets — macOS is nearly free (dogfood); Windows/D3D12-or-Vulkan decision post-M5.
7. **WebGPU as a 4th backend** (far future): the same HAL could target WebGPU — which would let the
   *native* engine render on the web without the DOM, converging the two stacks. Deliberately parked;
   noted so the HAL design doesn't preclude it.
8. ~~Track N2 — cross-target component façade — after M5.~~ **Superseded (2026-07-03): promoted to the
   foundational architecture** — components are authored once in a shared assembly and realized per
   target from day one. See `docs/SHARED-COMPONENTS-PLAN.md` and D12 (revised).

## Status log

- **2026-06-10 — M0 kickoff landed** (W3/W7 slices + the engine-facing seam of W1):
  `eQuantic.UI.Native.Engine` (geometry with y-down convention, CSS-rule radius normalization,
  sRGB⇄linear color model, flat heap-free `Paint`/`DrawCommand`/`DisplayList` + builder with baked
  transforms, and `Sdf.cs` — the **normative** per-corner rrect/stroke/coverage math the shaders will
  transliterate); `eQuantic.UI.Native.Engine.Reference` (scalar CPU rasterizer: linear premultiplied
  blending, sRGB-interpolated gradients, inverse-transform sampling, sRGB readback — plan D7);
  golden-image harness (dependency-free PNG codec over `ZLibStream` + CRC32, `EQ_UPDATE_GOLDENS=1`
  regen flow, actual/×8-diff artifacts on failure — plan D8) with the **first 14 golden cases**
  committed (clear, rects, blending, per-corner/overflow rrects, circle, borders, gradients,
  rotate/scale transforms, and a composed UI card) plus 19 unit tests over the normative math.
  The fine-grained RHI (`IDevice`/`ICommandList`/…) is deliberately deferred to the Metal spike so
  real GPU code shapes it (noted on `IRenderBackend`). Next: Metal offscreen spike on macOS
  (objc-interop decision), Slang toolchain spike, then the same 14 goldens running on Metal.

- **2026-07-03 — Design System + shared architecture landed.** The Photon Design System handoff
  (Claude Design) is preserved at `docs/design/Photon-Design-System.dc.html` and implemented as the
  SHARED token layer: `eQuantic.UI.Primitives` (new, zero-dep) holds `Color`, the full token set
  (§01–§08: paired light/dark `ColorToken`s, `VariantColors` with Pressed-as-token, type scale with
  Dynamic Type clamps, spacing/radius/icon/touch scales, analytic `ShadowSpec` elevation, motion), the
  `IAppTheme` contract + `PhotonTheme`, and the target-neutral Button style resolver (variant × size ×
  state; derived Outline/Ghost/Link; disabled = 38% group; focus double-ring). The engine now DEPENDS
  on Primitives (dependency inverted); `eQuantic.UI.Native.Components` is the native REALIZER
  (`ButtonRenderer` → display list). Spec fidelity is tested: token value pins, **recomputed WCAG
  contrast for every claimed pair**, resolver rules, and golden button matrices (9 variants × 4
  states, light + dark) rendered through the engine. Same day: **D12 revised / N2 promoted** —
  components are authored once in a shared assembly and realized per target
  (`docs/SHARED-COMPONENTS-PLAN.md`). Native suite: 94 tests, 16 goldens.

- **2026-07-03 — Write-once core landed** (abstract vocabulary + C# flex + native realizer — the heart
  of `SHARED-COMPONENTS-PLAN.md`, W6 slice 2). `eQuantic.UI.Primitives` gains the ABSTRACT NODE
  vocabulary (specs A1/A2/A4/A8/§08): `Box`+`BoxStyle` (explicit>Fill>Hug sizing, min/max, padding —
  no margin by design, token background, per-corner radius, uniform INSIDE border), `Row`/`Column`
  (gap-owned flex; Row cross-defaults Center, Column Stretch), `Flexible(n)`, `Spacer` (flex/fixed),
  role-driven `Text` (maxLines/ellipsis), `Pressable` (48dp hit contract) — plus `EdgeInsets`,
  `SizeValue`, `MainAlign`/`CrossAlign`, and `CornerRadii` moved down from the engine. New
  `eQuantic.UI.Native.Framework`: the C# flex `LayoutEngine` (leftover-by-weight, SpaceBetween,
  stretch, hug-with-flexibles takes finite extent for CSS parity, flexibles collapse only when
  unbounded, and the spec A2 TRUNCATION CONTRACT — text shrinks to ellipsis before any sibling is
  pushed; fixed children never shrink) and the `ITextMeasurer` seam with a deterministic
  `ApproximateTextMeasurer` stand-in (W4's HarfBuzz/FreeType plugs in here; goldens regenerate then by
  design). `eQuantic.UI.Native.Components` gains `PhotonRealizer` (abstract tree → layout → tokens
  resolved per mode → display list; inside-stroke borders per the fence — `ButtonRenderer` fixed to
  match; text renders as placeholder line bars until W4; Pressable hit rects expanded to ≥48dp).
  Tests: 17 flex geometry cases (the seed of the cross-target layout conformance suite) + goldens
  `abstract-flex-gallery` and the WRITE-ONCE `abstract-card` (title, avatar, identity, filled +
  outline buttons) in light + dark — one abstract tree through the whole stack. Native suite: 114
  tests, 19 goldens. Next: the shared Button/Text components authored on this vocabulary, the web
  realizer (TS lowering + token→CSS generation), Metal spike.

- **2026-07-03 — Shared components + component model + host loop landed.** The SHARED component model
  lives in `eQuantic.UI.Primitives`: `UiComponent` IS a `VisualNode` (components compose into any
  tree; the layout engine expands `Build(ComponentContext)` INLINE), with `StatelessComponent` /
  `StatefulComponent` (+`SetState` → `StateInvalidated`) mirroring the web authoring shape;
  `ComponentContext` is deliberately MODE-FREE — components author tokens, never resolved colors
  (`ColorToken.WithOpacity` covers the disabled 38% group at token level). New
  **`eQuantic.UI.Components.Shared`** (staging assembly, refs Primitives ONLY — merges into
  `eQuantic.UI.Components` when the web realizer lands): **Button** (spec A12 — Pressable → Box → Row
  → Text, size-table metrics via a system `Text.StyleOverride`, derived variants, MinWidth 64,
  Expand, disabled swallows presses; pressed/focus visuals await the interaction system) and **Card**
  (B1 — Elevated/Outlined/Filled, Radius.Lg, S4 padding; Elevated renders a border fallback until the
  engine's E1 shadow lands). `PhotonHost` (native realizer package) closes the v1 frame loop:
  retained root, `SetState` → `NeedsRender`, `Tap` dispatch to §08-expanded hit regions (topmost wins,
  disabled swallows). Layout fix en route: cross-axis **Stretch no longer overrides an explicit cross
  size** (CSS parity). Proven end-to-end by the native **Counter app test** (tap → SetState → rebuild;
  state bar 24→96dp over 3 taps) and goldens `shared-buttons`, `counter-initial`,
  `counter-after-3-taps`. Native suite: 129 tests, 22 goldens. Next: web realizer (TS lowering +
  token→CSS — the same Button on both targets), Metal spike.

- **2026-07-04 — Web realizer slice 1 landed** (`SHARED-COMPONENTS-PLAN.md` migration step 3).
  New `eQuantic.UI.Web`: `WebRealizer` lowers the SAME abstract trees the native realizer turns into
  Photon pixels to `HtmlElement`/DOM (SSR path): Box→div with `box-sizing:border-box` (inside-border
  parity), Row/Column→CSS flex (Flexible → `flex: n 1 0%` = native leftover-by-weight; `min-width:0`
  keeps the truncation contract), Text→role-classed span (+system StyleOverride inline), Pressable→
  neutralized `<button>` (aria-label, disabled swallows, OnPressed→OnClick). Colors lower as
  `light-dark()` straight from tokens — the DOM stays MODE-FREE like the abstract tree.
  `PhotonCssGenerator` produces the NORMATIVE embedded stylesheet from the tokens (custom properties,
  `.eq-type-*` role classes, `.eq-elevation-*` shadows, motion vars) with per-value parity tests —
  the "web embedded CSS = mobile design system" rule is now enforced by CI. The shared Button/Card
  lower with exact spec metrics (the write-once proof test composes the native golden card tree as
  DOM). Core `HtmlStyle` gained white-space/text-overflow/box-sizing (additive DOM mirrors). New web
  suite: 26 tests. Remaining slice 2: TS-runtime lowering (hydration parity) + eqc transpilation of
  shared sources; then the Metal spike.

- **2026-07-04 — Web slice 2A landed: TS-runtime lowering (hydration parity).** The abstract nodes
  gained WIRE discriminators (`VisualNode.NodeKind` → `nodeKind` after transpilation) and the TS
  runtime gained `src/shared/nodes.ts` (interfaces for the transpiled shapes) + `src/shared/lowering.ts`
  — `lowerVisualNode()` mirroring `WebRealizer` RULE-FOR-RULE client-side, including the canonical CSS
  property order copied from `HtmlStyle.ToCssString`. Parity is a tested contract: a CROSS-PINNED
  byte-exact style-string literal is asserted verbatim by BOTH `WebRealizerTests` (C#) and
  `lowering.spec.ts` (vitest), so SSR output and client lowering can only drift by failing CI.
  13 new vitest cases (vitest 268 total); exported from the runtime index for the upcoming
  boot/component-render integration (slice 2B: eqc transpilation of the shared C# sources).

- **2026-07-04 — Metal spike landed: the first GPU frames, ±1 LSB from the Reference.** New
  `eQuantic.UI.Native.Engine.Metal`: an offscreen `IRenderBackend` driving Metal through typed
  `objc_msgSend` P/Invoke only (see open question 2 — answered for Metal), with ONE pipeline built at
  device init (premultiplied src-over into `RGBA8Unorm_sRGB`, shared storage), one fullscreen-triangle
  draw per command, and `MetalShaders.cs` — runtime-compiled MSL that TRANSLITERATES the normative
  math: `Sdf.RoundedRect/Stroke/Coverage`, `Paint.ColorAt` (sRGB-space gradient lerp), IEC sRGB→linear,
  and the SAME `AverageScale` AA width as the Reference (not `fwidth`), so the two rasterizers are
  directly comparable. Readback un-premultiplies through linear space (the Reference's exact output
  conversion). The 14 golden scenes were promoted to a shared `GoldenScenes` catalog consumed by both
  `GoldenSceneTests` (Reference vs repo goldens, normative) and the new skippable `MetalParityTests`
  (Metal vs Reference, fuzzy gate: max channel diff ≤ 4, ≤ 1% pixels beyond ±2). **Measured on Apple
  Silicon: max channel diff 1 across every scene (0 on four), zero pixels beyond ±2** — the GPU passes
  the golden harness's own ±2 tolerance, validating Sdf-as-spec (D2) and the color model (D-linear
  blending) end-to-end on hardware. Spike fences honored: runtime MSL (Slang toolchain replaces it,
  D3), leading-Clear-only, per-process ObjC leaks, shared-storage textures (Apple Silicon; discrete
  GPUs need Managed + sync blit). Native suite: 144 tests (129 + 15 Metal). Next: Slang toolchain
  spike (precompiled metallib), RHI extraction from the spike's shape, Vulkan.

- **2026-07-04 — Web slice 2B landed: the write-once loop CLOSED on web.** The REAL shared components
  (`eQuantic.UI.Components.Shared` Button/Card — the same classes the native goldens render) now
  transpile through eqc and EXECUTE in the browser runtime, rendering the same values the C#
  `WebRealizer` pins. Compiler: metadata value-type initializers emit as config objects (they were
  silently dropped — the whole `BoxStyle { … }` vanished), C# parameter defaults become JS defaults,
  Primitives types route to `@equantic/runtime` imports via semantic namespace discovery, and a
  day-one ordering bug that left the parser's semantic provider null is fixed. Runtime: vocabulary
  classes are the wire shapes AND self-lower via `render()` (abstract trees enter the existing web
  pipeline with zero reconciler changes; legacy components mix into abstract trees through the same
  seam), plus `design-system.generated.ts` — every token/theme/size-table value generated from the
  C# single source and byte-pinned in CI. See `docs/SHARED-COMPONENTS-PLAN.md` (migration step 3)
  for the full slice log. Suites: vitest 284, web 32, compiler 412, conformance 526 — all green.

- **2026-07-04 — Web slice 2C landed: shared STATEFUL components on web + SDK wiring.** The
  `SharedCounter` proof — the same fields+SetState+Build shape as the native CounterAppTests
  component — is real eqc output executing in vitest: mount → click → `setState` → rAF re-render,
  the web mirror of the native tap → SetState → rebuild golden. New `SharedStatefulComponent`
  runtime base (direct-SetState, no state-class split); the parser routes bases that semantically
  resolve to `eQuantic.UI.Primitives.StatefulComponent` there. Three silent-wrong-code compiler
  fixes en route: named-argument REORDERING to real parameter positions (defaults fill the gaps),
  implicit value-type field defaults (`int _count;` → `= 0`), and NULLABLE-enabled Roslyn
  compilations in eqc (disabled annotations made `Action?` parse as `Nullable<Action>` and silently
  broke overload binding). `Components.Shared` now ships `tools/source` and the SDK scans it behind
  the opt-in `EnableEQuanticSharedComponents` gate (name collisions with the standard web components
  until the unification). Suites: vitest 286, web 33, compiler 412, conformance 526, native 144,
  server 37. Full log in `docs/SHARED-COMPONENTS-PLAN.md`.

- **2026-07-04 — Unification slice 1 landed: the Core⇄Shared SSR bridge.** `VisualNodeComponent`
  (eQuantic.UI.Web) adapts any abstract subtree into the Core `IComponent` world — Core pages compose
  write-once components server-side through `WebRealizer`, and the transpiled call resolves to the
  runtime's mirror class client-side (ambient theme; hydration parity by construction). New
  `[RuntimeProvided]` attribute extends eqc's runtime-import routing beyond the Primitives namespace.
  Boot-time theme registration proven with `setPhotonTheme` (no new machinery). Suites: vitest 289,
  web 38, compiler 412, conformance 526, native 144, server 37. Next on this front: name-collision
  resolution (shared REPLACES standard) → SDK gate default-on → live sample page.

- **2026-07-05 — Unification slice 2 landed: the shared library is RUNTIME-PROVIDED and the first
  write-once page is LIVE.** The pinned transpiled Button/Card embed in runtime.js and export from
  `@equantic/runtime`; eqc routes the `eQuantic.UI.Components.Shared` namespace there, so the name
  reuse against the standard web components resolves semantically (usings decide — CI-tested). The
  SDK references the write-once stack by default (zero-config; the 2C scan gate is gone). The live
  proof: `DefaultUIDashboard` `/shared`, verified in a real browser — SSR → hydration → click →
  `Count: 3`, Photon tokens resolving in dark mode to the spec values (#5ca2e8 Primary, 40dp Medium).
  En-route compiler fixes (each silent-wrong-code): runtime routing extended to state classes and
  static helpers (`RuntimeProvidedTypeScanner`), expression-bodied Build in state classes (emitted a
  dead `new Container({})`), static classes with Build misdetected as components. Suites: vitest 289,
  web 39, compiler 412, conformance 526, native 144, server 37.

- **2026-07-05 — Unification slice 3 landed: WRITE-ONCE PAGES.** A Primitives `StatefulComponent`
  with `[Page]` is a full page — the server SSR scan accepts `UiComponent` types and bridges them
  through the web realizer; the client mounts the transpiled `SharedStatefulComponent` directly.
  Live: `/counter-shared` serves `data-ssr="true"` HTML with the initial state, hydrates, and
  re-renders on click — and the page class is verbatim PhotonHost-compatible (the same file could
  drive the native host today). Suites: vitest 289, web 39, compiler 412, conformance 526,
  native 144, server 39.

- **2026-07-05 — Engine clip primitive landed (rrect, Metal parity held).** `DrawCommand` gains a
  nullable DEVICE-space rrect clip BAKED by the builder (`PushClip`/`PopClip` stack, like the
  transform; nested clips intersect their AABBs and keep the innermost radii — the documented v1
  fence, exact for nested scroll viewports). Rasterizers MULTIPLY pixel coverage by the clip's own
  SDF coverage, so clip edges anti-alias exactly like shape edges — the same normative math on both
  sides. New shared golden scene `clip-rrect` (overflowing gradient/circle/rotated-rect confined to
  a rounded viewport, plus an outside-the-clip control dot); the Metal parity suite ran it
  automatically through the shared catalog and HELD the fuzzy gate (uniforms grew to ten float4s:
  clip rect + radii, flag in flags.z). This opens the ScrollView (A6) gate. Suites: native 152
  (26 goldens, 16 Metal parity), all others green.

- **2026-07-05 — Slang toolchain spike landed (D3 validated).** ONE normative shader source now
  exists: `eQuantic.UI.Native.Engine/Shaders/Sdf.slang` (the Sdf.cs/ColorAt/ColorSpace/clip
  transliteration, HLSL-flavored). `slangc` (v2026.12.2, official release) compiles it OFFLINE to
  the committed `Generated/Sdf.metal` — embedded in the Metal backend, which now loads the GENERATED
  source instead of hand-written MSL — and `Generated/Sdf.spv` (SPIR-V, ready for the Vulkan
  backend). Toolchain findings: entry-point names and the `[[buffer(0)]]` uniform binding are
  PRESERVED by slangc's Metal emission, so the backend needed zero interface changes; the
  **16-scene Metal parity suite HELD the fuzzy gate unchanged** with the generated shader. Regen via
  `scripts/generate-shaders.sh` (EQ_SLANGC); packaging: slangc ships EMBEDDED in a NuGet package
  like the Bun binaries (framework-dev tool — app developers never see it). Remaining D3 tail: the
  offline metallib step (xcrun metal) and pipeline caching land with the packaging milestone.

## Definition of done (v1 preview)

Photon v1 is "real" when: the golden suite (≥ 400 cases) is green on Metal + Vulkan + Reference across
the device matrix; the Dashboard demo runs at 120 Hz with zero steady-state allocations and all frame
budgets met; VoiceOver/TalkBack navigate it; shaders are 100% precompiled (runtime pipeline creation
asserts); an external dev ships a template app in minutes; and the supported v1 surface (primitives,
components, styles) is documented with the same honesty as `docs/DOTNET-COVERAGE-PROGRAM.md`.
