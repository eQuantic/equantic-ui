## Context

`BoxStyle.Pattern` is a `GridPattern?` (src/eQuantic.UI.Primitives/Nodes/BoxStyle.cs:67), and `GridPattern` is a
readonly record struct that its own doc calls "deliberately a closed shape … not a general pattern/texture API"
(Nodes/GridPattern.cs:3-13). It reaches four places:

- **The web's C# lowering:** `BackgroundLayers` and `BackgroundLayerSizes`
  (src/eQuantic.UI.Web/WebLoweringVisitor.Containers.cs:1468-1488), `PaintsNothing` (:837), the hover's re-layering
  (:1163-1168), and `TokenCss.GridPattern` and `GridPatternSize` (src/eQuantic.UI.Web/TokenCss.cs:334-347).
- **The twin:** `BoxStyle.pattern` (src/eQuantic.UI.Runtime/src/shared/vocabulary.ts:254, 299), `GridPattern` (:1607),
  `GridPatternValue` (nodes.ts:285, 385), `gridPatternValue` and `gridPatternSize` (lowering.ts:2121-2135),
  `paintsNothing` (:3793), the layer lists (:4252-4268), and the exports (runtime-exports.ts:166, index.ts:185).
- **Photon:** `EmitChrome`'s `pattern` parameter and `EmitGridPattern`
  (src/eQuantic.UI.Native.Components/EmitVisitor.Chrome.cs:180-198, 329-343), called from :121-123. The rules start
  at the bounds and are bounded by them, neither placed in the padding area nor clipped to the radius: that is #1939.
- **Tests and fixtures:** `GradientShimmerRealizerTests` (:101, :146), `S5StateRealizerTests`,
  `ComponentParityFixtureTests:494` with `component-parity.spec.ts:283`, `gradient-shimmer.spec.ts:117-199`,
  `GradientShimmerTests` (Photon, :86-123), `primitives-types.fixture.json:139` and
  `roslyn-completions.fixture.json:1229`.

Nothing else reads it. The email realizer, the design hosts, `StyleDiff` (which has no pattern member), the SDKs, the
templates and the samples never mention it. eqc has no rule of its own for a vocabulary record.

The artwork already exists as data:

- **Vector:** an app's `Assets/**/*.svg` becomes `public static readonly VectorDrawing` members of a generated
  `Vectors` class (src/eQuantic.UI.Native.Build/VectorCatalog.cs:81-88).
- **Raster:** an image is a source string. `Image(string source, …)`, with `DarkSource` for the dark artwork
  (Nodes/Image.cs:25-43). The web writes it into `src` verbatim, and pairs the two with `.eq-themed-*`
  (WebLoweringVisitor.Graphics.cs:481-530). Photon decodes it through `IImageLoader.Load(string)`
  (src/eQuantic.UI.Native.Framework/Text/IImageLoader.cs:12-15), cached per source in `MotionScope.ImageCache`
  (EmitVisitor.Media.cs:262-273).

The demand's tile (`pattern-tile.svg`, 20,043 bytes) is one root `svg` with `viewBox="0 0 675 518"` and
`fill="none"`, and 24 `path` elements filled `#C8796B`, whose data uses only `M L H V C Z`. `SvgDocument` reads every
one of these (Vector/SvgDocument.cs:171-216, its paint state :240-283, and VectorPath.cs:396 for exponents). **No gap.**

## Goals / Non-Goals

**Goals:** everything the CSS backgrounds model gives one box's background, written once and drawn the same on both
targets:

- a list of layers;
- vector and raster tiles;
- repetition per axis, with spaced and fitted repetition;
- an anchor with an offset;
- a size in dp, as a fraction, or as a fit;
- the placing area and the clip area, with the rounded clip;
- the box attachment and the window attachment;
- a tile's opacity.

