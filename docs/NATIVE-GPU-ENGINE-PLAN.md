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
- **D6b — SAMPLING is a first-class RHI capability (nearest AND linear), decided 2026-08-01.**
  The engine shipped its first year with no sampler at all — every texture read is a `Load`. That
  was never a principle; it was the state that glyph and icon rasters justified, because those are
  generated at DEVICE SCALE, so texels map 1:1 and nearest is *correct* rather than a compromise.
  Two consumers now need real filtering: scaled images (already a named fence in the W4 log) and
  the dual-Kawase blur, whose entire bandwidth argument rests on hardware bilinear averaging four
  texels per tap. Emulating those taps with `Load` would preserve bit-exact parity at roughly 4×
  the taps — a workaround that quietly makes the plan's own algorithm cost what it exists to avoid.
  So samplers land properly: the RHI exposes a per-binding filter, blur and scaled images take
  linear, glyphs and icons keep nearest.
  Parity consequence, stated rather than discovered later: bilinear is mathematically specified (a
  weighted average of four texels by fractional position), but hardware evaluates the weights in
  bounded fixed point, so a filtered read differs slightly between the CPU reference and a GPU.
  That difference is BOUNDED and non-structural — the same class the existing ±4 tolerance was
  built for (sRGB decode, fast-math `pow`) — and a transliteration bug still moves whole edge runs
  by dozens of values, so the gate keeps its discriminating power.

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
  `scripts/generate-shaders.sh` (self-resolving since W2 slice 1 — see the 2026-08-01 entry;
  `EQ_SLANGC` remains the override); packaging: slangc ships EMBEDDED in a NuGet package
  like the Bun binaries (framework-dev tool — app developers never see it). Remaining D3 tail: the
  offline metallib step (xcrun metal) and pipeline caching land with the packaging milestone.

- **2026-07-05 — Analytic shadow landed (§05), Metal parity held; component elevations wired.**
  `Sdf.ShadowCoverage` is the new normative falloff — `1 − smoothstep(−1.5σ, +1.5σ, d)`, σ = blur/2 —
  implemented identically by the Reference and the Slang shader (flags.x = 2; blur rides the
  strokeWidth uniform slot; MSL/SPIR-V regenerated). `DrawCommandKind.ShadowRRect` bakes
  offsetY/spread into the shape at record time. New shared golden `shadow-rrect`; the Metal parity
  suite (now 17 scenes) held the fuzzy gate. Component layer: `BoxStyle.Elevation` (0–5, theme-
  resolved) — native emits the shadow under the fill; web/TS lower to `box-shadow` from the SAME
  ShadowSpec (TokenCss format, cross-consistent). Card Elevated is REAL now (E1 + the §05 dark-only
  1dp border via a transparent-light token), Switch thumb E1, AppBar scrolled E2. BottomNavigation's
  top-oriented E2 stays fenced (shadow orientation joins the insets work).

