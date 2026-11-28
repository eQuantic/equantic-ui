## Why

Closes #1937, the first slice of #1042 (a brand's texture behind a surface), which is a sub-issue of #1035 (any design
system in C#, with no CSS). Closes #1939 too, a Bug under #1587: on Photon a grid ignores its box's border and rounded
corners, while the web places and clips it as CSS does. The placing and clipping this change gives every pattern kind
fix it. #1041 (materials are a closed set) comes later and must be able to take this set into its materials without
reshaping it.

The demand is a back office canvas whose desk is the brand's pattern, a 675 × 518 vector tile repeated at 330 dp wide
over cream, quiet behind the sheets. Today the desk draws `GridPattern(24, …)`, because `BoxStyle.Pattern` takes only
the grid and `Image` cannot repeat.

The general capability is **a box paints layers of repeating artwork behind its content, with everything the CSS
backgrounds model gives a page**:

- several layers, repetition per axis, spaced and fitted repetition;
- an anchor with a pixel offset;
- a size in dp, as a fraction of the area, or as a fit;
- the area the tiles are placed in and the area they are clipped to;
- an attachment to the box or to the window;
- vector or raster artwork, and an opacity.

The owner set the bar: Photon reaches all of it, including where the web's own browsers fall short.

Flutter answers with `DecorationImage(image, repeat: ImageRepeat, alignment, fit, opacity, centerSlice, scale)` on a
`BoxDecoration`. That is a question about the RenderObject tree, the third tree: how a box paints.

## What Changes

- **BREAKING** `BoxStyle.Pattern` (one `GridPattern?`) becomes `BoxStyle.Patterns`, an `IReadOnlyList<Pattern>?`
  painted in list order, the last one on top, as a `Stack` orders its children. Every pattern paints above the
  background colour and below the glow and the gradient.
- **New** `Pattern`: a closed set of kinds with a visitor (`IPatternVisitor<TState, TResult>`) every realizer
  implements, so a kind a realizer cannot draw is a build error. Every kind has these members:
  - `Area`: where it is placed, a new `BoxArea` (`Border`, `Padding`, `Content`), `Padding` by default;
  - `ClipTo`: where it is clipped, `Border` by default, following the box's rounded corners;
  - `Attachment`: a new `TileAttachment`, either `Box` (the default: it moves with the box) or `Window` (it stays
    still against the window while the content scrolls).
- **The kinds:**
  - `GridPattern`: the grid as today, now a sealed record class of the set. On Photon it is now placed in the padding
    area and clipped to the rounded corners, as the web already does (#1939).
  - `DrawingPattern(VectorDrawing artwork, TileLength width = default, TileLength height = default, ColorToken? tint =
    null)` with `Opacity`: a vector drawing repeated. With no tint, the shapes the artwork leaves as `currentColor`
    take the theme's `TextPrimary`.
  - `ImagePattern(string source, TileLength width = default, TileLength height = default)` with `DarkSource`: a raster
    image repeated. It takes the same source string, and the same dark pair, that `Image` takes.
- **What the two tile kinds share** (through the abstract `ArtworkPattern`):
  - **size:** `TileLength` widths and heights, each either dp (a number converts) or a fraction of the area
    (`TileLength.Fraction(0.5f)`), with the missing one from the artwork's aspect; or `Fit` (the existing `ImageFit`);
  - **repetition:** `HorizontalRepeat` and `VerticalRepeat`, through a new `TileRepeat`: `Repeat`, `Once`, `Spaced`,
    `Fitted`;
  - **position:** `Anchor`, a `Point` of fractions with the rule `Image.FocalPoint` follows, and `Offset`, a `Point`
    in dp added to it.

  ```csharp
  Patterns = [new DrawingPattern(Vectors.PatternTile, 330) { Opacity = 0.09f }]
  ```

- **The web** writes each pattern as `background-image` layers in today's place in the list (gradient, glow, then the
  patterns from the top down), with matching `background-size`, `-repeat`, `-position`, `-origin`, `-clip` and
  `-attachment` lists:
  - a vector tile is an SVG data URL, and a raster tile is `url()` of its source;
  - a tint or a dark source that differs by mode or contrast follows the palette's decision, through mode and contrast
    toggles the theme's stylesheet sets on `:root`;
  - a window attachment is `background-attachment: fixed`. iOS and iPadOS Safari paint that as attached to the box,
    the one place the web falls short (design.md, decision 7).