On Photon, one draw per tile layer, seamless at any fractional scale, crisp at the device's scale, transformed with
the box. SSR and the twin write the same bytes, pinned. The grid gets the same placing and clipping (#1939).

**Non-Goals (fenced, each with its reason):**

- **`centerSlice` (nine-slice).** It is `Image`'s own fence (Nodes/Image.cs:17) and does not come for free. CSS
  repeats a whole image, and nine-slice is `border-image`, another property with another model. Photon would need a
  nine-rectangle draw. It enters with `Image`'s nine-slice, in the same shape on both.
- **The opacity of a raster tile** (decision 10).
- **The CSS local attachment.** It means something only for an element that scrolls its own content. No `Box` does
  that, a `ScrollView` does, so local is the box attachment here.
- **Flutter's `scale` (a raster's pixel density).** A raster tile is always sized in dp (decision 10), so a density
  has nothing left to decide.

## Decisions

### 1. Flutter's answer, and which tree

Flutter answers with `BoxDecoration.image: DecorationImage(image, fit, alignment, repeat: ImageRepeat, opacity,
centerSlice, scale)`, painted by `paintImage` inside `RenderDecoratedBox`. It is about the RenderObject tree (how a
box paints): our `BoxStyle` is the decoration, and each realizer is the painter. `docs/FLUTTER-PARITY.md` has no row
yet, and task 6.2 adds one.

What we take: `repeat` (split per axis), `fit` (our existing `ImageFit`), `alignment` (our `Point` of fractions, the
rule `Image.FocalPoint` follows, Nodes/Image.cs:55-77) and `opacity`.

Where we do better:

- Flutter holds one image per decoration, and we hold a list.
- Flutter has no spaced or fitted repetition, and no fraction size, offset, placing area, clip area or window
  attachment.

### 2. The closed set, named for #1041

The hierarchy:

```
Pattern                       abstract record, private protected constructor: Area, ClipTo, Attachment
├── GridPattern               sealed record (Cell, Color, LineWidth = 1)
└── ArtworkPattern            abstract record, private protected constructor: size, repeat, anchor, offset
    ├── DrawingPattern           sealed record (VectorDrawing artwork, …, ColorToken? tint): Opacity
    └── ImagePattern          sealed record (string source, …): DarkSource
```

- **Closing it:** a `private protected` constructor is .NET's own way to close a hierarchy to one assembly.
- **The visitor, `IPatternVisitor<in TState, out TResult>`:** one `Visit` per SEALED kind (grid, tile, image), the
  shape of `IVisualNodeVisitor` (Nodes/IVisualNodeVisitor.cs:53).
- **The twin:** each class carries `patternKind` (`'grid'`, `'drawing'`, `'image'`), and its lowering ends in an
  exhaustive `never` check, so `tsc` fails as the C# build does.
- **The C# 15 alternatives:** a `closed` hierarchy or a `union`. A visitor is what every realizer already implements
  for nodes, and what #1041 names as its instrument.
- **The names:**
  - `DrawingPattern` and `ImagePattern`: the pair takes the names of the nodes `Drawing` and `Image`, whose
    artwork each repeats (a `VectorDrawing`, an image source). The owner chose this pair in round 3 over the earlier
    `TilePattern`;
  - `ArtworkPattern`: what the two share, a piece of artwork placed and repeated.

  None of them clashes with the engine's `Texture` or `TextureData`, and none is named after a material, so #1041 can
  take the set whole.

### 3. A list, and how it composes with the slots

`BoxStyle.Patterns : IReadOnlyList<Pattern>?` replaces `Pattern`, with no second shape kept beside it.

- **The order:** the list paints in order, the last on top: the rule a `Stack` gives its children, the vocabulary's one
  precedent for layers. The web writes the list reversed, because in CSS the first layer is on top.
- **The slots:** each pattern is a layer between the background colour and the glow. The full order, bottom to top:
  background colour, `Patterns[0]` … `Patterns[n−1]`, `Glow`, `Gradient`. Photon emits it in that order
  (EmitVisitor.Chrome.cs:186-232), and the web writes the reverse: gradient, glow, `Patterns[n−1]` … `Patterns[0]`
  (WebLoweringVisitor.Containers.cs:1468-1475). For one pattern this is today's order exactly.
- **Equality:** reference equality on the list, as `BoxStyle.Shadows` already has (BoxStyle.cs:124-130).
- **#1041:** a material is an ordered stack of layers of closed kinds, which is the shape `Patterns` already has.
  #1041 adds a gradient kind and a glow kind to this set and folds `Background`, `Gradient` and `Glow` into the same
  list. The set and the list keep their shape. Only the slots beside them go.

