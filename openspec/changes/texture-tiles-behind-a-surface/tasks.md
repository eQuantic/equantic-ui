## 0. Measure before building (#1937, #1939)

- [x] 0.1 In Chromium, WebKit and Gecko, at device pixel ratios of 1 and 2.625 (devtools emulation), over a
  1600 × 1000 box, measure and record:
  - whether a 330 px SVG tile's size and phase snap to whole device pixels, and where the tenth tile's left edge lands;
  - `round` on one axis with `auto` on the other: is the other axis scaled (Backgrounds 3 §3.9)?
  - `fixed` under a rotated ancestor: does it paint as `scroll` (Transforms 1 §6)?

  Write the answers into design.md's open question 1. Photon's snapping (task 5.6) and its transform rule (task 5.7)
  follow them.
- [x] 0.2 Measure the space toggles of design.md decision 8 in the three engines: an empty custom property, `initial`
  as guaranteed-invalid, and a `var()` chain choosing among four URLs while `data-theme` and the emulated
  `prefers-contrast` change. If one engine fails, decision 8 falls back to a class per mode, and tasks 3.6 and 3.7
  change with it.

## 1. The vocabulary: a closed set, and a list

- [x] 1.1 `Pattern`: an abstract record, `private protected` constructor, members `Area` and `ClipTo` (`BoxArea`) and
  `Attachment` (`TileAttachment`), and
  `abstract TResult Accept<TState, TResult>(IPatternVisitor<TState, TResult> visitor, TState state)`.
  `IPatternVisitor<in TState, out TResult>` has `Visit(GridPattern, …)`, `Visit(DrawingPattern, …)` and
  `Visit(ImagePattern, …)`.
- [x] 1.2 `GridPattern`: `sealed record GridPattern(float Cell, ColorToken Color, float LineWidth = 1) : Pattern`.
- [x] 1.3 `ArtworkPattern`: an abstract record deriving from `Pattern`, `private protected` constructor taking the
  width and the height (`TileLength`), with `Fit`, `HorizontalRepeat`, `VerticalRepeat`, `Anchor` and `Offset`, and
  `Size SizeIn(Size area, float aspect)`.
- [x] 1.4 `DrawingPattern(VectorDrawing artwork, TileLength width = default, TileLength height = default,
  ColorToken? tint = null)` with `Opacity`. `ImagePattern(string source, TileLength width = default,
  TileLength height = default)` with `DarkSource`.
- [x] 1.5 `TileLength`: a readonly record struct, auto by default, dp through a conversion from `float` marked
  `[ConversionPassesThrough]` with its reason, and `TileLength.Fraction(float)`.
- [x] 1.6 `TileRepeat` (`Repeat = 0, Once = 1, Spaced = 2, Fitted = 3`), `BoxArea` (`Border = 0, Padding = 1,
  Content = 2`) and `TileAttachment` (`Box = 0, Window = 1`), one file each, added to `EnumValueAbiTests`.
- [x] 1.7 `BoxStyle.Patterns : IReadOnlyList<Pattern>?` replaces `Pattern`, with its doc saying the order and where
  the slots sit.
- [x] 1.8 Every refusal of design.md decision 4, each naming its member.
- [x] 1.9 `./scripts/public-api.sh update`, and read the diff: the `*REMOVED*` lines are `BoxStyle.Pattern` and the
  record struct's members, and nothing else.
- [x] 1.10 `PatternVocabularyTests` (new), covering:
  - every refusal;
  - `SizeIn` for dp, a fraction, both lengths given, and each fit;
  - the value equality of each kind;
  - `TileLength`'s conversion.
- [x] 1.11 Check: the solution builds up to the realizers, which fail to compile until they implement
  `IPatternVisitor`. That failure is the instrument working.

## 2. Shared drawing pieces, moved before they are reused

- [x] 2.1 The web: `DrawingElements.Shapes(artwork)` takes the shape attributes out of `LowerDrawing` and its twin
  `lowerDrawing`. The component pins of every drawing stay byte-identical.
- [x] 2.2 Photon: `ArtworkPaint` takes `RunPaint` and `Frame` out of `EmitDrawing`. The `Drawing` goldens stay
  byte-identical.
- [x] 2.3 Check: the Web, Runtime and Native test projects are green with no pin regenerated.

## 3. The web: the C# lowering

- [x] 3.1 `SvgMarkup` and `TileArtwork` (decision 9). `DataUrl`, with the explicit table, and the CSS string escaping
  for a raster's source. `DataUrlEncodingTests` covers every code point from 0 to 127, `é`, `€`, and a source with a
  quote, a backslash and a line break.
