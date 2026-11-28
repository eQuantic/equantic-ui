# box-paint Specification

## Purpose
What a box paints from its style, the same on the web and on Photon: the layers between its edge and its child
that are neither its layout nor its shadow's depth.

## Requirements

### Requirement: A box's inset highlight is its padding box's top row on both targets

A box that sets `BoxStyle.InsetHighlight` SHALL paint one pixel row of that colour along the top of its padding box. The padding box is the box past its border on each side that has one. The row SHALL stay inside the padding box's rounded corners, and SHALL paint over the box's background layers and under its border. The web SHALL lower it to an `inset 0 1px 0` box-shadow entry, and Photon SHALL fill that row clipped to the padding box's rounded rectangle.

#### Scenario: A rounded box with no border

- **WHEN** a box with a fill, a corner radius of 8 and an inset highlight is drawn on Photon
- **THEN** its top row is the highlight, the row below it is the fill, and the pixel at its corner, outside the rounded corner, is the page behind it

#### Scenario: A box with a border

- **WHEN** a box with a 2 dp border and an inset highlight is drawn on Photon
- **THEN** its top two rows are the border, the next row is the highlight, and the row below that is the fill

#### Scenario: The web

- **WHEN** either box is lowered on the web
- **THEN** its box-shadow carries `inset 0 1px 0` and the highlight's colour

### Requirement: A fill's edges land on whole device pixels on Photon

Photon SHALL paint an axis-aligned fill with each edge on the nearest whole device pixel under its transform, half a pixel rounding up, as a browser snaps a box's background. An axis-aligned clip SHALL land on whole device pixels the same way, as a browser snaps an overflow clip. Two fills that share an edge on a fractional device pixel SHALL NOT let the one behind show through the one in front. A fill under a transform that turns or skews it, and a disc, SHALL be painted where they are. A fill's corners SHALL keep their antialiasing, and a fill thinner than a pixel SHALL keep one pixel.

#### Scenario: A swipeable row's action behind its surface, at 2.625 device pixels a dp

- **WHEN** a red action and a white surface, both 78 dp tall from the same top, are painted at 2.625 device pixels a dp
- **THEN** the pixel row at 204.75 px, which both edges cut, holds none of the action's red

#### Scenario: A closed swipeable row at 2.625 device pixels a dp

- **WHEN** a closed `SwipeableRow` 78 dp tall, whose box clips its action and its surface, is painted at 2.625 device pixels a dp
- **THEN** the pixel row its bottom edge cuts, over the action, holds none of the action's red

#### Scenario: A centred line at a half pixel

- **WHEN** a line's exact centre puts its left edge at 130.5 at one device pixel a dp
- **THEN** its fill's edge is painted at 131, and its layout keeps 130.5

### Requirement: A box paints a list of patterns from a closed set that every realizer draws

`BoxStyle.Patterns` SHALL take a list of `Pattern`, whose kinds are `GridPattern`, `DrawingPattern` and `ImagePattern` and
no other. A type outside the vocabulary's assembly SHALL NOT be able to derive from `Pattern` or `ArtworkPattern`.

Every realizer SHALL draw a pattern through `IPatternVisitor<TState, TResult>`, which has one method per kind, so adding
a kind is a build error in every realizer that does not draw it. The runtime's twin SHALL dispatch on the kind with an
exhaustive check, so adding a kind fails its type check too.

The list SHALL paint in order, the last pattern on top. From the bottom, a box SHALL paint its background colour, then
its patterns, then its glow, then its gradient, on both targets.

#### Scenario: A grid draws as it did

- **WHEN** a box with no border and square corners sets `Patterns = [new GridPattern(56, Theme.Border)]`
- **THEN** the web writes the same two hairline layers and `56px 56px, 56px 56px` as before, and Photon emits the same
  hairline fills as before

#### Scenario: A kind a realizer does not draw

- **WHEN** a fourth kind is added to `Pattern` and `IPatternVisitor` gains its method
- **THEN** the web realizer, Photon's emitter and the runtime's twin each fail to build until they draw it

#### Scenario: Two patterns under a glow and a gradient

- **WHEN** a box sets a background, `Patterns = [grid, tile]`, a glow and a gradient
- **THEN** the web's `background-image` lists the gradient, the glow, the tile, then the grid's two layers, over the
  background colour, and Photon draws the background, the grid, the tile, the glow, then the gradient

### Requirement: Every pattern is placed in one area of the box and clipped to another

Every pattern SHALL be placed in its `Area` and clipped to its `ClipTo`, each one of the border, padding and content
areas, by default the padding area and the border area. The padding area SHALL be the border area less the border on
the sides that draw one, and the content area SHALL be the padding area less the box's padding. Each area's corner
radii SHALL be the box's radii less what was removed, never below zero. A pattern SHALL be clipped to its clip area's
rounded rectangle. On both targets, a grid SHALL start its rules at its placing area's origin, and they SHALL continue
across the clip area on every side.

#### Scenario: A grid in a bordered, rounded box (#1939)