### 4. The members, and what each one is in CSS

| Member | On | Values | Web | Photon |
|---|---|---|---|---|
| `Area` | every kind | `BoxArea.Border`, `Padding` (default), `Content` | `background-origin` | the placing rrect (decision 5) |
| `ClipTo` | every kind | `Border` (default), `Padding`, `Content` | `background-clip` | `PushClip` of that rrect |
| `Attachment` | every kind | `TileAttachment.Box` (default), `Window` | `background-attachment: scroll` / `fixed` | decision 7 |
| width, height | artwork | `TileLength`: auto (default), dp (a number converts), `Fraction(f)` | `background-size` | `ArtworkPattern.SizeIn` |
| `Fit` | artwork | `ImageFit?`: `Contain`, `Cover`, `Stretch` | `contain` / `cover` / `100% 100%` | the same, against the area |
| `HorizontalRepeat`, `VerticalRepeat` | artwork | `TileRepeat.Repeat` (default), `Once`, `Spaced`, `Fitted` | `repeat` / `no-repeat` / `space` / `round` | `Tiling` (decision 12) |
| `Anchor` | artwork | `Point` of fractions 0..1, (0, 0) by default | percentages of `background-position` | origin = area + anchor·(area − tile) |
| `Offset` | artwork | `Point` in dp, (0, 0) by default | `calc(p% + xpx)`, or `calc(p% - xpx)` for a negative | added to the origin |
| `Opacity` | tile | 0..1, 1 by default | `opacity` on the SVG's one `g` | the command's alpha |
| tint | tile | `ColorToken?`, `TextPrimary` when null | `color` on the SVG's root (decision 9) | the compositor's inherited colour |
| `DarkSource` | image | `string?` | decision 8 | the source the mode picks |

**`TileLength`** is a readonly record struct of kind and value:

- a number converts to dp, marked `[ConversionPassesThrough]` as `SizeValue` is (Layout/LayoutTypes.cs:84), so the twin
  takes a raw number as dp;
- `TileLength.Fraction(0.5f)` is half the placing area on its axis;
- the default is auto.

`SizeValue` cannot serve: its kinds are `Hug`, `Fill`, `Fixed` and `WindowMinus` (LayoutTypes.cs:20-26), and a
fraction of a placing area is not a layout size.

**The refusals**, each an `ArgumentOutOfRangeException` naming the member:

- a negative dp or a fraction outside (0, 1];
- a `Fit` beside a width or a height;
- a tile with neither a width, a height, nor a fit;
- an anchor outside 0..1 on either axis;
- an opacity outside 0..1;
- an empty source.

There is no intrinsic size. A raster's size in pixels is not a size in dp (decision 10), and requiring one rule for
both kinds keeps the web and Photon from needing to agree on a vector's viewBox units.

### 5. One placement for every kind on Photon (fixes #1939)

`PatternPlacement` (Native.Components) answers, for a pattern and a box:

- **the three areas:** the border area is the bounds with `CornerRadius`. The padding area is deflated by
  `BorderWidth` on the sides that draw a border. The content area is deflated further by `Padding`. Each area's radii
  are deflated by what was removed and floored at zero, as CSS computes inner radii;
- **the placing area and the clip area:** chosen by `Area` and `ClipTo`, with the window attachment replacing the
  placing area (decision 7).