- [x] 3.2 `PatternLayers : IPatternVisitor<…>` writes each kind's entries in the seven lists, replacing
  `TokenCss.GridPattern` and `GridPatternSize`, which are deleted:
  - the grid: its two layers, with its origin, clip and attachment;
  - the vector tile: its data URL, its size (`auto` for an auto length), its repeat, its position (with `calc()` for
    an offset), its origin, clip and attachment;
  - the image tile: its `url()` and the same lists.
- [x] 3.3 `BackgroundLayers` becomes the one value of decision 6: the list written reversed after the gradient and the
  glow, each list padded and written only when needed. `HtmlStyle` gains `BackgroundRepeat`, `BackgroundOrigin` and
  `BackgroundAttachment`. `PaintsNothing` reads `Patterns is null or []`. The hover path writes the same lists.
- [x] 3.4 Extend `GradientShimmerRealizerTests`, case by case:
  - one default grid writes what it wrote before;
  - two patterns under a gradient and a glow keep the order and pad every list;
  - `Once` × `Spaced` writes `no-repeat space`;
  - an anchor of (0.5, 1) with an offset of (12, −8) writes `calc(50% + 12px) calc(100% - 8px)`;
  - a fraction writes `25% auto`, and `Fit = Cover` writes `cover`;
  - `Area = Content` and `ClipTo = Padding` write `content-box` and `padding-box`;
  - a window attachment writes `fixed`;
  - an image tile writes `url("…")`.
- [x] 3.5 The toggles of decision 8:
  - the variants as custom properties on the element, only when they differ;
  - the layer's `var()` chain;
  - `ThemeStylesheet` writing the four toggles from the selector list it shares with the `eq-themed-*` rules,
    extracted once.
- [x] 3.6 Extend `ThemeStylesheetTests`: the toggles for each declared mode, under `data-theme`, and in the contrast
  query, under the same selectors as `eq-themed-dark`.
- [ ] 3.7 Check, against the real thing: a page of `samples/DefaultUIDashboard` with a desk that has a vector tile, an
  image tile with a dark source, a tinted tile and a window-attached tile inside a scroller. Open it in a browser:
  - in light and in dark;
  - with the theme toggled live;
  - under emulated more contrast;
  - scrolled.

  Write down a screenshot of each.

## 4. The web: the runtime's twin

- [x] 4.1 `vocabulary.ts`:
  - `Pattern`, `ArtworkPattern`, `GridPattern`, `DrawingPattern`, `ImagePattern` (with `patternKind`, and the C#
    refusals word for word) and `TileLength` (normalizing a raw number to dp);
  - the enum unions of the three enums, from `src/eQuantic.UI.Web.Build`'s generator, never written by hand;
  - `BoxStyle.patterns: Pattern[] | null`;
  - the exports in `runtime-exports.ts` and `index.ts`.
- [x] 4.2 `nodes.ts`: `PatternValue`, a union discriminated by `patternKind`.
- [x] 4.3 `lowering.ts`: `patternLayers` switches on `patternKind` and ends in an exhaustive `never` check. Port
  `svgMarkup`, `tileArtwork`, the encoding table, the source escaping and the toggles' chain, and share the shapes'
  builder with `lowerDrawing`.
- [x] 4.4 `gradient-shimmer.spec.ts`: the twin of every case of 3.4.
- [x] 4.5 The C#-against-TypeScript pin:
  - `DataUrlEncodingTests` writes a fixture of every code point's encoding, and a new vitest replays it through the
    twin's table;
  - `ComponentParityFixtureTests` and `component-parity.spec.ts` gain `box-patterns`: a grid, a three-shape vector
    tile (solid, inherited at 55 %, two-stop gradient) and an image tile, under a gradient and a glow, with every
    non-default member;
  - they also gain `box-pattern-themed`: a tint that differs by mode, and an image tile with a dark source.

  The fixture pins the atomic class names. Regenerate with `EQ_UPDATE_PARITY_FIXTURE=1`, and read the diff.
