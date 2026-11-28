## Context

Slice 1 (#1937, archived as `2026-11-28-texture-tiles-behind-a-surface`) gave `BoxStyle.Patterns`, a list from a
closed set dispatched by `IPatternVisitor`, and Photon's `TiledTexture` draw. What #1938 still asks:

1. **The fade.** Nothing takes alpha away from a subtree along a gradient. What exists:
   - `LinearGradient` (Nodes/LinearGradient.cs:11-23): two `ColorToken` stops and a `Via`, along one of four
     `GradientDirection` keywords. `RadialGradient` (Nodes/RadialGradient.cs:18-24): two `ColorToken` stops, a centre in
     fractions and radii in **dp**. Neither has stop positions, a radius in fractions of the box, or `closest-side`,
     and both are colours, painted by the box, not opacities applied to a subtree. #1041 will fold both into the
     `Pattern` set, so this change does not reshape them.
   - `BoxStyle.Opacity` (BoxStyle.cs:132) is one alpha for a group: the web writes `opacity`, Photon `PushLayer`
     (EmitVisitor.cs:100).
   - Photon's engine has the group layer already: `BeginLayer`/`EndLayer` (DisplayList.cs:19-25), rendered offscreen
     by `RhiRenderer.Render`'s phase one (Rhi.cs:440-483) and composited by `layer_composite` (Sdf.slang:280-305),
     with the Reference backend's surface stack as the normative twin (ReferenceBackend.cs:48-80).
2. **Past the edges.** `Positioned` already takes negative offsets and fractions (Nodes/Positioned.cs:3-14). The web's
   `Stack` is `display: grid; position: relative`, each `Positioned` an absolutely positioned anchor with a `z-index`
   of its depth (WebLoweringVisitor.Containers.cs:59-157), and nothing clips unless a `Box` says `Clip`
   (BoxStyle.cs:122-125). Photon's `Stack` and `Positioned` only descend (EmitVisitor.cs:403, :412), and a `Box`'s
   `Clip` pushes its rrect.
3. **The azulejo.** Cura draws it in CSS on its sections' ids
   (`.tools/drafts/fade-1938/cura-azulejo.css`); its C# says so in comments (HeroSection.cs:46-50,
   CaregiversSection.cs:46-49).

The measurements this design rests on are in `.tools/drafts/fade-1938/measure/` (outside the repository):
`run.js` drives Chrome 157.0.8103.81 and Firefox 159.0 through Playwright at device pixel ratios 1 and 2,
`analyze.py` compares every pixel of a white box masked over black with the analytic opacity of decision 4,
`analyze2.py` reads named pixels and `elementFromPoint`, `blendspace.py` computes the blending-space difference, and
`numbers.fsx` measures .NET's formatting, `run-stops.js` and `analyze-stops.py` measure CSS's stop rules, and
`blendspace-hero.py` bounds the hero's blending difference. Results are in `results/`.

**Dependencies.** #1952 (a Bug) makes Photon's live windows run the offscreen layer phase and pools the layer targets.
It blocks #1938: until it merges, a masked layer, like every group opacity, is real only on the offscreen path. This
change proves the fade there (the Reference backend, and Metal and Vulkan in the parity suites) and once on a live
window after #1952 merges (task 5.6). #1953 owns the blending space (decision 12), #1954 the overhang over a stack's
neighbours (decision 9), and #1955 and #1956 the box gradient's findings (decision 4).

## Goals / Non-Goals

**Goals:**

- A subtree fades along a linear or a radial gradient of opacities, on both targets, drawn the same, with no change to
  its layout or to where it takes the pointer.
- The radial gradient reaches everything Cura writes and what CSS names: a centre in fractions, an ellipse in
  fractions of the box or in dp, and the four sizes CSS names.
- Photon composites the masked subtree once (no double blend), crisp at any scale, under any transform and clip, and
  reads any number of stops exactly.
- What already lets a decoration reach past its surface is pinned on both targets, and written down.
- Cura's azulejo in C#, and a golden that both targets are compared with.

**Non-Goals (fenced, each with its reason):**

- **A mask from an image or a drawing** (SwiftUI's `.mask { view }`, CSS's `mask-image: url()`). No demand, and
  Photon would composite two layers. `Mask` is a closed set, so it enters as a kind.