The grid and the tiles both use it. The grid's rules then start at the placing area's origin, continue on every side
of it, run across the clip area's bounds, and are drawn inside `PushClip` of the clip area's rounded rectangle.
For a box with no border, a square corner and the default areas, the grid's commands are the ones it emits today
(`GradientShimmerTests:86-109` stays as it is), and a bordered or rounded box now matches CSS (#1939).

The size, from `ArtworkPattern.SizeIn(Size area, float aspect)`, the one C# answer, ported by the twin for nothing
(the web writes CSS and lets the browser size):

- a dp or fraction width with an auto height gives the height from the aspect;
- the reverse gives the width from it;
- two lengths are used as they are;
- a `Fit` is computed against the area.

The vector's aspect is `VectorDrawing.Aspect`. The raster's is the decoded image's, on Photon. On the web, `auto` is
written and the browser reads it.

**The position:** origin = area.start + anchor·(area.size − tile.size) + offset.

**Spaced and Fitted:**

- **Spaced:** n = floor(area ÷ tile). With n ≥ 2, the tiles start at the area's start, the period is tile +
  (area − n·tile) ÷ (n − 1), and the extent is the tile. With n < 2, one tile at the origin.
- **Fitted:** n = max(1, round(area ÷ tile)) and the tile is area ÷ n. When the other axis's length is auto, that axis
  is scaled by the same factor.

This is CSS Backgrounds 3 §3.4 and §3.9, **a claim from the specification, measured by task 0.1**.

### 6. The web's layer lists

`PatternLayers : IPatternVisitor<…>` replaces `TokenCss.GridPattern` and `GridPatternSize`. `BackgroundLayers` and
`BackgroundLayerSizes` become one value with seven lists: image, size, repeat, position, origin, clip and attachment.
Each pattern contributes one entry, and a grid two. A list is written only when one of its entries is not CSS's
default, and it is padded with the defaults for every other layer, because CSS repeats a short list cyclically:

| List | Default entry |
|---|---|
| repeat | `repeat` |
| position | `0% 0%` |
| origin | `padding-box` |
| clip | `border-box` |
| attachment | `scroll` |

The size list keeps today's rule: written when there is a pattern, with `auto` for the gradient and the glow. A box
with one default grid writes exactly what it writes today, and the component pins prove it. The hover path
(`style with { Gradient = … }`, :1163-1168) writes the same lists.

`HtmlStyle` gains `BackgroundRepeat`, `BackgroundOrigin` and `BackgroundAttachment`. `BackgroundClip` and
`BackgroundPosition` exist (Dom/HtmlStyle.cs:96-101). The escape hatch transcribes the DOM (principle 5).

### 7. The window attachment, on both targets

- **The web:** `background-attachment: fixed`. CSS then places the layer against the viewport and ignores
  `background-origin`, and the clip still applies.
- **Photon:** the placing area is the window, `MotionScope.ViewportW` by `ViewportH` (MotionScope.cs:26-27), mapped
  into the box's local space through the inverse of `DisplayListBuilder.CurrentTransform` (DisplayList.cs:429). A
  scroll moves the box, and the tiles stay still. This works on every shell, iOS included.
- **Under a transform, on both targets:** CSS Transforms 1 §6 says a fixed background on an element affected by a
  transform paints as `scroll`. Photon matches it: `EmitState` gains `UnderTransform`, set when the emitter descends
  through a box with a `Transform` or a motion that transforms. Under it, a window attachment paints as `Box`. A
  scroll's translation is not a transform here, as in a browser. **A claim from the specification, measured by
  task 0.1.**
- **Where the web falls short:** iOS and iPadOS Safari paint `fixed` as `scroll`, so there the tile moves with the
  box. **Documented WebKit behaviour, not measured here.** Task 4.9 measures it in the iOS simulator's Safari, and the
  wiki says it.

  The owner decided that the web keeps WebKit's behaviour there, documented. The workaround the web has, a
  `position: fixed` child under a `clip-path: inset(0)` box, is rejected.
  It turns one element into two for every window-attached layer, it splits the layer list across two elements, and it
  changes the hydration tree. Photon, where the SDK owns the paint, is where "properly" is affordable.

### 8. Mode and contrast on the web: toggles on `:root`

A data URL is a separate document and cannot read a CSS variable or `light-dark()` from the page. A raster's dark
source is another URL. A layer whose image differs by mode or contrast therefore selects among its URLs with
**space toggles**:

- **The theme's stylesheet** sets four custom properties on `:root`: `--eq-on-light`, `--eq-on-dark`, `--eq-on-std`
  and `--eq-on-more`. On each axis, one is empty (`--eq-on-light: ;`) and the other is `initial`. It writes them
  under exactly the selectors that choose `.eq-themed-dark` (ThemeStylesheet.cs:349-371): the declared mode, else
  `prefers-color-scheme`, and `:root[data-theme]` over both. The contrast pair goes under
  `@media (prefers-contrast: more)`. The selector list is extracted once and both families use it, so the image pair
  and the tiles cannot follow two decisions, the defect the `DarkSource` comment records.
- **The element** carries one custom property per variant and layer, for example
  `--eq-p0-dm: var(--eq-on-dark) var(--eq-on-more) url("…")`. It is valid only when both toggles are empty, because a
  custom property that references a guaranteed-invalid value with no fallback is itself guaranteed-invalid (CSS
  Variables 1 §3). The layer is `var(--eq-p0-ls, var(--eq-p0-lm, var(--eq-p0-ds, var(--eq-p0-dm))))`.
- **A raster with a dark source** uses only the mode toggles (`-l`, `-d`). A layer whose variants are all equal writes
  its URL directly, which is the demand's case.

The toggles are inherited from `:root`, so no rule per layer or per index is needed. The variants stay inline
(`CustomProperties` are per-element inputs by design, Dom/HtmlStyle.cs:251), and only for a layer that varies.

**This is a claim from the specification, measured by task 0.2 in the three engines.** Before this, I proposed a
class with a rule per mode. It needed a rule per layer index once the box took a list, and was replaced.

### 9. The SVG writer and the encodings

- **One builder:** `LowerDrawing` (WebLoweringVisitor.Graphics.cs:188-268) and `lowerDrawing`
  (lowering.ts:3389-3480) each build a shape's attributes in line. Both move into one builder per producer,
  `DrawingElements.Shapes`, which the inline drawing and the tile share, with `WebRealizer.GradientElement` and
  `GradientId` (WebRealizer.cs:95, :137). The drawing's markup stays byte-identical.
- **A writer for the tile:** `SvgMarkup` serializes the tile's tree to text:
  - single-quoted attributes in insertion order;
  - `&`, `'` and `<` as entities;
  - no whitespace.
- **The tile's document:** the root carries `xmlns`, `viewBox`, `width` and `height` (the artwork's own grid, which
  gives the browser the intrinsic ratio an `auto` length reads), `preserveAspectRatio='none'` (so two given lengths
  stretch, as on Photon), `fill='none'` and `color='<hex>'`, which answers `currentColor` inside the standalone
  document. The size list writes an auto length as `auto` for both kinds (`330px auto` for the desk), so the twin
  computes no aspect. The gradient
  `defs` stay inline, and the shapes sit inside one `g` carrying `opacity` below 1.