- **Photon** composites each tile once into an RGBA texture at its device size, from the shapes through the
  rasterizer or from the image loader resampled, and caches it. It draws each tile layer with ONE new command,
  `TiledTexture`, which samples with repeat addressing in a new fragment entry of `Sdf.slang`. There are no seams at
  any fractional scale. A window attachment anchors the tiles to the window, including on iOS.
- **Fenced:** `centerSlice` (nine-slice), `Image`'s own fence; and the opacity of a raster tile, which CSS cannot
  give one layer (design.md, decision 10).

What changes for a developer: the C# above. The migration line, for any app that set the grid:
*`Pattern = new GridPattern(…)` becomes `Patterns = [new GridPattern(…)]`.*

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `box-paint`: a box paints a list of patterns from a closed set of kinds (a grid, a vector tile and a raster tile),
  with the CSS backgrounds model on both targets.

## Impact

Parts reached, in the order the work goes:

- **The vocabulary** (`eQuantic.UI.Primitives`): `Pattern`, `IPatternVisitor<,>`, `GridPattern`, `ArtworkPattern`,
  `DrawingPattern`, `ImagePattern`, `TileLength`, `TileRepeat`, `BoxArea`, `TileAttachment`, and `BoxStyle.Patterns`.
  **Public surface moves**:
  - removed (`*REMOVED*`): `BoxStyle.Pattern` and the record struct's members;
  - declared: the new types;
  - the three enums enter `EnumValueAbiTests`.
- **The web realizer** (`eQuantic.UI.Web`):
  - `TokenCss.GridPattern` and `GridPatternSize` go;
  - `HtmlStyle` gains `BackgroundRepeat`, `BackgroundOrigin` and `BackgroundAttachment`, transcribing the DOM;
  - `ThemeStylesheet` writes the mode and contrast toggles;
  - **public surface moves** in Web's `PublicAPI.Unshipped.txt`.
- **The runtime's twin** (`eQuantic.UI.Runtime`): `vocabulary.ts`, `nodes.ts`, `lowering.ts`, `runtime-exports.ts`,
  `index.ts`, and the generated enum unions.
- **Photon** (`eQuantic.UI.Native.Engine`, `.Engine.Metal`, `.Engine.Vulkan`, `.Engine.Reference`,
  `eQuantic.UI.Native.Components`):
  - `DrawCommandKind.TiledTexture` and `RhiPipelineKind.TiledRgba`, both appended;
  - `Tiling`;
  - `tiled_rgba_fragment` in `Sdf.slang`, with `Sdf.metal`, `Sdf.spv` and `Sdf.metallib` regenerated;
  - the tile compositor and its cache, the shared pattern placement, and `EmitVisitor`.

  These are experimental assemblies, declared with `[EQX0001]`.
- **eqc**: nothing. A vocabulary record reaches its runtime twin by name. `TileLength`'s conversion from a number is
  marked `[ConversionPassesThrough]`, as `SizeValue`'s is.
- **The email realizer, the design hosts, the SDKs, the templates and the samples**: nothing reads the pattern.
- **The developer surface** (`developer-surface.baseline.txt`): does not move.

Rules of `docs/PRINCIPLES.md` it follows:

- **2:** the capability is the CSS backgrounds model, and the desk is one test of it.
- **4:** written once, realized by both targets in the same change, which is also #1939's fix.
- **5:** `TileRepeat.Spaced` and `Fitted`, `BoxArea`, and `TileAttachment.Window`, never `space`, `round`,
  `background-origin` or `fixed`.
- **6:** Flutter's `DecorationImage` first, and the RenderObject tree named.
- **9:** a closed set with a visitor, and an exhaustive check in the twin, so a build fails where a realizer would
  have warned.
- **13:** no compatibility shape for `Pattern` or `GridPattern`.

It bends none. The data URL is runtime output, so it is written by one writer per producer and pinned byte for byte.
The Codegen writers are not used, because they are for generated files.