- **Blend modes other than `dstIn`** (Flutter's `ShaderMask.blendMode`). A fade is `dstIn`; the others are colour
  effects, #1041's ground.
- **The live window's layer phase and the layer pool:** #1952's, which this change waits on.
- **A stop without an offset** (CSS spreads them evenly). Every `MaskStop` states its offset, so nothing is inferred.
- **Circles sized by a keyword** (CSS's `circle closest-side`). An ellipse covers the demand, and a circle in dp is
  `RadialExtent.Dp(r, r)`. `RadialExtentKind` is append-only, so a circle kind can join.
- **Repeating gradients** (`repeating-radial-gradient`): no demand.

## Decisions

### 1. Flutter's answer, and which tree

Flutter answers with `ShaderMask(shaderCallback: (Rect bounds) => Shader, blendMode: BlendMode.modulate, child)`, and
a fade is written with `blendMode: BlendMode.dstIn` and a `LinearGradient` or `RadialGradient` of colours whose alpha
is read. It is a question about the **RenderObject tree** (the third tree): `RenderShaderMask` pushes a
`ShaderMaskLayer`, which composites the child into a save layer and draws the shader over it with `dstIn`. Our
`VisualNode` is the first tree, and the realizers are the painters, so the answer is a node in the vocabulary and a
layer in Photon's display list. `docs/FLUTTER-PARITY.md` has no row for it yet; task 6.2 adds one.

What we take: a single-child node, the bounds of the child as the gradient's box, `dstIn` as the only mode.

Where we do better:

- Flutter's `RadialGradient` is a circle whose radius is a fraction of the shorter side; an ellipse needs a
  `GradientTransform`. Ours is an ellipse sized in fractions of each axis, in dp, or by CSS's four sizes.
- Flutter's stops are colours whose alpha is read, so a mask written in brand colours reads as alpha by accident. Ours
  are opacities, which is all a mask means.

### 2. A node around any child, not a member of `BoxStyle`

The case for the node:

- **Principle 15:** a capability is a node around any child. A fade applies to a `Text`, an `Image`, a `Row`, a
  `ScrollView`'s content as well as a box; a `BoxStyle` member would reach only boxes, and the next demand ("fade the
  end of this list") would ask for a wrapper `Box` that exists to carry it.
- **Flutter and every native toolkit** make it a wrapper (`ShaderMask`, SwiftUI's `.mask`, Compose's `graphicsLayer` +
  `drawWithContent`).
- **Photon:** a layer around a subtree is what the emitter already does for a wrapper's descent
  (EmitVisitor.cs:96-121); a box member would put a layer inside the box's own emission, where the chrome and the
  children are split.

The case for a member, and why it loses: `BoxStyle.Opacity` is a member, and the box is Cura's surface. But opacity is
a member because a box's opacity glides with its `Transition` (EmitVisitor.cs:85-93), and a mask has no glide in this
change. A box that fades is `Masked(mask, Box(...))`, one line.

**Both** is refused: two ways to write one thing, and the reader would have to learn which composites first.

### 3. The names

The vocabulary speaks no target's language (principle 5). `mask-image` is the web's; "mask" is not: Flutter
(`ShaderMask`), Core Animation (`CALayer.mask`), SwiftUI (`.mask`), Android (`PorterDuff` masks in its docs), Figma and
every compositor say it. "Shader" is the GPU's word for how Flutter implements it, and our gradient is not a shader.

| Name | What | Why |
|---|---|---|
| `Masked` | the node | the participle the vocabulary uses for a wrapper that gives its child one property (`Positioned`, `Anchored`, `Pinned`, `Simulated`) |
| `Mask` | the closed set | what the node takes |
| `LinearMask`, `RadialMask` | its kinds | the gradient's two shapes, named as Flutter names its gradients |
| `MaskStop` | `(Offset, Opacity)` | an opacity at a fraction of the ray |
| `RadialExtent`, `RadialExtentKind` | the ellipse's size | CSS's "ending shape" size, in neutral words; reusable by #1041's gradient kind |

`Fade` was the issue's verb, and is refused as the node's name: `PresenceMotion.Fade` (Nodes/PresenceMotion.cs:7)
already means an animation. The owner chose these names.

### 4. The geometry, one normative C# answer

The gradient's box is the node's bounds (the child's, since the node is layout-transparent). Every formula lives on
the record (principle 11), and Photon calls it; the web writes CSS and lets the browser compute it, so the twin ports
none of it.

- **`LinearMask(float angle, …)`**: the angle in degrees, clockwise from "toward the top" (CSS Images 3 §3.1, and
  every design tool's angle). With d = (sin θ, −cos θ) and the box W × H, the gradient line runs through the centre
  with length L = |W·sin θ| + |H·cos θ|, so its ends touch the corners; t = ((p − c)·d) ÷ L + 0.5. `Axis(Size box)`
  returns the two ends, which is what `Paint.Linear` takes.
- **`RadialMask(Point center, RadialExtent extent, …)`**: the centre as fractions (cx·W, cy·H), the rule
  `Pattern.Anchor` and `Image.FocalPoint` follow. t = √(((x − cx)/rx)² + ((y − cy)/ry)²). `Radii(Size box)`:

  | Extent | rx, ry |
  |---|---|
  | `Fraction(fx, fy)` | fx·W, fy·H |
  | `Dp(x, y)` | x, y |
  | `ClosestSide` | min(cx, W − cx), min(cy, H − cy) |
  | `FarthestSide` | max(cx, W − cx), max(cy, H − cy) |
  | `ClosestCorner` | √2 × the `ClosestSide` radii (CSS: the ratio of closest-side, through the closest corner) |
  | `FarthestCorner` (the default) | √2 × the `FarthestSide` radii |

  A zero radius on an axis (a centre on an edge with `ClosestSide`) gives CSS's degenerate ellipse: opacity is the last
  stop's everywhere but the centre line. `Radii` returns it, and the composite treats t as +∞ off that line.
- **The stops, as CSS has them** (CSS Images 4 §3.5.3, measured below). A mask takes **one or more** stops, any
  number. `Mask.Resolved` is the list after the fix-up, the one list both targets consume:
  - a stop whose offset is below an earlier stop's offset moves up to the largest earlier one;
  - repeated offsets stay, and two stops at one offset are a hard step;
  - offsets outside 0..1 stay, and the opacity is interpolated between them as on any other offset;
  - a single stop is that opacity everywhere.

  `OpacityAt(t)` over the resolved list is piecewise linear: the first stop's opacity before it, the last one's after
  it, and at a hard step the later stop's from the step on.
- **What is refused** (each an `ArgumentOutOfRangeException` naming its parameter, as slice 1's refusals do): no
  stops (`stops`), an offset that is not finite (`stops`), and an opacity outside 0..1 (`stops`). CSS clamps an alpha
  outside 0..1 where it parses one; a typed opacity outside it is a mistake in C#, and slice 1 refuses
  `DrawingPattern.Opacity` outside 0..1 for the same reason.

**Measured, the stop rules** (`results/stops-analysis.json`, Chrome 157 and Firefox 159, a 400 px `to right` mask
read per pixel against the rules above):

| Case | Both engines | Against the rules |
|---|---|---|
| no stop (`linear-gradient(to right)`) | refused: the computed value is `none` | refused (`stops`) |
| one stop, with and without an offset, linear and radial | accepted, a solid opacity | ≤ 0.5/255 |
| `#000 60%, transparent 30%` | the second stop moves to 60%, a hard step | 0 |
| `#000 0%, …/0.5 70%, transparent 40%` | the third stop moves to 70% | ≤ 0.5 (Chrome), 0.92 (Firefox) |
| three stops at 50% | opaque before, transparent from 50% | 0 |
| a step inside a ramp (`…0.5 50%, …0.2 50%`) | the later stop wins from 50% | ≤ 0.49 / 0.89 |
| `-50%` to `150%`, linear and radial | 0.75 at t = 0, 0.25 at t = 1, as extrapolated along the line | ≤ 0.5 / 0.74 |
| every offset past 1, every offset before 0 | the first stop's opacity, the last stop's | 0 |
| eight stops | piecewise linear | ≤ 0.51 / 0.95 |

**Measured** (`results/mask-analysis.json`): Chrome and Firefox, at ratios 1 and 2, match this model at every pixel
centre within 0.5/255 for the hero's ellipse, `closest-side` on a square, a wide and an off-centre box, an off-centre
`farthest-corner`, and a 135° linear mask; within 1.0/255 (Firefox) for a three-stop 200° linear mask; and exactly (0)
at a hard stop, which neither engine antialiases. Firefox and Chrome compute the same `mask-image` for Cura's
`ellipse 70% 70% at 70% 35%, #000 35%, transparent 76%` and for the form decision 5 writes.

**Interpolation:** a mask reads only alpha, and alpha interpolates linearly in both premultiplied and straight
interpolation, so the question does not arise for a mask. (For colour gradients the browsers interpolate premultiplied:
`linear-gradient(red, transparent)` over white measures (255, 128, 128) at its middle in both engines, where straight
interpolation would give (192, 128, 128). Photon's box gradient interpolates straight: #1956. Its diagonal keywords
differ from CSS's on a box that is not square: #1955.)

### 5. The web: a wrapper with `mask-image`

`LowerMasked` writes one `div` with `mask-image` and the child inside it, the shape `LowerLoopMotion` has
(WebLoweringVisitor.Interaction.cs:419-437). The wrapper stands in for its child's layout as every wrapper does: it
joins `Fills`, `CapsAt` and the lists the layout spec's "Every layout-transparent wrapper passes its child's fill
through" names, in C# and in the twin.

`MaskCss : IMaskVisitor<…, string>` writes, through `TokenCss`'s four-decimal formatter:

| Mask | CSS |
|---|---|
| `LinearMask(135, [(0, 1), (1, 0)])` | `linear-gradient(135deg, rgb(0 0 0 / 1) 0%, rgb(0 0 0 / 0) 100%)` |
| `LinearMask(90, [(0.6, 1), (0.3, 0)])` | `linear-gradient(90deg, rgb(0 0 0 / 1) 60%, rgb(0 0 0 / 0) 60%)`: the resolved stops |
| `LinearMask(90, [(0.3, 0.5)])` | `linear-gradient(90deg, rgb(0 0 0 / 0.5) 0%, rgb(0 0 0 / 0.5) 100%)`: one stop written twice |
| `RadialMask((0.7, 0.35), Fraction(0.7, 0.7), [(0.35, 1), (0.76, 0)])` | `radial-gradient(70% 70% at 70% 35%, rgb(0 0 0 / 1) 35%, rgb(0 0 0 / 0) 76%)` |
| `RadialMask((0.5, 0.5), ClosestSide, …)` | `radial-gradient(closest-side at 50% 50%, …)` |
| `RadialMask(…, Dp(120, 80), …)` | `radial-gradient(120px 80px at …, …)` |

The web writes `Mask.Resolved`, never the stops as given, so the browser's fix-up has nothing left to do and the
two targets cannot follow two readings of one list. A single stop is written as the same stop at 0% and 100%: Chrome
157 and Firefox 159 accept a one-stop gradient (CSS Images 4), and the doubled form is valid in every engine, Safari's
older ones included. The shape keyword is omitted: two lengths, and every size keyword alone, mean an ellipse. **Measured** with
`numbers.fsx`: `0.7f`, `0.35f`, `0.76f`, `0.055f` and `0.325f` times 100 write `70`, `35`, `76`, `5.5` and `32.5`
from both a float and the double a twin holds after `Math.fround`, so SSR and the twin agree.

What the browsers do with it, **measured** in both engines:

- **Clipping:** content that overflows the wrapper (an absolutely positioned child at −40 px, a 20 px spread shadow) is
  cut at the wrapper's border box (`mask-clip: border-box`), and the mask's box is a rectangle whatever the element's
  `border-radius`. Photon does the same (decision 6: the composite is 0 outside the node's rect).
- **An ancestor's `overflow: hidden` with a radius** clips the masked subtree at the rounded corner, as Photon's clip
  clips the composite.
- **Hit-testing:** `elementFromPoint` at a point the mask hides entirely returns the masked element, and a button
  under that point returns the button. A mask changes no hit test, which is the rule Photon keeps (decision 7).
- **Painting order:** a mask makes the wrapper a stacking context, painted as if positioned at `z-index: 0`, exactly
  as `opacity` below 1 already does. Inside a `Stack` the cells carry their own `z-index`
  (WebLoweringVisitor.Containers.cs:72-75), so nothing changes there.

`HtmlStyle.MaskImage` transcribes the DOM (principle 5's escape hatch). The unprefixed property is enough for Chrome
120+, Firefox 53+ and Safari 15.4+; task 2.8 opens the page in iOS Safari too.

### 6. Photon: a group layer that carries a mask

**The smallest robust addition is the existing layer, given a mask**, not a new scope kind: every walker that pairs
`BeginLayer` with `EndLayer` (`FindTopLevelBackdrops`, `RenderLayers`, `RenderLayerRange`, `MatchingEnd`, the
builder's balance check, the Reference backend's stack) then works unchanged, nesting included.

- **The command:** `BeginLayer` as today, plus, for a masked layer, slot reuse in the idiom slice 1 set:
  `Paint.Kind` is `LinearGradient` or `RadialGradient` (a plain layer keeps `Solid`, its default), `Paint`'s two points
  carry the geometry exactly as `Paint.Linear` and `Paint.Radial` carry it (the axis ends, or the centre and the radii,
  in local space), `Shape.Rect` is the node's local rect, `Transform` local to device, `Clip` the enclosing clip,
  `StrokeWidth` the alpha (1 for a mask alone), and `TextureId` the stop texture in the frame's texture table. No
  field is added to `DrawCommand`, and no new table to `DisplayList`.
- **The stop texture**, a new data format:
  - `TextureFormat.R32Float = 2`, appended: one IEEE 754 single per texel, four little-endian bytes, **never
    decoded** (no sRGB view, no normalization) and **read only by `Load`**, never through a sampler, so no filter can
    touch it. `TextureData.Floats(int width, int height, ReadOnlySpan<float> values)` builds one.
  - The stops' texture is N × 2: row 0 the resolved offsets, row 1 the opacities, N the stop count.
  - Metal creates it as `MTLPixelFormatR32Float`, Vulkan as `VK_FORMAT_R32_SFLOAT`, both with sampled usage (Vulkan
    requires `SAMPLED_IMAGE` for that format on every device; linear filtering, which it does not require, is never
    asked for). Each backend's `CreateTexture` chooses the format and the bytes per row from a table keyed by
    `TextureFormat`, replacing the `rgba` boolean both have today (MetalDevice.cs:139-146, VulkanDevice.cs:197-198).
  - It binds to the coverage slot, which the shader declares `Texture2D<float>` (Sdf.slang:26): a one-channel float
    texture is what that declaration reads.
- **The cache, `MaskStopCache`**, per host beside `PatternTileCache` and threaded through `MotionScope` as it is
  (MotionScope.cs:40):
  - **The key:** the resolved stops' value, a `StopsKey` holding the floats' bits with a hash computed once (the
    fingerprint) and an equality that compares every float, so two masks that hash alike never share a texture. A
    `ConditionalWeakTable<Mask, TextureData>` in front of it answers a mask instance seen before without hashing.
  - **Eviction:** least recently used, past 256 entries. An entry is 8·N bytes.
  - **Per frame:** nothing while the masks are unchanged. The renderer keeps an uploaded texture while frames draw it
    (Rhi.cs:741-768, `RetainFrames`), and the cache hands it the same instance, so a steady page uploads nothing. A
    new mask costs one upload of 8·N bytes. Per pixel inside the node's rect, the composite reads the N offsets and,
    at the segment it lands in, two opacities: N + 2 `Load`s, against the one `Load` of a plain layer. Pixels outside
    the rect are discarded before the loop.
- **The Reference backend (normative):** `BeginLayer` pushes a fresh surface as today; `EndLayer` of a masked scope
  composites each pixel as `layer × OpacityAt(t(local)) × alpha × clip`, with local = the inverse transform of the
  pixel centre, t from `LayerMask.ParameterAt` (unclamped), the opacity from the stop texture's floats with the rules of
  decision 4, and 0 outside `Shape.Rect`. `Paint`'s own parameter cannot serve: it is private and clamped to 0..1
  (`GradientPositionAt`, Paint.cs:98), and a stop may sit past 1. A degenerate radius gives t = +∞.
- **The shader:** a new entry `masked_layer_composite` in `Sdf.slang`, transliterating the Reference. Uniform reuse,
  no new slot: `inv0`/`inv1` the node's inverse transform (device → local; `layer_composite` uses identity there),
  `rect` the local rect, `gradient` the paint's four floats, `flags.y` 1 linear or 2 radial, `flags.w` the stop count,
  `flags.z` and `clipRect`/`clipRadii` the clip, `colorA.a` the alpha. The layer target is loaded by device pixel
  (`colorTexture.Load(int3(pixel, 0))`, since it is full size), the stops by `coverageTexture.Load(int3(i, row, 0))`.
  `RhiPipelineKind.MaskedLayerComposite` is appended, the Metal and Vulkan devices register it, and
  `scripts/generate-shaders.sh` regenerates `.metal`, `.spv` and `.metallib`.
- **Why a data texture and not a ramp:** the browsers draw a hard stop with no antialiasing (measured: 0 difference),
  and a filtered 256-texel ramp would soften it by 1/256 of the ray (0.8 px on a 200 px ray). Read texel by texel, the
  stops are exact at any count.

### 7. Photon: the live window is #1952's

`RenderToDrawable` (MetalBackend.cs:48-53) and `VulkanSwapchain.Present` (VulkanSwapchain.cs:52-66) call
`RhiRenderer.Encode` with no layer phase, so `EncodeRange` falls back to per-command alpha (Rhi.cs:620-628) and skips
every `BackdropBlur` (Rhi.cs:548). Only `Render`, the offscreen path the goldens and parity tests take, runs phase one
(Rhi.cs:322-336). A mask has no per-command approximation, so on a live window it can draw only once the window paths
run phase one. That is Bug #1952, which also pools the layer targets, and it blocks #1938.

This change therefore:
- proves the fade on the offscreen path: the Reference backend, and Metal and Vulkan in the parity suites;
- adds no fallback for a masked layer in `EncodeRange`. With no phase one, the masked scope's commands draw unmasked,
  as today's fallback draws an opacity scope at a scaled alpha, until #1952 removes the fallback for both;
- proves the mask on a live window once, after #1952 merges (task 5.6).

### 8. Photon: what a masked layer costs, and how it behaves

| Concern | Answer |
|---|---|
| **Per frame** | One render target of the surface's size per layer, created and disposed every frame today (Rhi.cs:475, :487-499), one pass, one fullscreen-triangle composite with N + 2 `Load`s a pixel inside the node (decision 6). The composite costs what any draw costs, since every draw is a fullscreen triangle with a discard (Sdf.slang:97-103). The allocation does not: 20.7 MB at 2880 × 1800. #1952 pools the targets for every layer; this change adds no allocation of its own beyond the stop texture, uploaded once per distinct mask. |
| **Transforms** | The mask is in the node's local space: it rotates, scales and skews with the node, as `mask-image` follows its element's transform. A child's own transform moves the child under the mask on both targets. |
| **Clips** | The enclosing clip is the command's `Clip`, applied to the composite; clips inside the subtree apply inside the layer. The fence on rotated clips (an AABB, DisplayList.cs:534-540) is unchanged. |
| **Scale** | The layer is rendered at device size and the mask is evaluated per device pixel, so it is crisp at any scale, with nothing cached. |
| **Hit-testing** | Unchanged. The input targets are settled past the layer as the opacity path settles them (EmitVisitor.cs:110-121); the mask never reaches `PhotonHost`'s hit test, as the browsers' never reaches theirs (decision 5). |
| **Nesting** | A mask inside a group opacity, and the reverse, compose through phase one's innermost-first recursion (Rhi.cs:458-483). |

### 9. Past the edges: what already works, and what was missing

**What works on both targets today** (read in the code, then measured on the web):

- A `Positioned` at negative offsets, or with fractions, places its child past the stack and out of the stack's layout
  (Nodes/Positioned.cs:3-14; WebLoweringVisitor.Containers.cs:117-157; Photon measures it against the stack, the
  layout spec's "A positioned child is placed by fractions of its stack and of itself").
- Paint order is child order: the decoration as the stack's **first** child paints behind every later child and above
  the background of the box the stack sits in, because that box paints before its children on both targets
  (EmitVisitor.Chrome.cs:180-232; the web's cells and anchors carry `z-index` by depth,
  WebLoweringVisitor.Containers.cs:76-83, :152, :208).
- A surface with `Clip = true` clips it: `overflow: hidden` on the web, `PushClip` on Photon. **Measured:** with the
  box clipped and rounded, the decoration does not reach its neighbours in either engine (`results/points-analysis.json`,
  `stack` with `clip=1`).

So **item 2 of #1938 needs no new member.** What was missing is the proof and the words: a parity test of the
overhang on both targets (tasks 4.2 and 4.3) and the wiki's section (task 6.1).

**What does not hold, measured, and is not this change's:** without a clip, the web paints a `Positioned` that leaves
its stack **above the stack's neighbours**, the one before and the one after, and it takes their pointer
(`elementFromPoint` answers the decoration over both). Photon paints in tree order, so the neighbour after the stack
paints over the decoration. `isolation: isolate` on the stack does not change it (measured): a positioned stack paints
after in-flow neighbours whatever its own stacking. That is Bug #1954. Cura never meets it: its slabs clip, and its
hero's field overhangs empty padding.

### 10. The azulejo in C#

**The tile**, one `VectorDrawing(0, 0, 58, 58, shapes)`, the seven CSS layers drawn bottom to top (CSS lists the top
layer first):

| CSS layer | Shape | Path |
|---|---|---|
| `radial-gradient(circle at 0 0, c 0 32%, transparent 33%)` and the three other corners | a circle at each corner, r = 26.658 | `M-26.658 0A26.658 26.658 0 1 0 26.658 0A26.658 26.658 0 1 0 -26.658 0Z` and its three translations |
| `radial-gradient(circle at 50% 50%, c 0 12%, transparent 13%)` | a circle at the centre, r = 5.1265 | `M23.8735 29A5.1265 5.1265 0 1 0 34.1265 29A…Z` |
| `linear-gradient(c 0 2px, transparent 2px)` | the top band | `M0 0H58V2H0Z` |
| `linear-gradient(90deg, c 0 2px, transparent 2px)` | the left band | `M0 0H2V58H0Z` |

**The radii:** a `circle` with no size is `farthest-corner`. From a corner of a 58 px tile that is 58√2 = 82.0244 px, so
32% to 33% is a ramp from 26.248 to 27.068 px; from the centre it is 29√2 = 41.0122 px, so 12% to 13% runs from 4.9215
to 5.3316 px (`numbers.fsx`). A vector circle at the ramp's middle, **26.658** and **5.1265**, has its antialiased
half-coverage contour exactly where CSS's ramp is at half opacity; only the edge's softness differs (CSS's ramp is
0.82 px and 0.41 px wide, a rasterizer's edge about 1 px). So yes, the gradients become circles, and nothing in the
vocabulary needs to imitate a gradient ramp. A circle past the tile is cut by the tile on both targets: the SVG
document's viewport clips on the web, and the compositor's texture is the tile on Photon.

**The paint:** each field is its own drawing with fixed colours (Cura's palette, `VectorPaint` solid colours with
alpha): the hero's lines `#FAF7F2`, centre `#C5DDD3`, corners `#D3E5DD` over a field of `#E6EFEA`; the slabs' lines in
the slab's colour over white circles at their opacities (centre 0.10 / 0.08 / 0.12, corners 0.07 / 0.055 / 0.08).

**The fields:**

```csharp
// The hero: the field behind the profile, past its top and end, fading toward the start and the bottom.
Stack(width: SizeValue.Fill, children:
[
    new Positioned(
        Masked(new RadialMask(new(0.7f, 0.35f), RadialExtent.Fraction(0.7f, 0.7f), [new(0.35f, 1), new(0.76f, 0)]),
            Box(new BoxStyle { Width = SizeValue.Fill, Height = SizeValue.Fill, CornerRadius = new(28),
                Background = Azulejo.HeroField, Patterns = [new DrawingPattern(Azulejo.HeroTile, 58)] })),
        top: -64, end: -80, bottom: 64, start: 80) { StartFraction = 0.08f, BottomFraction = 0.04f },
    Column(…the profile…),
])

// A slab: the surface clips, the stack fills it, the field sits in the corner, the content keeps its padding.
Box(new BoxStyle { Background = Bark, CornerRadius = new(20), Clip = true }, Stack(children:
[
    new Positioned(Masked(new RadialMask(new(0.5f, 0.5f), RadialExtent.ClosestSide, [new(0, 1), new(1, 0)]),
        Box(new BoxStyle { Width = 240, Height = 240, Patterns = [new DrawingPattern(Azulejo.BarkTile, 58)] })),
        top: -60, end: -60),
    Box(new BoxStyle { Padding = … }, content),
]))
```

`width: 92%` with `right: -80px` is an anchor on both edges: the start edge is W − 0.92 W − 80 from the end, so
`StartFraction = 0.08` with `Start = 80`; the bottom likewise, `BottomFraction = 0.04` with `Bottom = 64`. CSS's
`::before` is placed in its section's padding box; in C# the stack sits inside the padded content, so a slab moves its
padding inside the stack, as above.

**Where it lives:** `Azulejo.cs`, the tile and the four fields as static members with no page attribute, in
`samples/DefaultUIDashboard/Screens/`, linked into `samples/PhotonDesktop` and into the native test project with
`<Compile Include="…" Link="…" />`, MSBuild's own way to compile one file twice (principle 9). A new page
`AzulejoScreen` (`/azulejo`, linked from `/desk` and listed in `ConsoleLayout`) in the dashboard and a screen in
PhotonDesktop compose them. A new page rather than `/desk`: the desk is slice 1's tiles in light, dark and contrast,
and the azulejo is a brand's page whose colours are fixed.

### 11. The cross-target golden

**What exists:** a "cross-pin" here means the C# realizer and its TypeScript twin asserting the same output
(StackRealizerTests.cs:31-33, `ComponentParityFixtureTests`). Photon's pixels are held by `GoldenImage`
(tests/eQuantic.UI.Native.Engine.Tests/Golden/GoldenImage.cs:11-18: per channel, ±2/255, regenerated with
`EQ_UPDATE_GOLDENS=1`) and by `GpuParity`, which holds Metal and Vulkan to the Reference. Nothing compares a browser's
pixels with Photon's, and `tests/e2e` (Playwright, Chromium only) does not run in CI.

**The instrument:** one PNG per scene, written by the Reference backend from the shared `Azulejo.cs`, is the golden.
The Photon side matches it as every golden does (±2/255). The web side is a C# script in CI's `samples` job,
`scripts/check-cross-target-goldens.cs`, which serves the dashboard, opens `/azulejo` in headless Chrome over the
DevTools protocol (the client `measure-edit-to-view.cs` already has, scripts/measure-edit-to-view.cs:357-380), takes
each scene's element at a device scale of 2, and compares it with the same PNG. It fails, naming the scene, the
worst pixel and an amplified diff. A Playwright spec in `tests/e2e` runs the same comparison in Firefox locally.

**The scenes and their tolerances:**

| Scene | What | Tolerance |
|---|---|---|
| `fade-probe` | a white box under each mask of decision 4, the stop cases of the measurement table included, on a transparent ground; the **alpha channel** compared | 2/255 at every pixel: the browsers are within 0.5 for the geometry and 0.95 for the stop rules (measured), the Reference within 1 (task 3.9), and the PNG quantizes |
| `azulejo-hero` | the hero's field over the page's cream | per channel, 4/255 at 99% of pixels and 24/255 at any. The 4 is the blending-space bound below (3/255) plus the PNG's quantization (1); the 24 is a **claim** for the antialiased edges of the tile's circles, where two rasterizers differ, which task 5.4 measures on the first run and writes, never larger, into the script |
| the slabs | the three dark corners | **not compared across targets**: #1953 (decision 12) |

**Is the hero within tolerance under Photon's linear-light blending?** Yes, by computation (`blendspace-hero.py`).
The mask's fade is a translucent composite of the field over the cream at every opacity from 0 to 1, and the field
itself holds antialiased edges between its colours. Over every pair of the field's colours (the field `#E6EFEA`, the
lines `#FAF7F2`, the centre `#C5DDD3`, the corners `#D3E5DD`), at every edge coverage and every mask opacity in steps of
1/255, the browsers' sRGB-encoded blend and Photon's linear-light blend differ by **at most 3/255**, at the centre
circle (`#C5DDD3` on the field) at an opacity near 0.53. The colours are close to the cream, which is what keeps the
difference small; the dark slabs, measured the same way, reach 51 to 56/255.

The alpha probe is what makes the fade comparable at all: it reads the mask itself, independent of how each target
blends.

### 12. A finding that bounds the golden: the targets blend in different spaces

Photon blends in linear light (Color.cs:3-6: "rendering happens in linear premultiplied space"); both browsers blend in
sRGB-encoded values. **Measured:** white at `opacity: .5` over black is 128 in Chrome and Firefox; Photon's rule gives
188. For the azulejo's colours: the hero differs by at most 3/255 over every pair, edge and opacity
(`blendspace-hero.py`; the first pass, `blendspace.py`, left out the centre circle and said 2), but the slabs' white
at 5.5% to 12% over bark is (73, 51, 47) on the web and (89, 77, 75) on Photon, a difference of up to 51/255 on bark
and 56/255 on the final call's green. Slice 1's translucent tiles carry the same difference.

That is not this change's to fix: it is every translucent paint, and Flutter, like the browsers, blends sRGB-encoded
values. It is Bug #1953. Until it lands, the slabs are held by each target's own golden (Photon's PNG, and the web's
component pins of their CSS), and the cross-target script lists them as fenced, pointing at #1953.

### 13. #1041 is not blocked

`Mask` is its own closed set and touches neither `Pattern`, `LinearGradient` nor `RadialGradient`. When #1041 gives the
box's gradient kind stops and an extent, `RadialExtent` and the geometry of decision 4 are there to take; the opacity
stops stay the mask's, because a material's stops are colours.

## Risks / Trade-offs

- **[A long stop list]** The composite reads N + 2 texels a pixel. → N is what the developer wrote; a fade has two to
  four, and eight measured costs nothing visible. A binary search over the offsets is the remedy if a profile ever
  shows one.
- **[A new texture format on two GPUs]** `R32Float` is new to both backends. → It is created only for stops, read only
  by `Load`, and held by the Metal and Vulkan parity suites through the `masked-layer` golden.
- **[Full-size layers]** A mask over a small node still renders a surface-sized layer. → #1952's pool removes the
  allocation; a bounded layer (a sub-target at the node's device bounds) is a later optimization that needs a device
  offset in every draw, and is not needed for correctness.
- **[On a live window before #1952]** A masked subtree draws unmasked there. → #1952 blocks #1938, so the pull request
  merges after it, and task 5.6 looks at the window.
- **[A stacking context per mask on the web]** A masked subtree outside a `Stack` paints above later in-flow
  neighbours it overlaps, as `opacity` does. → Overlap outside a `Stack` is rare in the vocabulary (no negative
  margins); the wiki says it.

## Migration Plan

Nothing to migrate: every member is new. Cura replaces its `landing.css` azulejo with the C# of decision 10 and drops
the four ids' rules.

## Open Questions

None left to the owner. Two claims are left to measure, each by its task: Vulkan and Metal on the live window
(task 5.6, after #1952), and the `azulejo-hero` scene's edge tolerance (task 5.4).

The owner decided: the names of decision 3; any number of stops, exact, read from a data texture (decision 6); the
live window's layer phase and the pool as #1952; the blending space as #1953, with the hero compared across targets and
the slabs fenced; the golden as a CI script over headless Chrome plus a local Firefox spec; the `/azulejo` page and its
PhotonDesktop screen from one linked `Azulejo.cs`; the overhang over neighbours as #1954; the gradient findings as #1955
and #1956; and one pull request whose parts are the sub-issues of tasks.md's sections.