- **The data URL's encoding is an explicit table**, the same in both languages:
  - `%`, `#`, `"`, `<`, `>`, and code points 0 to 31 and 127 become `%XX`, with uppercase hexadecimal digits;
  - every non-ASCII code point becomes its UTF-8 bytes;
  - everything else stays as it is.

  Measured with `dotnet fsi`: `Uri.EscapeDataString("'()*!~ #<>\"%é")` gives
  `%27%28%29%2A%21~%20%23%3C%3E%22%25%C3%A9`. JavaScript's `encodeURIComponent` leaves `'()*!~` alone (documented,
  **not measured**). Neither can be the shared rule, and a fixture of every ASCII code point pins one producer against
  the other.
- **A raster's source** is written as a CSS string, `url("…")`, escaping `\`, `"` and line breaks, through the same
  table in both producers. A relative source resolves against the document, as `Image`'s `src` does, because the
  atomic sheet is inline in the page.

### 10. The raster tile

- **The source:** the same string `Image.Source` takes, with the same `DarkSource`, so an app names its artwork once
  for both nodes.
- **Density:** a raster tile is always sized in dp, by a width, a height or a fit, never by its pixels. The browser
  scales it to that size at its own pixel ratio. Photon resamples the decoded image ONCE to the tile's device size:
  - an area average when shrinking by more than 2 times, bilinear otherwise;
  - in premultiplied linear space;
  - the result is cached.

  The shader then samples it texel for texel, the same draw as a vector tile. The wiki tells a developer to supply at
  least the tile's dp times the highest scale they ship to.