- **WHEN** a 200 × 120 box with a 4 dp border and a corner radius of 16 sets a 50 dp grid
- **THEN** on both targets the vertical rules are at x = 4, 54, 104 and 154 and the horizontal ones at y = 4, 54 and
  104, and on Photon the pixel at the corner outside the radius is the page behind it

#### Scenario: A tile placed in the content area and clipped to the padding area

- **WHEN** a box with a 2 dp border and 10 dp of padding tiles with `Area = BoxArea.Content` and
  `ClipTo = BoxArea.Padding`
- **THEN** the first tile starts at (12, 12), no tile pixel lands on the border, and the web writes `content-box` and
  `padding-box` in the origin and clip lists

### Requirement: An artwork tile repeats from an anchor at a size, per axis

A `DrawingPattern` or an `ImagePattern` SHALL repeat its artwork over its placing area at a size:

- each of its width and height in dp, or as a fraction of the placing area, with a missing one from the artwork's
  aspect;
- or, when `Fit` is set, the artwork fitted to the placing area (`Contain`, `Cover` or `Stretch`).

Its `Anchor`, a pair of fractions, SHALL place the tile's point at that fraction at the same fraction of the placing
area, moved by `Offset` in dp. The tiles SHALL continue from there on every side, the left and the top included.

Each axis SHALL repeat as its `TileRepeat` says:

- `Repeat`: tiles across the whole clip area;
- `Once`: one tile, at the origin;
- `Spaced`: as many whole tiles as fit the placing area, the first and the last at its edges, the room left over
  shared between them, and one tile at the origin when fewer than two fit;
- `Fitted`: the tile scaled so a whole number of tiles, at least one, fills the placing area, with an auto length on
  the other axis scaled with it.

The construction SHALL refuse each of these with an `ArgumentOutOfRangeException` naming the member:

- a negative dp length, or a fraction outside (0, 1];
- a `Fit` beside a length;
- a tile with neither a length nor a fit;
- an anchor outside 0..1;
- an opacity outside 0..1;
- an empty source.

#### Scenario: The desk's tile

- **WHEN** a 1600 × 1000 dp box with no border tiles a 675 × 518 artwork with `new DrawingPattern(art, 330)`
- **THEN** the tile is 330 × 253.2444 dp, the first one's top-left corner is the box's, and five tiles show across and
  four down, the last cut by the box's edge, on both targets, and the web writes `330px auto`

#### Scenario: An anchor at the centre with an offset

- **WHEN** the same box sets `Anchor = new Point(0.5f, 0.5f)` and `Offset = new Point(12, -8)`
- **THEN** a tile's centre is at (812, 492), tiles continue left of it and above it, and the web writes
  `background-position: calc(50% + 12px) calc(50% - 8px)`

#### Scenario: A fraction of the area

- **WHEN** the same box tiles with `width: TileLength.Fraction(0.25f)`
- **THEN** the tile is 400 dp wide and 306.963 dp tall, and the web writes `25% auto`

#### Scenario: Spaced and Fitted

- **WHEN** a 1000 dp wide box repeats a 300 dp tile `Spaced` across, and another `Fitted` across
- **THEN** the first shows three whole tiles at x = 0, 350 and 700, and the second three tiles 333.3333 dp wide, with
  the height scaled by the same factor, on both targets

#### Scenario: Refusals

- **WHEN** a tile is built with `width: 330` and `Fit = ImageFit.Cover`, or with `Anchor = new Point(1.5f, 0)`
- **THEN** the construction throws `ArgumentOutOfRangeException` naming `Fit`, or naming `Anchor`

### Requirement: A pattern attached to the window stays still while the content scrolls

A pattern with `Attachment = TileAttachment.Window` SHALL be placed against the window instead of its placing area, and
still clipped to its box's clip area. The web SHALL write `background-attachment: fixed` for its layers. Photon SHALL
place it against the window on every shell. On both targets, a window-attached pattern in a box that has a transform,
or that is under one, SHALL paint as attached to its box, as CSS Transforms says. A scroll is not a transform for this
rule.

#### Scenario: A desk inside a scroller

- **WHEN** a tiled box with a window attachment inside a `ScrollView` is scrolled by 300 dp on Photon
- **THEN** the tile's pixels at a given window position are the same before and after the scroll, and the box's
  content moved

#### Scenario: The web

- **WHEN** the same box is lowered on the web
- **THEN** its attachment list holds `fixed` for the tile's layer and `scroll` for every other layer

#### Scenario: Under a transform

- **WHEN** the scrolled box sits inside a box with `Transform = Transform2D.Rotate(2)`
- **THEN** on Photon its tile moves with the box, as a box attachment does

### Requirement: A vector tile is painted at its opacity, its tint answering currentColor

A `DrawingPattern` SHALL paint at its `Opacity` as one composited picture, so overlapping shapes inside it do not
double-blend. The shapes the artwork leaves as `currentColor` SHALL take its tint, or the theme's `TextPrimary` when it
has none.