- [ ] 4.6 Regenerate `primitives-types.fixture.json` and `roslyn-completions.fixture.json`, and read both diffs.
- [ ] 4.7 Check:
  - `dotnet build src/eQuantic.UI.Runtime -t:TestRuntime` is green;
  - the page of 3.7, reached by navigation from another page, shows the same layers, with no patch in the hydration
    log;
  - the page opened in Safari in the iPhone and the iPad simulators, scrolled: record whether the window-attached
    tile moves with the box (WebKit's documented behaviour, which the web keeps, with no workaround). Write the
    measured answer into design.md decision 7 and into the wiki's note of task 6.1.

## 5. Photon: placement, one repeating draw, and the compositor

- [ ] 5.1 `PatternPlacement` (decision 5): the three areas with their radii, the placing area, the clip area and the
  window attachment.
- [ ] 5.2 The grid through `PatternPlacement`: rules from the placing area's origin across the clip area, inside
  `PushClip` of the clip area's rounded rectangle (#1939). `GradientShimmerTests:86-123` stays green unchanged.
- [ ] 5.3 The normative math, `Tiling` and `Tiling.TryTexel`. `TilingTests` covers:
  - negative cells, left of and above the origin;
  - `Once` on one axis;
  - the spaced gap;
  - a texel at each edge.
- [ ] 5.4 `DrawCommandKind.TiledTexture = 9` and `DisplayListBuilder.TiledTexture(in Rect area, TextureData texture,
  in Tiling tiling, float opacity)`. The Reference backend draws it through `Tiling.TryTexel`, premultiplied and
  linear.
- [ ] 5.5 `tiled_rgba_fragment` in `Sdf.slang`, with the slots of decision 12. `RhiPipelineKind.TiledRgba`,
  appended, and registered by the Metal and the Vulkan devices. Regenerate with `./scripts/generate-shaders.sh` and
  commit `Sdf.metal`, `Sdf.spv` and `Sdf.metallib`, never edited by hand.
- [ ] 5.6 The exact geometry of decision 11: a fractional period and phase, never snapped, sampled with linear
  filtering and repeat addressing. `TilingTests` holds the tenth tile of 330 dp at 2.625 at 7796.25 device px.
- [ ] 5.7 `EmitState.UnderTransform`, set when the emitter descends through a box with a `Transform` or a motion that
  transforms. Under it, a window attachment paints as a box attachment.
- [ ] 5.8 `TileCompositor`: a vector through the host's `IIconRasterizer` and `ArtworkPaint`; a raster through
  `motion.ImageLoader` and `motion.ImageCache`, resampled as decision 10 says. Both composite in premultiplied linear
  space and store straight sRGB.
- [ ] 5.9 `PatternTileCache`, threaded through `PhotonRealizer.Realize` and `MotionScope`, with the key and the
  budget of decision 13.
- [ ] 5.10 `EmitVisitor`: `EmitChrome` takes `Patterns`, and `PatternPainter : IPatternVisitor` draws each in order,
  between the background and the glow. With no rasterizer there is no vector tile, and with no loader no image tile.
- [ ] 5.11 `PatternPhotonTests` (new, `tests/eQuantic.UI.Native.Engine.Tests`, with `PortableIconRasterizer` and a test
  image loader), for emission:
  - one `TiledTexture` command per tile layer for a 2560 × 1440 box;
  - the list's order against the glow and the gradient;
  - the cache hit on a second frame, and a new composite on a new `RenderScale`;
  - the padding and content areas;
  - the window attachment's origin before and after a scroll of 300 dp;
  - the transform rule;
  - an image tile's texture at 240 × 160 for the spec's case.
- [ ] 5.12 Pixel tests on the Reference backend, in the same file:
  - no seam at 2.625;
  - one tile against the same artwork as a `Drawing`, within one level per channel;
  - the corner outside the radius is the page, for a tile and for a grid (#1939);
  - half opacity is 0.5 in linear light;
  - a window-attached tile's pixel at a fixed window point is unchanged by a scroll.
- [ ] 5.13 Check, against the real thing: `samples/PhotonDesktop` with the desk of 3.7:
  - on macOS (Metal), at the display's scale, then after moving the window to a display of another scale;
  - on the Windows runner (Vulkan);
  - in the iOS simulator through `samples/WalletMobile`'s shell, scrolled, where the window-attached tile stays still.

  Ask for the macOS jobs on the branch (`gh workflow run ci.yml --ref <branch>`), since this changes the Apple track.

## 6. Documentation, ledger and suites

- [ ] 6.1 The wiki's `Styling` page, in all five languages (English, pt-BR, es, fr, de), "Tile artwork behind a
  surface":
  - the desk as the example;
  - a table of the members;
  - an image tile, its density advice, and that it has no opacity because the image carries its own transparency;
  - the window attachment, and that iOS and iPadOS Safari keep WebKit's behaviour (the tile moves with the box),
    as task 4.7 measured;
  - the fences.

  `Upgrading`, in every language it is written in: the break and its migration line. Write both on the wiki branch
  `feat/a-texture-tiles-behind-a-surface` (worktree `wiki-texture`).
- [ ] 6.2 `docs/FLUTTER-PARITY.md`: a row for `DecorationImage` (`repeat`, `alignment`, `fit`, `opacity`), with the
  fenced `centerSlice` and `scale` and their reasons.
- [ ] 6.3 A ledger entry under `docs/ledger/`, citing #1937 and #1939.
- [ ] 6.4 Archive the change with `openspec archive texture-tiles-behind-a-surface --yes` before the merge.
- [ ] 6.5 Every test project of the solution, with the Conformance suite in full, each read by its exit code, through
  the build lock with `EQ_WIKI_DIR` pointed at the wiki worktree.