- **Opacity is fenced for a raster.** CSS has no per-layer opacity. An SVG wrapper around a raster cannot load it,
  because an SVG used as an image loads no external resource. `-webkit-cross-fade()` is not in Gecko. A member one
  target cannot honour is not offered: an `ImagePattern` has no `Opacity`. **The image carries its own
  transparency**: an app that wants a quieter raster tile exports the image with the alpha it wants. The wiki says
  exactly this beside the member table.
  `DrawingPattern` has it, because the SVG carries it.
- **Photon's loader:** the compositor takes the decode from `motion.ImageLoader` through `motion.ImageCache`, so a
  source shared with an `Image` node decodes once. A host with no loader draws no image tile, as a host with no
  rasterizer draws no vector tile.

### 11. Photon's snapping

While the box's transform is a scale and a translation:

- **The period:** the period in device pixels is round(tile × s), where s is the render scale times the transform's
  scale, and the period in local units is that ÷ s.
- **The phase:** the origin is moved to the nearest whole device pixel.
- **The texture:** composited at exactly the period's device size.

So texels map one to one, crisp and uniform. Measured: at 2.625, 330 dp is 866.25 px and becomes 866, and 253.2444 dp
is 664.77 px and becomes 665. Five tiles drift at most 1.25 px from the unsnapped grid. Whether the browser snaps the
same way is open question 1 (task 0.1). Under a rotation or a skew, nothing is snapped and the sampling is nearest:
the images' fence (EmitVisitor.Media.cs:260).

### 12. Photon's one repeating draw per tile layer

- **The command:** `DrawCommandKind.TiledTexture = 9`, appended (the enum stays append-only for the reason
  `EnumValueAbiTests` gives). `Shape` is the clip area's rectangle, `Clip` is the baked rounded clip, `TextureId` is an
  `Rgba8` texture, and `Paint.Color.A` is the opacity (an `Rgba8` texture ignores the tint, DisplayList.cs:103). A new
  `Tiling` field holds the origin, the period, the extent and the repeat flags.
- **The normative math** is `Tiling.TryTexel` in the engine's C#:
  - the cell is floor((local − origin) ÷ period) on each axis, so it is negative left of and above the origin;
  - an axis that does not repeat discards every cell but 0;
  - the position inside the cell must be below the extent, which is the spaced gap;
  - the texel is min(size − 1, ⌊within ÷ extent × size⌋).
- **Its users:** the Reference backend calls it, and `tiled_rgba_fragment` in `Sdf.slang` transliterates it, as that
  file does for `Sdf.cs` (Sdf.slang:1-6).
- **No new uniform.** The slots `textured_rgba_fragment` (Sdf.slang:311-336) leaves unused carry the tiling:
  - `radii.xy`: the origin;
  - `radii.zw`: the period;
  - `colorA.xy`: the extent;
  - `colorA.w`: the opacity;
  - `colorB.xy`: the repeat flags;
  - `gradient.xy`: the texture size.
- **The pipeline:** `RhiPipelineKind.TiledRgba` is appended, and the Metal and Vulkan devices register the entry.
  `scripts/generate-shaders.sh` regenerates the three outputs.

### 13. The compositor and the cache

- **The compositor, `TileCompositor`:**
  - **A vector tile:** each shape is rasterized through the host's `IIconRasterizer` at the tile's device size, the
    way `EmitDrawing` builds its glyph (EmitVisitor.Media.cs:112-121), without `IconRasterCache`. Its coverage is
    filled with `ArtworkPaint`, `EmitDrawing`'s `RunPaint` and `Frame` (:126-171) moved into one shared object,
    evaluated per pixel by `Paint.ColorAt`. The shapes are blended src-over in premultiplied linear space, as the
    Reference backend blends (ReferenceBackend.cs:170-175, `ColorSpace` in Color.cs:26), and stored as straight
    sRGB.
  - **A raster tile:** the resampling of decision 10.