#### Scenario: Half opacity

- **WHEN** an opaque white tile at `Opacity = 0.5f` is drawn over a black background
- **THEN** on Photon each pixel is 0.5 in linear light, and the web writes the tile's shapes inside one group at
  `opacity='0.5'`

### Requirement: A raster tile repeats the image source an Image node takes

An `ImagePattern` SHALL take the same source string `Image` takes, and `DarkSource` for the dark mode. It SHALL be sized
in dp, by a fraction, or by a fit, never by its pixels. The web SHALL write it as `url("…")` with the source escaped
as a CSS string. Photon SHALL decode it through the host's image loader, sharing the cache with `Image` nodes, and
resample it once to the tile's device size. A host with no image loader SHALL draw no image tile. An `ImagePattern`
SHALL have no opacity of its own.

#### Scenario: A raster tile on both targets

- **WHEN** a box tiles `new ImagePattern("img/linen.png", 120)` over a 60 × 40 px image at 2 device pixels a dp
- **THEN** the web writes `url("img/linen.png")` and `120px auto`, and Photon draws one `TiledTexture` command whose
  texture is 240 × 160 px

#### Scenario: A dark source

- **WHEN** the tile sets `DarkSource = "img/linen-dark.png"` and the app is in dark mode
- **THEN** Photon composites the dark image, and the web's computed `background-image` is the dark URL

### Requirement: The web writes a tile's layer identically from SSR and from the twin

The web SHALL lower a vector tile to `url("data:image/svg+xml,…")`, holding the artwork as a standalone SVG at the
tile's size. The data URL SHALL encode exactly `%`, `#`, `"`, `<`, `>`, every control character, and every non-ASCII
character as its UTF-8 bytes, with uppercase hexadecimal digits. Each `background-*` list SHALL hold one entry per
layer, be written only when one of its entries is not the default, and be padded with the other layers' defaults. The
server's lowering and the runtime's twin SHALL write the same bytes, so the same style hashes to the same atomic class.

#### Scenario: The same class on both producers

- **WHEN** the parity fixture's tiled box, with a grid, a vector tile and an image tile, is lowered by the C# realizer
  and replayed through the twin
- **THEN** both write the same seven background lists and the same atomic class names

#### Scenario: The encoding table

- **WHEN** every code point from 0 to 127, `é` and `€` are encoded by both producers
- **THEN** both write the same text, `é` is `%C3%A9`, `#` is `%23`, and a space, `'`, `(` and `)` are left as they are

### Requirement: A layer that varies by mode or contrast follows the palette's decision on the web

When a layer's image is the same in every mode and contrast, the web SHALL write it directly. When it varies, the
element SHALL carry one custom property per variant, valid only while the theme's toggles on `:root` select it. The
layer SHALL read them through nested `var()` fallbacks. The theme's stylesheet SHALL set the toggles `--eq-on-light`,
`--eq-on-dark`, `--eq-on-std` and `--eq-on-more` under the same selectors that choose `Image.DarkSource`'s artwork:
the declared mode, else the system's, a live `data-theme` over both, and `prefers-contrast: more`.

#### Scenario: A declared dark mode and a live toggle

- **WHEN** an app declares the dark mode and a page tiles with a tint whose dark colour differs from its light one
- **THEN** the box's computed `background-image` is the dark URL, and setting `data-theme="light"` makes it the light
  one without a render

### Requirement: Photon draws each tile layer as one repeating draw, crisp at the device's scale

Photon SHALL composite each tile once into an RGBA texture whose size is the tile's size in device pixels, rounded up:

- from a vector, its shapes' solid and two-stop gradient paints resolved as a `Drawing` resolves them;
- from a raster, the decoded image resampled.

The texture SHALL be cached by what was composited, its device size and the resolved tint.

Photon SHALL draw each tile layer with one `TiledTexture` command over its clip area, whatever the number of tiles. The
command SHALL sample with linear filtering and repeat addressing, so every device pixel samples exactly one position of
one period. The period and the phase SHALL be the exact ones, never rounded to a device pixel, as Chromium draws a
background tile. A change of the device's
scale SHALL composite the tile again. A host with no rasterizer SHALL draw no vector tile.

#### Scenario: A full-window desk

- **WHEN** a 2560 × 1440 dp box is tiled with a 330 dp tile
- **THEN** the display list holds one `TiledTexture` command for the tile, inside one clip

#### Scenario: No seam at a fractional scale

- **WHEN** an opaque single-colour square tile repeats over a box at 2.625 device pixels a dp on the Reference backend
- **THEN** every pixel inside the box is the tile's colour

#### Scenario: No drift from the exact geometry

- **WHEN** a 330 dp tile repeats from the box's origin at 2.625 device pixels a dp
- **THEN** the tenth tile starts at 7796.25 device pixels, not at 7794

#### Scenario: The cache

- **WHEN** the same tiled box is drawn in two frames, then once more after the render scale changes from 2 to 3
- **THEN** the artwork is composited once for the first two frames and once again for the third