- **2026-07-31 — Status-log sync: the 2026-07-30 wave (logged in detail in the SHARED/STYLE plans).**
  The engine/native layer grew far past the last entry while this log slept; the systems, briefly,
  with their homes: **group-opacity layers** — `DisplayList` gained `BeginLayer`/`EndLayer`
  (`PushLayer(alpha)`/`PopLayer`, balance-checked); the Reference backend composites a real offscreen
  layer, the Metal spike approximated per-command alpha (fence RETIRED 2026-08-01 by the offscreen pass — see that entry).
  **Static transforms** — center-anchored `Matrix2D` from `Transform2D` (translate→rotate→scale).
  **Scroll compositor v1** — `ScrollStore` (path-keyed offsets), `ScrollRegion` routing in
  `PhotonRealizer`, `PhotonHost.ScrollBy`, real `PinSticky` (vertical; end-of-container release
  fenced). **Value-transition animator (B14)** — `TransitionStore`, pure f(t) smoothstep, resolves
  flex weights during layout. **Pointer pipeline** — `HoverRegion`s + `PointerMove`/`PointerLeave`
  (real hover on Photon). **Enter/exit motion** — `PresenceStore` presence clocks per layout path +
  display-list command SNAPSHOTS replayed for exits (departed subtrees animate out as pixels only).
  **Drag-to-dismiss** — `DragStore` (input-driven follow, released glide as f(t)), `DragRegion`s,
  the press/move/up state machine with slop-cancel. **Navigation seam** — `LinkRegion`s +
  `PhotonHost.NavigationRequested` (the platform shell's future router hook). Suites at sync time:
  Native 266+, goldens 62 (incl. mid-drag sheet states). The long poles are UNCHANGED and are the
  point of the next phase: W4 text (HarfBuzz/FreeType/atlas), W5 platform shells (macOS first as
  dogfood), RHI extraction + Vulkan, toolchain packaging (metallib + pipeline caching), ObjC
  lifetime management. All M0–M5 exit boxes remain open.

- **2026-07-31 — W5 milestone 1: the FIRST REAL PHOTON WINDOW (macOS shell).**
  `eQuantic.UI.Native.Shell.MacOS` — NSWindow + CAMetalLayer hosting the PhotonHost, ZERO
  third-party packages (Edgar's directive): slim typed `objc_msgSend` AppKit bindings, the
  Metal-spike pattern, arm64-only for now. The backend grew per-format pipelines (offscreen stays
  RGBA8_sRGB; the window layer is BGRA8_sRGB — same shaders, hardware swizzle) and
  `RenderToDrawable` (encode straight into the drawable's texture, present on commit). The host
  gained `RenderScale`: layout/input/hit regions stay in dp, one root scale transform rasters at
  backingScaleFactor (retina). OS input (mouse down/up/move/drag, scroll) routes into the ordinary
  pointer pipeline — press visuals, hover diffs and the wave-3 anchored Select all live in the
  window. Cooperative loop: blocks on the next OS event while idle, free-runs on the frame clock
  while motion is active. `samples/PhotonDesktop` is the dogfood app (write-once Card/Buttons/
  ProgressBar/Select); `--self-test` presents 120 frames and exits 0 — proven on hardware
  (120/120, exit 0; Metal parity suite held at 275 native tests). Fences: fixed window size
  (host viewport work), keyboard/IME (W4/M4), per-process ObjC lifetimes, x64 msgSend variants,
  frame pacing via CVDisplayLink (today presents block on completion).

- **2026-07-31 — W4a: REAL TEXT — the engine draws glyphs (CoreText, zero third-party).**
  The engine gained its texture primitive: `DrawCommandKind.Texture` (A8 coverage × paint tint,
  NEAREST — rasters are device-scale, texels map 1:1), a per-frame texture table with identity
  dedupe, and the NORMATIVE Reference rasterizer. `ITextRasterizer` joins the Framework: the
  platform service that turns a measured block into an A8 raster — on macOS `CoreTextService`
  implements BOTH measurer and rasterizer (system frameworks only, Edgar's zero-third-party rule;
  HarfBuzz/FreeType are no longer the plan for platforms with a system text engine), so layout
  breaks and raster breaks agree by construction; lines sit on the STYLE's line-height grid, and
  the raster draws through a scaled CTM (wrap in dp, pixels at device scale). The realizer emits
  one Texture command per Text block (per-host cache keyed by everything but color — the tint
  carries it, light/dark share rasters); no service → the deterministic bars (tests unchanged,
  goldens intact). PROOF: PhotonDesktop `--render-png` renders the write-once demo through the
  Reference at 2× — San Francisco glyphs from OUR engine. Fences: **W4b — the Metal textured
  pipeline via the Slang round-trip** (the window skips Texture commands and shows bars over
  CORRECT CoreText layout until then), ellipsis glyph on truncation, icon/image rasters riding the
  same primitive, CTFont per-process cache.

- **2026-07-31 — W4b: REAL TEXT ON SCREEN — the Metal textured pipeline (Slang round-trip).**
  `textured_fragment` joins the ONE normative Slang source (regenerated with slangc 2026.14.1 —
  MSL + SPIR-V recommitted): A8 coverage × tint via texel `Load` (NEAREST by definition, no
  sampler bindings — exact Reference parity), clip multiplying like the SDF path, texture size
  riding the gradient uniform slot. MetalBackend: per-(format, textured) pipeline cache, R8Unorm
  uploads cached by raster IDENTITY (replaceRegion bindings), a 1×1 dummy keeps the shared texture
  binding valid for SDF passes, and RenderCore switches pipelines per command run. The
  `texture-coverage` scene joins the golden catalog — Reference golden + Metal parity in one
  entry; parity held at 17/17 scenes. The WINDOW now renders REAL San Francisco glyphs
  (PhotonWindow plugs CoreTextService as measurer AND rasterizer; self-test 120 frames, exit 0).
  Remaining W4 tail: icon glyph rasters riding the same primitive (the 30% disc placeholder),
  image decode/upload (A11/M4), ellipsis on truncation, texture eviction policy.

- **2026-07-31 — W4 icons: REAL GLYPHS — the 30% disc retires.**
  `SvgPath` joins the Framework: a pure-C# SVG path-data parser (full command set, quadratics
  elevate exactly, arcs convert via F.6.5 into ≤90° cubics; SVG lexing incl. compact arc flags;
  malformed data degrades, never throws) — the ONE normalizer every platform rasterizer consumes.
  `IIconRasterizer` is the platform seam; macOS implements it with CoreGraphics only (fill nonzero
  / stroke round-cap-round-join — the pack convention — through a viewBox→pixel CTM, stroke widths
  in glyph units). The realizer draws icons as tinted Texture commands (per-host cache keyed by
  glyph VALUE + size + scale; color rides the tint — modes share rasters); no service → the disc.
  The Select's chevron, the curated set and pack glyphs all render REAL in the window and the
  Reference alike (6 parser tests incl. every curated glyph; suites Native 285). Fences: fill-rule
  evenodd (IconGlyph carries none), atlas packing (one texture per glyph today), image decode
  (A11/M4 — the remaining Texture consumer).

- **2026-07-31 — W5: LIVE WINDOW RESIZE.** `PhotonHost.Resize(w,h)` adopts a new viewport WITHOUT
  recreating the host — component instances, transitions, scroll offsets and presence clocks all
  survive; the next frame lays out against the new size (S6 size-class changes resolve naturally).
  The shell polls the content bounds each cycle (no ObjC delegate class needed), resizes the
  drawable at backingScaleFactor and keeps input mapping against the CURRENT height. Window gains
  the Resizable style + a 320×240 content minimum. Proven: resize contract tests (viewport
  adoption, Fill re-layout, same-host dispatch after resize, same-size no-op) + window self-test.

- **2026-07-31 — W4 images: the Rgba8 texture path (the last Texture consumer).**
  `TextureData.Rgba` (straight sRGB, texel color wins over tint), the `textured_rgba_fragment` in
  the ONE Slang source (Load/nearest; premultiplies the linear sample), per-kind Metal pipelines +
  sRGB RGBA uploads, and the NORMATIVE Reference branch. `IImageLoader` is the platform seam
  (Image.Source → RgbaImage); the realizer owns the FIT math (Stretch/Contain centered/Cover
  clipped to the node rrect) with per-source caching; no loader → the placeholder box (tests
  unchanged). `texture-rgba` joins the golden catalog — parity held 18/18. Remaining: the macOS
  CoreGraphics/ImageIO loader wired into the shell (the seam + fake-loader tests landed first),
  bilinear for scaled images, URL/async sources.

- **2026-07-31 — W4 images COMPLETE on macOS: the ImageIO/CoreGraphics loader.**
  `CoreGraphicsImageLoader` (SYSTEM frameworks only): any OS-decodable format → straight sRGB RGBA
  (CG's premultiplied output un-premultiplied at the boundary), stateless — the host caches per
  source. Wired into the window AND the offscreen proof; the demo shows one of the engine's own
  goldens decoded and drawn through the Rgba8 texture path (image delivered). Known issue filed:
  a fixed-size Image STRETCHES under a Column's Cross=Stretch (the "stretch-fills-auto-only"
  layout contract says explicit sizes should hug — layout fix pending; Contain keeps the picture
  correct inside the stretched bounds meanwhile). Fences: bilinear for scaled images, URL/async
  sources with loading states.

- **2026-07-31 — D3 COMPLETE: the offline metallib.** `generate-shaders.sh` now also runs
  `xcrun metal`/`metallib` (Metal Toolchain, downloaded by Edgar) and commits `Sdf.metallib`;
  the backend loads the PRECOMPILED library first (dispatch_data + newLibraryWithData) with the
  MSL source path as the dev fallback — ZERO runtime shader compilation, the founding thesis
  fulfilled. Window presents are asynchronous (drawable-pool backpressure). Parity 18/18 through
  the binary path. Remaining D3 tail: pipeline caching across launches (binary archives).

- **2026-07-31 — W1 LANDS: the RHI extracted — and VULKAN RENDERS, ±1 LSB from the Reference.**
  The fine-grained RHI now exists in the engine (`Rhi.cs`), EXTRACTED from the Metal spike's
  proven shape per the 2026-06-10 deferral, not invented ahead of it: `IRhiDevice` /
  `IRhiRenderTarget` / `IRhiTexture` / `IRhiCommandList`, `RhiPipelineKind` (the D5 fixed
  registry as a CLOSED enum — the interface cannot express creating a pipeline at draw time),
  the normative 160-byte `DrawUniforms` block (moved up from the Metal backend, now shared, with
  `TryBuild` as the one encode math), and `RhiRenderer` — the display-list encode loop
  (leading-Clear→pass-clear, the layer-alpha spike fence, textured pipeline switching, the
  identity-keyed texture cache + 1×1 dummy binds) hoisted ONCE above the backends, so GPU
  targets can only differ in API calls, never in frame semantics. Metal reimplemented over the
  RHI (`MetalDevice`/`MetalCommandList`/`MetalTexture` + the `MetalBackend` adapter; public
  surface — `DeviceHandle`, `PixelFormatBgra8UnormSrgb`, `RenderToDrawable` — unchanged, the
  shell untouched); Metal parity held 18/18 and the window self-test presented 120/120 through
  the shared encoder. NEW `eQuantic.UI.Native.Engine.Vulkan`: OWNED slim bindings (~48 typed
  `LibraryImport` externs + exact-ABI structs — open question 2's answer extended to the C ABI,
  simpler than Obj-C as predicted), instance at Vulkan 1.2 (the committed `Sdf.spv` is SPIR-V
  1.5) with conditional portability enumeration/subset for dev ICDs, device requiring
  `shaderDrawParameters` (slangc lowers `SV_VertexID` via `gl_BaseVertex` → the DrawParameters
  capability), ONE shader module carrying all four entry points (D3's Vulkan half redeemed),
  per-draw uniforms through a persistently-mapped dynamic-offset UNIFORM RING — 160 B exceeds
  the 128 B push-constant floor the spec guarantees; stride 256 = the alignment ceiling, so no
  limits struct is ever queried — descriptor sets cached per (coverage, color) view pair,
  one-shot staging uploads, readback via `TRANSFER_SRC` finalLayout → buffer → the SHARED
  `RhiReadback` un-premultiply (Metal and Vulkan readbacks can only agree). MEASURED through
  MoltenVK as a DEV ICD (D1 untouched — the product ships native Vulkan on Android, native Metal
  on Apple, no translation layer ever; brew `vulkan-loader` + `molten-vk` are dev-machine
  tooling): all 18 golden scenes at **max channel diff 1, zero pixels beyond ±2, exact-0 on the
  same four scenes Metal zeroes** — the two GPU backends are pixel-twins through one encoder and
  one Slang source. `VulkanParityTests` mirrors the Metal suite case-for-case (skips without a
  loader). Suite: native 327, all three backends green on one host — the M0 pixel gate is
  satisfied everywhere but "on physical devices" (Android hardware = the W5 shell's milestone).
  Fences: offscreen-only (the swapchain arrives with the Android shell), submits always wait
  (real fences join the frame loop), 4096-draw uniform ring per pass, descriptor pool sized 1024
  pairs.

- **2026-08-01 — D3 CLOSED: pipeline caching across launches on BOTH GPU backends — and D5's
  assert is REAL.** The cross-launch cache the D3 entry left pending now exists through one
  shared seam (`Engine/PipelineCache.cs`: cache directory — `EQ_PHOTON_CACHE_DIR` override for
  tests/sandboxes/the future Android shell, else the platform user-cache location — file names
  FINGERPRINTED by SHA-256 of the shader bytes, so a framework upgrade targets a fresh file and
  stale siblings sweep; everything best-effort: an unwritable disk degrades to per-launch
  compilation, never to a failed device). Metal: `MTLBinaryArchive` loaded from disk when
  present (corrupt archives delete-and-rebuild), attached to every PSO descriptor via
  `setBinaryArchives:` — creation is an archive LOOKUP on every launch after the first —
  add+serialize on first boot; measured on this Mac: `Sdf-<hash>.metalarchive`, 92 KB, the whole
  registry. Vulkan: `VkPipelineCache` seeded from the on-disk blob (the driver validates
  vendor/device/UUID itself; a rejected blob retries empty), passed to every
  `vkCreateGraphicsPipelines`, persisted on first boot. BOTH devices now build the ENTIRE fixed
  registry AT INIT (Metal: RGBA8 + BGRA8 × 3 kinds; Vulkan: RGBA8 × 3 until the Android
  swapchain adds its format) and `PipelineState`/`Pipeline` became pure lookups that THROW on a
  miss — D5's "creating a pipeline at draw time is a bug by definition (asserted)" is now
  literally asserted. `PipelineCacheTests` (per backend, skippable): the first device in a fresh
  cache dir MUST persist the artifact; a second device consumes it and is judged against the
  Reference on the busiest scene. Suites: native 329; window self-test 120/120 (the BGRA8
  registry + archive live in the real window). NOTED/BLOCKED: the offscreen group-opacity
  composite — the last rendering-correctness fence — needs a NEW Slang entry point
  (`layer_composite`: premultiplied sample × layer alpha; the existing textured pipelines
  premultiply straight-alpha texels and cannot express it), and slangc is NOT locatable on this
  machine (Spotlight + deep search came up empty; the D3/W4b regens ran from a since-cleaned
  location). Blocked until `EQ_SLANGC` points at a binary again — the W2 toolchain packaging
  (slangc embedded per-platform like Bun) is what makes this structural instead of environmental.

- **2026-08-01 — W2 slice 1: the toolchain RESOLVES ITSELF; the shader blocker is retired.**
  `scripts/slang-toolchain.sh` acquires the PINNED slangc (2026.14.1) on first use and verifies its
  SHA-256 before extracting; `generate-shaders.sh` sources it and no longer demands `EQ_SLANGC`
  (which still wins as an explicit override). Resolution order: override → a toolchain package
  extracted in-repo → the local cache → pinned download. PROOF the provisioning is correct, not
  merely present: regenerating from `Sdf.slang` reproduces `Sdf.metal`/`Sdf.spv`/`Sdf.metallib`
  **byte-identically** to the committed artifacts (clean `git status`), from a cold cache and from
  the persistent install alike — so the version, the toolchain wiring (including `xcrun metal`) and
  the committed outputs are all confirmed at once.
  DELIBERATE DEVIATION from this plan's letter, stated so it is a decision and not an oversight:
  the per-platform NuGet packages are NOT created yet. Bun ships inside packages because EVERY app
  build needs it; slangc is framework-dev-only — app developers consume the committed
  `.metallib`/`.spv` and never compile a shader — so committing ~57 MB per platform (≈230 MB across
  four) would weigh the repo down for a binary almost nobody fetches. The pin + digest give the
  same reproducibility at zero repo cost, and the resolver already PREFERS a packaged toolchain the
  moment one exists, so promoting it later is additive. Package the binaries when app-level shader
  compilation becomes real — that is the milestone that actually needs them.

- **2026-08-01 — The OFFSCREEN PASS lands: group-opacity layers are real on both GPUs.** The
  approximation the Metal spike shipped with — per-command alpha, which double-blends every
  overlap inside a layer — is RETIRED; `RhiRenderer` now renders each layer scope into its own
  target and composites it once. The design is TWO-PHASE rather than nested passes: every scope
  (innermost first) renders before the parent pass opens, because Metal and Vulkan each allow one
  open pass per encoder and a nested design would have needed different scaffolding on each.
  `layer_composite` joins the ONE Slang source — its own entry point rather than a reuse of the
  image path, because a render target's texels are already PREMULTIPLIED, so compositing is a
  scale where the image path premultiplies (sharing it would apply alpha twice). Both backends'
  render targets became SAMPLEABLE, and `IRhiRenderTarget IS an IRhiTexture` stopped being
  aspirational: the bind sites cast to a concrete texture type and would have thrown on a layer
  target, so each backend gained an internal bindable accessor both kinds satisfy. New golden
  `layer-group-opacity` (overlapping circles in one layer, a nested layer, a clipped layer) —
  parity green on Metal AND Vulkan, 21 scenes × 2 backends.
  En route, the parity gate caught an authoring bug in the new scene itself: a redundant `Clear`
  on top of the catalog's own diverged by design, since the Reference applies mid-stream Clears
  and the GPU backends honor only the leading one. That fence is real and now has a test that
  would notice if anyone leaned on it.
  This is the machinery `backdrop-blur` needs — the blur chain renders into these same offscreen
  targets — so the next slice is the dual-Kawase (plan W3), not new plumbing.

- **2026-08-01 — W3 lands end-to-end: dual-Kawase blur + BackdropBlur, CPU-normative on three
  backends, and the Hero pill is real frosted glass.** The pyramid's normative math lives in
  `Blur.cs` — 5-tap /8 downsample, 8-tap /12 upsample, half-DESTINATION-texel offsets,
  `Levels(radius)` clamped at 4 — operating on premultiplied sRGB8 (`BlurImage`) and re-encoding
  per level, because that is precisely where the GPU quantizes (its levels are stored render
  targets); quantize anywhere else and the parity gate notices. The shader twins transliterate the
  taps; `RhiRenderer.Blur` walks the same pyramid through the D6b LINEAR sampler. On top of it,
  `DrawCommandKind.BackdropBlur` (radius in the StrokeWidth slot, region rrect baked device-space
  into the Clip slot by the builder — the PushClip math) splits the frame: content-so-far renders
  offscreen, blurs, and composites back through `layer_composite`'s clipped path, before drawing
  continues. The Reference twin snapshots via `ReadPixelsPremultipliedSrgb`, runs `Blur.Apply`,
  and source-overs under the rrect's AA coverage — same math, same order. Fence (both targets,
  by decision not accident): a backdrop inside a group-opacity layer is skipped — CSS opacity
  isolates the backdrop root the same way. Vocabulary: `BoxStyle.BackdropBlur` → CSS
  `backdrop-filter` + `-webkit-` twin on web (pinned C#↔TS), one engine command on Photon
  (pinned in `S3BackdropBlurNativeTests`). Golden `backdrop-blur` (two stacked glass splits) is
  green on Reference + Metal + Vulkan.
  TWO real bugs died en route, both mine, neither MoltenVK's:
  (1) `CreatePipeline`'s fragment-entry `switch` never learned the blur kinds — they fell into
  `_ => "sdf_fragment"` and the GPU faithfully ran the wrong shader (transparent with ColorA=0,
  white with ColorA=1 — the "signature" that briefly looked like a driver cache collision). The
  name switch is GONE: the build now emits ONE single-entry SPIR-V per stage (explicit
  `[[vk::binding]]` keeps numbers stable across the split files), every module exposes `main`,
  and WHICH code runs is chosen by the module — the shape everyone ships, immune to the whole
  bug class.
  (2) The Vulkan descriptor-set cache was keyed by VIEW HANDLE and never invalidated: destroy a
  target, let the driver reuse the handle, and a later lookup returns a set pointing at freed
  memory — intermittent DEVICE_LOST that the validation layer HIDES (wrapped handles never
  collide). Disposal now funnels through `OnViewDestroyed`, which drops the entries and recycles
  their sets. The backdrop splits churn targets hard enough that a 20-iteration soak now covers
  what the old tests never exercised.

- **2026-08-07 — M4 accessibility bridge v1: the semantics tree, and VoiceOver's first answer.**
  Photon draws its own pixels, so the OS saw one opaque view. `SemanticsTree.Collect` (shared,
  target-neutral) now derives reading-order semantics from the realized frame — page AND overlay
  layouts (`RealizeResult.OverlayRoots`, retained so dialogs are seen) — with the path as identity,
  a control's inner text as its name, and scrolled-out content included (the FocusStop rule, not
  the pointer's). `PhotonHost.Semantics()` + `ActivatePath(path)` are the bridge surface; the
  macOS bridge (`PhotonAccessibility`) answers the content view's `accessibilityChildren` with
  pressable `EQAXElement`s (role/label/value/enabled/frame y-flipped/identifier=path,
  `accessibilityPerformPress` → the same handler a tap runs). Proof: 8 `SemanticsTests` including
  the `SemanticsMatchFocusStops` PARITY GATE (the semantics walk and the input walk can never
  drift apart silently), and the window self-test now prints the live AX answer — the Studio
  gallery reports `accessibility elements: 100 — first: AXButton "Back"` through the real ObjC
  dispatch. iOS/Android bridges consume this same tree when their shells gain them.

- **2026-08-07 — M4 IME: composition through NSTextInputClient.** The macOS view CONFORMS (runtime
  `class_addProtocol` + ten methods with hand-spelled struct encodings — a wrong encoding is a
  silent no-op). While a field holds the caret, keys go to the platform's input context: dead keys
  and CJK methods compose as MARKED TEXT (host state, rendered inline underlined at the caret,
  value untouched until commit; cancel restores exactly; compose-over-selection replaces like
  typing; obscured fields compose blind), editing keys return as selectors re-spelled to the DOM
  names the host already speaks, ⌘-chords never enter the context, and
  `firstRectForCharacterRange` anchors the candidate window at the real caret. Proof: 8
  `ImeCompositionTests` + the self-test driving the registered selectors against the ⌘K field
  (`ime probe: marked '´' → committed`). Fence: code surfaces compose blind (no marked-run visual
  in the editor's face yet); human fingers still owe the "system invokes us" half.

- **2026-08-07 — W7 perf harness: the promises get numbers.** `PerfHarnessTests` measures the C#
  half of a frame (realize: layout + token resolve + emit) on a dashboard-shaped scene (24 cards +
  a loop-motion strip, 1280×900): steady-state managed allocation per frame, display-list command
  count, and realize time. Baselines on this M-series, 2026-08-07: **183 KB/frame** allocated in
  steady state (~22 MB/s of gen0 pressure at 120 Hz — the arena/pooling target, and now it has a
  number: LayoutNode tree + paths + sink lists are rebuilt every frame), **146 commands**,
  **realize p50 0.23 ms / p95 0.32 ms** against the 8.33 ms budget. Ceilings are pinned as
  REGRESSION rulers (256 KB, 200 commands, 33 ms alarm — deliberately loose on time so shared CI
  never flakes); the ratchet tightens as pooling lands, never loosens casually. The GPU half stays
  covered by parity goldens, not CI timing; device-class numbers wait on device runs.

- **2026-08-07 — M3 recycling list: ten thousand rows cost a screenful, on BOTH targets.** The
  write-once `ListView` (count + fixed extent + builder; overscan margin; the rest of the list is
  two spacers, so layout and the scrollbar see the true content height) rides the ScrollView
  out-channels the native realizer always had — and the WEB half of those channels now exists:
  the scroll event feeds `onScrolled`, an after-pass sweep (the shortcut/camera pattern) measures
  the viewport and adopts the initial offset once. Landing it surfaced and fixed THREE SDK gaps:
  the `#app` frame is now EXACT viewport height (app pages scroll internally, document pages
  overflow and body-scroll as before — one CSS rule, both worlds); a web `ScrollView` without
  explicit Height defaults to 100% (a scroll view IS the window its parent gives it — native
  parity; the console shell's fixed-toolbar design started working on web the moment this landed);
  hydration adopts `data-eq-*` framework markers (SSR cannot know client identities). Proof: 6
  `ListViewTests` (window materializes, commands independent of Count — 100 vs 10 000 emit
  IDENTICAL command counts —, MaxOffset sees the whole list, scroll moves the window, sub-row
  scrolls repaint without invalidating), 4 web specs, and the live browser: /rows at scrollTop
  220 000 holds 25 children (#04997–#05019). v1 fence: vertical, fixed extent.

- **2026-08-07 — Frame allocation: 183 → 106 KB (−42%), and an honest map of the rest.** Two safe
  wins landed: the PATH-STRING CACHE (a path is identity — "0/1/2" this frame must be the same
  string next frame; the host lends a dictionary that survives frames, `LayoutContext.ChildPath`
  is the single concatenation point) and the REUSED DisplayListBuilder (`Reset()` keeps every
  buffer's capacity; the macOS shell and the harness now run one builder per loop — worth 65 KB
  alone). Two bigger wins were built, MEASURED (down to 75 KB combined), and REVERTED with their
  lessons recorded: a double-buffered LayoutNodePool broke 155 tests that legitimately retain
  RealizeResults across frames — recycling the tree needs an explicit result LIFETIME first (the
  `LayoutContext.Node` factory every node now passes through is the hook waiting for it); and
  ArrayPool-ing the flex scratch arrays corrupted goldens in ways the prefix-clearing did not fix
  — parked rather than shipped on hope. The ratchet is pinned at 152 KB; goldens are the aliasing
  guard that caught both reverts.

- **2026-08-07 — M5 first slice: `dotnet new equantic-app`, and the external-developer path RUNS.**
  New `eQuantic.UI.Templates` package (PackageType=Template): scaffolds a csproj on
  `Sdk="eQuantic.UI.Sdk"` with the version STAMPED into global.json at pack time from $(Version)
  (no per-release hand edit), a Program.cs (AddUI/UseTheme/MapUI) and a write-once counter page.
  Proven END-TO-END as an external developer would live it: `dotnet pack` the framework →
  `dotnet new install` → `dotnet new equantic-app -n HelloQuantic` → `dotnet build` (SDK resolved
  from the NuGet feed, first try) → `dotnet run` → the counter counts in the browser, themed and
  viewport-centered. Three commands, no Node, no JS. This also re-ran the NuGet consumption path
  nothing had exercised since 0.1.2 (samples moved to project references). Remaining M5: native
  template (`equantic-native`), docs polish, preview NuGet publishing.

## Definition of done (v1 preview)

Photon v1 is "real" when: the golden suite (≥ 400 cases) is green on Metal + Vulkan + Reference across
the device matrix; the Dashboard demo runs at 120 Hz with zero steady-state allocations and all frame
budgets met; VoiceOver/TalkBack navigate it; shaders are 100% precompiled (runtime pipeline creation
asserts); an external dev ships a template app in minutes; and the supported v1 surface (primitives,
components, styles) is documented with the same honesty as `docs/DOTNET-COVERAGE-PROGRAM.md`.

### Track W — the iOS shell (2026-08-02)

`eQuantic.UI.Native.Shell.iOS`: a `UIViewController` whose view IS the `CAMetalLayer`, driven by a
`CADisplayLink` that presents only when the host says something changed. UIKit owns three things —
the view, the touches, the clock — and the drawable is driven by the same ObjC messages the macOS
window sends, so both platforms come off one engine rather than two that agree by inspection.
`PhotonApp.Run(args, () => new App(), theme)` is the whole entry point: no storyboard, no
AppDelegate, no Info.plist keys beyond the launch screen.

What the platform taught us, each fixed in the ENGINE rather than the shell:

- **Metal is Apple's, not the Mac's.** `MetalDevice` refused to start anywhere but macOS. The same
  device, queue and pipelines back a phone; the guard now names Apple hardware.
- **A metallib is built for ONE target.** The committed artifact is macOS's, and on the simulator it
  loads happily and then fails at the first pipeline ("library was not compiled for the simulator").
  Off macOS the MSL path takes over — the same source the metallib was compiled from — until
  `generate-shaders.sh` emits an artifact per platform.
- **The binary archive cannot be serialized on a simulator.** Not an error it reports: an assertion
  that takes the process down. There is nothing to save there either, so the cross-launch archive
  stays switched on where a real driver's compilation is what it skips.
- **`UILaunchScreen` is not cosmetic.** Without it iOS runs the app in a compatibility window, and
  every safe-area inset it reports is that fiction's rather than the phone's.

`eQuantic.UI.Native.Shell.Apple` now holds what both shells share — the ObjC runtime bindings and the
CoreText / CoreGraphics / ImageIO services. Text, icons and images are decoded by the same system
frameworks on both platforms; only the window and the event loop ever differed.

Verified on an iPhone 17 simulator: the Wallet launches full screen with the notch and home
indicator respected, the tab bar navigates, the first transaction row swipes to reveal its action,
and the list pulls to refresh. The desktop head builds and self-tests from the SAME two app files.

Open: a per-platform metallib (a shader compile at first launch until then), a real device run
(signing), and an SDK-generated Info.plist so an app author never has to know about `UILaunchScreen`.

### Track W2 — the app writes C# and nothing else (2026-08-02)

`eQuantic.UI.Sdk.Native`, the device sibling of the web SDK. An app names the platforms it runs on
and writes C#; the SDK supplies the vocabulary, the component library, the shell for each target,
the platform minimum, and the app manifest. The Wallet is now ONE project with two target
frameworks and a `#if IOS` at the entry point — its only non-`.cs` file is a fifteen-line csproj
that names two TFMs and a bundle id.

What that removed, each of which was a thing an app author had to KNOW:

- **The Info.plist.** `UILaunchScreen` is not cosmetic — without it iOS runs the app in a
  compatibility window and every safe-area inset belongs to that fiction. The SDK merges it through
  `PartialAppManifest`, and an app that wants different orientations still states its own.
- **Which shell to reference.** It follows from the target framework, so it is not a choice.
- **`SupportedOSPlatformVersion`.** 15.0 is where CAMetalLayer, safe-area insets and
  MTLBinaryArchive are all present; picking it is the framework's job, not the app's.
- **The second project.** A phone head and a desktop head were two csprojs and a set of linked
  files; they are one project and one `#if`.

Verified end to end: the app installs and runs full screen on an iPhone 17 simulator with the tab
bar navigating, and the same source self-tests at 120 frames through the desktop head.

### Track W3 — the app icon, and the writers behind it (2026-08-02)

An app's icon is now a build input the framework owns. `Assets/AppIcon.png` (artwork a designer
made) or `Assets/AppIcon.cs` (an `IAppIcon` the engine draws) — CONVENTION, the way .NET does it,
with `EQuanticAppIconSource` in the project file as the override. Both end in the same generated
asset catalog, so nobody hand-writes a `Contents.json`, an `.xcassets` or a manifest key.

`eqicon` is the tool. The C# path compiles the icon file ON ITS OWN against the vocabulary — the
app's assembly may target a device this machine cannot load, and an icon has no business reaching
into the app. The tree is designed on a 64dp canvas and rasterized at whatever density the platform
installs from, which is the same relationship every other tree has with a screen's scale factor.

Two platform traps absorbed rather than passed on: an iOS icon must be OPAQUE (one with an alpha
channel installs as a blank tile, silently), and Apple reads the icon's location from the app
MANIFEST rather than from a build property. eqicon flattens the first and writes the second.

`eQuantic.UI.Codegen` is new, and answers a standing complaint: generated files were string blobs
scattered through the code. One `CodeWriter` (append, indent, disposable scopes — the closer travels
with the scope, so an opener without its closer is unrepresentable) and a writer per FILE TYPE on
top: `PropertyListWriter` (tab-indented, like every plist on a Mac) and `JsonWriter` (whose one job
is that the last member never gets a comma and every other one does). The compiler's
`TypeScriptCodeBuilder` predates this and should be rebased on the same `CodeWriter` — it carries
source-map state, so that is its own change.

### Track W4 — the same icon, in a browser (2026-08-02)

The icon an app states once now appears wherever it belongs. `eqicon --web` writes what a browser
asks for — 512 and 192 for the install manifest, 180 for what iOS Safari pins to a home screen, 32
for the tab — from the very same `Assets/AppIcon.png` or `Assets/AppIcon.cs` the device SDK reads.
The web SDK runs it; the shell links the results; the app writes nothing. Asking an author to state
the icon once and then also write four `<link>` tags would be handing back the work we just took.

The downscale is a BOX FILTER: every destination pixel is the average of the source pixels it
covers. Point sampling is one line shorter and shreds an icon's edges at 32px, which is the size a
user sees most. No `.ico`: every browser that matters has taken a PNG favicon for a decade.

`ApplicationTitle` is the name a browser installs under — the SAME property the device head uses, so
an app that cares states it once, and absent that the assembly's name is what .NET would have called
it anyway. And the up-to-date check watches EVERY output rather than one: a check that watches a
single file calls a half-written set finished, which is how a deleted manifest stayed deleted.

### Track W5 — the host: CreateBuilder, Services, Configuration, Run (2026-08-02)

The sample's entry point was a `#if IOS` with two hand-rolled heads, which is the opposite of an
SDK. It is now the shape every modern .NET program has:

```csharp
var builder = PhotonApplication.CreateBuilder(args);
builder.Services.AddSingleton<IWalletLedger, WalletLedger>();
builder.Configure(photon => photon.Title = "eQuantic Wallet");
var app = builder.Build();
app.Run<WalletApp>();
```

`eQuantic.UI.Native.Hosting` wraps `HostApplicationBuilder`, so `Services`, `Configuration`,
`Environment` and `Logging` are the real ones — appsettings.json, appsettings.{Environment}.json,
user secrets, environment variables and the command line, already merged, in that order. A .NET
developer learning a second shape for the same idea is a cost the framework should not be charging.

The component is resolved from the CONTAINER, which is the point: `WalletApp(IWalletLedger ledger,
IConfiguration configuration)` takes its dependencies through its constructor like any other class,
and `--screen cards` reaches it without a line of parsing. `PhotonOptions` binds from the `Photon`
section, so `--Photon:MaxFrames 120` is the self-test and needs no bespoke flag; a value written in
the program still wins, because a value in a file is a default and a value in code is a decision.

WHICH device runs it is not a question the program answers. Each shell declares itself with
`[assembly: PhotonRunner(...)]` and the host finds the one that shipped — by looking in the folder
beside the program rather than at the entry assembly's reference list, because an app never names
its shell in code and the compiler drops references nothing mentions.

### Track W6 — the Android shell (2026-08-02)

`eQuantic.UI.Native.Shell.Android`: an Activity whose `SurfaceView` the engine presents into, the
`Choreographer` as the clock (the display's own vsync rather than a timer guessing at it), touches
routed into the same `PhotonHost` pipeline, and the system's insets fed to `SafeAreaInsets` so a
`SafeArea` node lays out against what the DEVICE reserves. The Wallet's trees needed no change to
sit correctly on it.

`AndroidTextService` is the platform's own shaper — `StaticLayout` decides where lines BREAK, and
where they SIT is the design system's decision, so each is drawn on the style's line-height grid.
The same split CoreText is held to. `AndroidIconRasterizer` lowers the SHARED `SvgPath` parse onto
`android.graphics.Path`: same glyphs, same units, same A8 coverage, only the fill differs.

Presentation goes through the NORMATIVE Reference backend for now — pixel-correct by definition and
slower than the GPU. The Vulkan swapchain (`VK_KHR_android_surface`, acquire/present, real fences)
replaces that one method and nothing above it. That is the next piece, and the backend still says
so in its own doc comments.

What the platform taught us:

- **An Android app has no `Main`.** The SDK rewrites `Exe` to `Library` because the system launches
  an Activity. So a Photon program is a METHOD — `public static PhotonApplication CreateApp(string[])`,
  the shape MAUI's `MauiProgram.CreateMauiApp` has — and the SDK generates the `Main` for the heads
  that need one. `Assembly.GetEntryAssembly()` is null there too, so the host finds the program
  among the loaded assemblies.
- **Android's implicit usings collide with the vocabulary** — Button, Dialog, ProgressBar,
  ScrollView, Space, Switch. A Photon app draws with the vocabulary, so the SDK removes
  `Android.App/Widget/Views/Content` from the implicit set; an app that wants a platform view still
  writes one `using`.
- **A raster measured at 1x and multiplied comes out short.** Glyph advance is not exactly linear in
  size, and the last character of every line was being sliced off. The raster is measured with the
  SCALED paint.

Verified on an emulator (API 36): the Wallet full-screen with insets honoured, real glyphs and real
icons, and the tab bar navigating under a tap. Open: the launcher icon (mipmaps from the same
`Assets/AppIcon.png`), and the Vulkan swapchain.

### Track W7 — a generator writes the other half of Program (2026-08-02)

`Program` is partial by nature, so the ceremony half is a SOURCE GENERATOR rather than a build step
writing text into obj. A generator sees the compilation, which is the whole difference: it finds the
app's `CreateApp` by symbol, emits a DIRECT call to it, and says at compile time when there is no
program (EQ3001) or more than one (EQ3002). Nothing is looked up at run time on any platform — the
reflection the host used to do is gone.

Three files come out, each only where it belongs:

- `PhotonProgram.g.cs` — a `[ModuleInitializer]` registering the factory. That is what a shell calls
  on Android, where the system launches an Activity and no `Main` ever runs.
- `PhotonEntryPoint.g.cs` — the `Main`, as the other half of `public partial class Program` when the
  app named its program that (the convention), and on its own when it did not. Executables only.
- `PhotonMainActivity.g.cs` — the launcher Activity, in the APP's assembly. It has to be there: an
  Activity in the shell leaves the app assembly unreferenced and unloaded, so its module initializer
  never runs and the program is never registered. That was a real crash, not a hypothetical.

### Track W8 — the Vulkan swapchain: Android draws on the GPU (2026-08-02)

The Android shell presents through VULKAN, straight into the window's own surface. Same engine, same
`RhiRenderer` encode loop the Metal backend runs, so the two GPU backends can only differ in API
calls — which was the point of extracting the RHI in the first place.

What it took, and what it did NOT:

- **The render pass gained a final layout.** A layer target ends shader-readable because it is
  sampled next; a swapchain image ends presentable because it is shown next. Everything else about
  the two passes is identical, which is why they are one method and not two.
- **A render target can now ADOPT an image it does not own.** A swapchain's images belong to the
  swapchain; destroying them here would take memory the presentation engine still holds.
- **Submit learned semaphores.** A presented frame waits for the image the display engine handed
  over and signals when it is done drawing. A queue wait cannot express that: the two sides run on
  different clocks and neither can be told to stand still. (v1 still waits after submitting — real
  frames-in-flight needs a fence per image, and that lands with the pacing work.)
- **Nothing above the swapchain changed.** The display list, the layout and the trees never learn
  which backend drew them, which is exactly why the Reference path could stand in until today.

`ANativeWindow_fromSurface` is the whole bridge: libandroid is the platform's own, like Metal is on
Apple, so no third party crosses this line either. Where Vulkan is unavailable the shell still falls
back to the NORMATIVE Reference backend and says which one it used, in the log, rather than leaving
it to be guessed from a screenshot.

Verified on an emulator (API 36, host GPU): `Photon: presenting through Vulkan`, the Wallet drawn
edge to edge with insets honoured, and the tab bar navigating under a tap.

### Assets — where an app's platform inputs live (2026-08-02)

```
Assets/
  AppIcon.png            the mark, stated ONCE. Or AppIcon.cs, an IAppIcon the engine draws.
  .generated/            every platform's icon set, derived from it. Ignored, never committed.
```

Nothing is committed and no build is ever missing an icon. Two facts about MSBuild made that
harder than it sounds, and both are now answered rather than worked around:

- **Android validates that every declared resource EXISTS before any ordinary target runs** —
  earlier than `BeforeBuild`, earlier than anything `BeforeTargets` can reach. The answer is
  `InitialTargets`, which an imported project contributes to the one importing it and which runs
  before the build's first target. Android only: running the shared icon target that early is
  exactly what stops Apple's packaging from ever receiving its items.
- **A wildcard is empty on a clean build.** Declaring the mipmaps by LITERAL path fixes that on its
  own: an item with a literal path exists whether or not the file does, and the file only has to be
  there when aapt reads it. The set is fixed — five density buckets, one name — so naming them costs
  nothing. `Link` carries the bucket, which an item transform cannot.

iOS's catalog stays in `obj/` and is handed to Apple's packaging by an ordinary target, which is
what that packaging takes. The web set lands in `wwwroot/`, because a server serves it.

Two designs were tried and rejected, both of which "worked" while hiding something: committing the
generated mipmaps (a clean clone builds, but a deleted folder silently produces one bad build), and
generating during RESTORE (Android is fixed, and the iOS catalog silently ships empty).