- **The cache, `PatternTileCache`:** per host, beside `IconRasterCache`, threaded through `PhotonRealizer.Realize`
  (PhotonRealizer.cs:328, :419) and `MotionScope`.
  - **The key:** what was composited (the vector's content fingerprint, memoized per instance in a
    `ConditionalWeakTable`, or the raster's resolved source), the device width and height, and the resolved inherited
    colour. Mode and contrast reach a vector's pixels only through that colour (Vector/VectorDrawing.cs:84-96), and a
    raster's only through the source the mode picks.
  - **Eviction:** least recently used first out, under a budget of 64 MiB. A tile larger than the budget is drawn and
    not kept. Opacity is a uniform and is not in the key.

### 14. What each of the owner's ten points became

| # | Point | Where |
|---|---|---|
| 1 | Repetition per axis | decisions 4 and 12 |
| 2 | Spaced and fitted | decision 5 |
| 3 | Anchor, phase on every side, and the offset | decisions 4, 5 and 12 |
| 4 | Explicit, aspect, fraction, cover and contain | decisions 4 and 5 |
| 5 | Placing and clip areas, and the rounded clip, for every kind | decision 5 |
| 6 | Box and window attachment | decision 7 |
| 7 | Seamless | decisions 11 and 12 |
| 8 | Crisp, re-rasterized on a new scale, transformed | decisions 11, 12 and 13 |
| 9 | Opacity, and the list | decisions 3 and 10 |
| 10 | Performance | below |

**Performance:** a full-window desk of 2560 × 1440 dp with a 330 dp tile spans 48 tiles, or 63 with the anchor's
extra row and column (measured with `dotnet fsi`). On Photon it is **one command per tile layer**, against 48 to 63
per-tile commands, and one composite per key. The texture is 3,009,600 bytes at 3× (measured). On the web it is one
layer per pattern, and the data URL weighs about the SVG's size (about 20 KB here), once, in the atomic class.

## Risks / Trade-offs

- **One command per tile instead of a shader** →
  - **The case for it:** it needs no shader. The Reference backend's half-open texel rule (ReferenceBackend.cs:131-134)
    leaves no gap between two rectangles that share a float edge.
  - **The case against it:** it is 48 to 63 commands for a desk. Each tile spans a fractional width (866.25 px from an
    866 px texture), so a texel column repeats irregularly. On the GPU, the seams rest on the rasterizer's
    shared-edge rule over separately rounded floats.

  **Chosen: the shader.** It costs one entry point and two appended enum values.
- **Space toggles** → a technique that relies on guaranteed-invalid propagation. Mitigation: task 0.2 measures it in
  three engines before the web's mode work starts. If one engine fails, the fallback is the per-mode class, at a rule
  per layer index.
- **iOS Safari's fixed background** → on the web there the tile moves with the box. Mitigation: documented in the
  wiki, and the rejected workaround is recorded in decision 7.
- **The CPU composite** → one pass per shape over the tile's pixels, once per key. Mitigation: the cache. No
  `StyleDiff` member reaches a pattern, so nothing animates a tint.
- **#1939 changes Photon's grid for bordered or rounded boxes.** Mitigation: that is the fix. A golden with a bordered
  grid, if any, moves, and the change says so.

## Migration Plan

The break, for the release notes and the wiki's Upgrading page:

- **`BoxStyle.Pattern`** goes, replaced by `BoxStyle.Patterns`.
- **`GridPattern`** becomes a sealed record class deriving from `Pattern`. Its parameterless constructor and the
  struct's members go.
- **`TokenCss.GridPattern` and `GridPatternSize`** go from `eQuantic.UI.Web`.
- **The migration line:** *`Pattern = new GridPattern(…)` becomes `Patterns = [new GridPattern(…)]`.*

No sample or package in the tree sets a pattern.

## Open Questions

1. **Does the browser snap a background tile's size and phase to device pixels?** And does each engine follow the
   §3.9 `round` rule for the other axis and the Transforms §6 rule for fixed? Task 0.1 measures all three. Photon
   follows what is measured.
2. **Do space toggles resolve in Chromium, WebKit and Gecko?** Task 0.2.
3. **The 64 MiB budget** is a judgment, not a measurement. The engineer measures the desk sample's resident size in
   the iOS simulator.
4. **The size of the change:** about 34 points as one PR, by the owner's decision:
   - the vocabulary: 5;
   - the web and its twin, with the toggles: 10;
   - Photon (shader, compositor, raster, cache, attachment, placement and #1939): 15;
   - the measurements: 1;
   - documentation: 3.
