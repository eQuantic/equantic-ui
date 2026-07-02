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
│   StatefulComponent/StatelessComponent · Build(context) · SetState │
│   IAppTheme theming · DI/services                                   │
├─────────────────────────────────────────────────────────────────────┤
│ Widget layer (native) — eQuantic.UI.Native.Widgets                  │
│   Box/Text/Image/Button/ScrollView/… → typed styles (no CSS)        │
│   Layout: flex/stack/absolute (own C# implementation)               │
├─────────────────────────────────────────────────────────────────────┤
│ Render tree + Reconciler — eQuantic.UI.Native.Framework             │
│   Primitive tree (RRect/TextRun/Image/Shadow/Clip/Layer/Transform)  │
│   Keyed-LIS reconciler (C# port of the web algorithm)               │
│   Compositor: layers, damage/dirty-region tracking                  │
├─────────────────────────────────────────────────────────────────────┤
│ ENGINE CORE ("Photon") — eQuantic.UI.Native.Engine        ★ the IP  │
│   Display list → render passes → draw batches                      │
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
- `eQuantic.UI.Native.Widgets` — the component library for native.
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
  native. Native widgets take typed style props (`Style { Padding, Background, CornerRadius, … }`);
  `IAppTheme`/variants/sizes carry over **conceptually** (theme objects returning typed styles instead
  of class strings). `StyleBuilder`(string CVA) stays web-only. This is the largest authoring-surface
  divergence and is stated here so nobody "discovers" it in month 9.
- **D12 — The widget trees are siblings, not the same tree.** `HtmlElement` deliberately mirrors the
  DOM 1:1 (existing design rule) — it must not be bent into a native abstraction. Native gets its own
  primitive/widget tree under the **same component authoring model** (`Build`, `SetState`, props,
  children). A shared cross-target widget façade ("write once, render DOM or Photon") is a *later
  convergence project* (Track N2), intentionally out of v1.

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
- **W6 — Framework + widgets.** C# port of the keyed-LIS reconciler over the primitive tree; flex/
  stack/absolute layout engine (own C#, Yoga-binding as fallback plan only); gesture system (tap,
  drag, fling with platform scroll physics curves); core widgets: Box, Text, Image, Button,
  ScrollView, TextInput(M4), List with recycling(M3); typed styles + `IAppTheme` mapping.
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
layout/widgets against the Reference backend). 12–18 months to M5 preview is realistic **only** with
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
2. Vulkan binding strategy — Silk.NET vs owned slim bindings (M0 spike decides; owning is preferred).
3. Android AOT flavor — NativeAOT vs Mono AOT (M0 gate, criteria above).
4. Bidi implementation — minimal UBA subset in C# vs small C lib (M1).
5. MSDF glyphs — v2 candidate, only if zoom/scale-independent text proves needed.
6. Desktop targets — macOS is nearly free (dogfood); Windows/D3D12-or-Vulkan decision post-M5.
7. **WebGPU as a 4th backend** (far future): the same HAL could target WebGPU — which would let the
   *native* engine render on the web without the DOM, converging the two stacks. Deliberately parked;
   noted so the HAL design doesn't preclude it.
8. Track N2 — cross-target widget façade (one component set rendering to DOM *and* Photon) — scoped
   and scheduled only after M5.

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

## Definition of done (v1 preview)

Photon v1 is "real" when: the golden suite (≥ 400 cases) is green on Metal + Vulkan + Reference across
the device matrix; the Dashboard demo runs at 120 Hz with zero steady-state allocations and all frame
budgets met; VoiceOver/TalkBack navigate it; shaders are 100% precompiled (runtime pipeline creation
asserts); an external dev ships a template app in minutes; and the supported v1 surface (primitives,
widgets, styles) is documented with the same honesty as `docs/DOTNET-COVERAGE-PROGRAM.md`.
