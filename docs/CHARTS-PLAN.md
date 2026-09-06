# Charts — one vocabulary, drawn by both realizers

## Why a plan, and why now

The SDK ships three chart assemblies today, and none of them is a chart the framework draws:
`eQuantic.UI.Charts` is a web-only base (`IChart : IComponent`, a `ChartData<T>` of labels and
datasets), and `eQuantic.UI.Charts.ChartJs` / `eQuantic.UI.Charts.ApexCharts` hand that data to a
JavaScript library through an `HtmlElement` and a script tag. They work on the web and nowhere
else, and they fail the product principle in the one place a consumer meets them: the developer
writes `Type = "line"`, `Curve = "smooth"`, `Position = "top"` — a target's incantations, in
strings, looked up in someone else's documentation.

The decision (2026-09-04) is to write our own: **one assembly, one vocabulary, realized on the web
and on Photon from the same component code**, with no library name in it. And the bar is stated:
*nothing less than what those two offered* — this is not a sparkline kit, it is the chart layer a
dashboard product is built on.

This plan exists because the survey found the question is not "which chart first" but three
decisions the components would otherwise take by accident: what a chart is made OF (nodes or a
canvas), where its colours COME FROM (a theme surface that does not exist yet), and what the
parity net IS (a fixture, like Mermaid's, or a promise). Each is decided below.

## What the two offered — the parity bar

The union of what Chart.js and ApexCharts expose, against the slice that delivers it. "Offered" is
what a consumer could reach through the wrappers' options plus what the wiki advertised
("Bar, Line, Pie, Doughnut, Radar… Area, Bar, Line, Heatmap, Candlestick").

| capability | Chart.js | Apex | slice |
|---|---|---|---|
| Bar / column — grouped, stacked, horizontal | ✓ | ✓ | 1 |
| Line — straight, step, smooth; markers | ✓ | ✓ | 2 |
| Area — single and stacked | ✓ | ✓ | 2 |
| Pie / donut | ✓ | ✓ | 3 |
| Scatter / bubble | ✓ | ✓ | 3 |
| Radar / polar | ✓ | ✓ | 4 |
| Heatmap | | ✓ | 3 |
| Candlestick / range bar (finance) | | ✓ | 4 |
| Radial bar / gauge | | ✓ | 3 |
| Sparkline, stat tile, meter (figures) | | ✓ (sparkline) | 2 |
| Axes: category, value, time; titles; tick format | ✓ | ✓ | 1–2 |
| Legend, position, toggle-to-isolate | ✓ | ✓ | 1 |
| Tooltip on hover/focus; crosshair on lines | ✓ | ✓ | 1–2 |
| Data labels (selective) | ✓ | ✓ | 1 |
| Animations (enter) | ✓ | ✓ | 4 |
| Responsive to the box | ✓ | ✓ | 1 |
| Zoom / pan on a time axis | | ✓ | 4 |
| Export (image) | | ✓ | 4 — through a capability, never a toolbar button |
| Mixed (bar + line on one axis) | ✓ | ✓ | 2 |
| Annotations (threshold line, band) | | ✓ | 4 |
| Treemap, box plot, funnel, 3D, maps | | ✓ (some) | **fenced** — see below |

The wrappers stay published until slice 5 measures this table complete. Then they go, in one
release, named in the migration notes.

## The shape, decided

### 1. Chrome is nodes; marks are a canvas

A chart has two kinds of content and they want different machinery.

**Chrome** — title, legend, axis tick labels, axis titles, the tooltip, the table view, the empty
state — is TEXT AND BOXES, and the vocabulary already does that better than any drawing call
would: `Text` measured by the realizer that will show it, `TypeRole.Caption`/`LabelSmall` for
ticks, theme tokens for ink, `Row`/`Column`/`Stack` to place it, `Pressable` for a legend entry.
It is themable, accessible, selectable and localizable for free, on both targets.

**Marks** — bars, line segments, points, sectors, cells, candles — are ARITHMETIC over a box, and
the vocabulary already has the node for that too: `Canvas`, whose painter is deliberately the
engine's own shapes and nothing else (`ICanvasPainter`: filled/stroked rounded rectangles, circles,
annular sectors, lines as rotated rectangles). Every mark below maps onto those five, which is what
makes a chart exactly as fast, as antialiased and as correct as everything else the framework draws,
on every target. Pointer events arrive in the canvas's own coordinates, which is the hit-testing a
chart needs (nearest X for a crosshair, nearest mark for a tooltip) — the app's arithmetic, not the
engine's.

| mark | painter call(s) |
|---|---|
| bar / column / candle body / heat cell | `FillRect` — square at the baseline, the data end rounded by a second, overlapping rounded rect |
| stacked segments, adjacent bars | `FillRect` shortened by the 2dp surface gap (the surface shows through; never a stroke) |
| line | `Line` per segment, 2dp; a `FillCircle` of radius 1dp at each joint fills the notch square caps leave |
| smooth line | a monotone cubic sampled into short `Line` segments (four per interval) |
| marker / end dot | `FillCircle` surface ring (r + 2) under `FillCircle` colour (r ≥ 4) |
| area wash | `FillRect` columns, 1dp wide, baseline to the interpolated line, series colour at 10% opacity |
| pie / donut / gauge / radial bar | `FillAnnularSector` — the inner radius IS the donut; the gap is an angular inset |
| scatter / bubble | `FillCircle`, ring under, radius from the size channel |
| gridline / axis / crosshair | `Line`, 1dp, `theme.Border`; the baseline `theme.BorderStrong` |
| candle wick | `Line`, 1dp |

**The one fence this inherits.** The painter has no arbitrary paths — Photon is an SDF rasterizer
and a path engine is the engine plan's v2. So a smooth line is sampled and an area is tiled, both
exactly and deterministically on both targets, and a shape that needs a real path (a filled polygon
of a radar chart's web) takes the `Vector` node — an SVG path on the web, one rasterized texture per
shape on Photon — as Mermaid already does for its edges. When the path engine lands, the tiled and
sampled spellings are replaced behind the same API; nothing a consumer wrote changes.

### 2. Colour comes from the theme, and it does four jobs

Charts do not pick colours; they ask the theme, and the theme answers with a `DataPalette`
(`IAppTheme.Data`, defaulted so no existing theme breaks). Every colour in it does exactly one job:

| job | encodes | structure | in the palette |
|---|---|---|---|
| categorical | identity — which series | 8 hues, **fixed order**, assigned in sequence, never cycled | `Series[0..7]` |
| sequential | magnitude | one hue, light→dark steps; the anchor flips in dark mode | `Sequential[]` |
| diverging | polarity | two hues that read as opposite + a neutral midpoint | `Diverging` |
| status | state | four reserved steps, always with an icon + label | `Status` |
| de-emphasis | "the rest", "Other" | one gray | `Other` |

The default instance is the validated reference palette of the data-visualization method the SDK
adopts (eight hues that clear every colour-vision gate in both modes on the adjacent pairlist; its
first three also all-pairs). A brand theme overrides `Data` — and holds the result to the same
audit, which is the point: **the audit is code** (`PaletteAudit`, in Primitives): OKLab lightness
band and chroma floor, protan/deutan separation simulated with Machado 2009, a normal-vision
floor, contrast against the chart surface; and the ordinal checks for a ramp. The reference numbers
the method publishes are pinned as a test, so the port cannot drift from the method and a palette
cannot drift from the port.

The rules that follow are enforced BY CONSTRUCTION, not documented and hoped for:

- **Colour follows the entity.** A series takes its slot at construction; filtering a series out
  does not repaint the survivors.
- **Eight is the ceiling.** `SeriesColor(8)` throws, by name: fold the tail into `Other`, facet into
  small multiples, or encode a second channel (shape). Never a generated ninth hue.
- **One value axis.** The API has no second one. Two measures of different scale are two charts,
  or one indexed to a common base.
- **Text never wears the data colour.** Labels, values, ticks and legend text use the theme's text
  tiers; identity rides the coloured mark beside them (a swatch, a line key). The one exception is a
  label INSIDE a filled segment, which picks white or ink by the fill's luminance.
- **Status colours are reserved.** A series that MEANS good/bad wears status; "series 4" never does.
- **Nominal bars share one colour.** Colouring each bar by its value double-encodes what length
  already shows; an ordered category (funnel stage, tier) takes the sequential ramp instead.

### 3. Marks and spacers — fixed, not styled

Bars ≤ 24dp thick, the slot's leftover left as air; 4dp rounded data end, square baseline. Lines
2dp. Markers ≥ 8dp. Area wash at ~10%. Gridlines and axes hairline (1dp), solid, one step off the
surface. A **2dp surface gap** separates touching fills — stacked segments and adjacent bars alike —
and a **2dp surface ring** sits under any dot that may cross a line. Nothing is ever separated by a
stroke drawn around it. These are constants of the chart layer, not properties a consumer sets;
what a consumer sets is data, orientation and the few choices that change meaning (stacked or
grouped, straight or smooth, the axis kinds).

### 4. Interaction is part of the deliverable

- **Lines and areas:** a crosshair finds the X — a 1dp hairline snaps to the nearest data position,
  and ONE tooltip lists every series at that X (values first, in the strong tier; names second,
  each keyed by a short line of its colour). Nobody has to hit a 2dp line.
- **Bars, segments, cells, dots:** the mark is the hit target, and the hit area includes the gap
  and then some — never only the painted pixels; a dot's area is at least 24dp, dense scatter uses
  nearest-point.
- **The tooltip is a node**, positioned over the canvas in the same `Stack`; keyboard focus on the
  chart moves the active index with the arrows and shows the same tooltip; `Esc` clears it.
- **Legend:** a `Row` of `Pressable` entries — swatch shaped like the mark (rectangle for bars and
  areas, line for lines) plus `Text`; a press isolates the series, a second press restores.
- **Refetch keeps the frame:** while data reloads the previous render holds at reduced opacity —
  no skeleton, no jump.
- **Photon parity:** the same pointer events exist (`CanvasPointer`), hover on desktop, tap and
  drag on touch; the tooltip and legend are the same nodes.

Tooltips enhance and never gate: every value is also reachable through a selective direct label
or the **table view**, the WCAG twin of every chart — a `DataTable` of the same series, toggled from
the chart frame. The chart's accessible name is its title; its role is a figure with the table as
its description. A single series draws no legend box (the title names it); two or more always do.

### 5. Parity is the design constraint — the net

- **Layout fixture, cross-pinned.** Every chart's geometry (plot rectangle, tick positions, bar
  rectangles, sector angles, point centres) comes out of ONE solver that runs as C# on the server
  and on Photon and as the transpiled twin in the browser, dumped to a fixture the C# test and the
  vitest spec both assert — the Mermaid shape (`mermaid-layout.txt`, one dumper mirrored line for
  line, regenerated by an env var). Integers and exact halves wherever the layout decides; the float
  math that remains stays in `float` so the twin's `Math.fround` agrees.
- **Palette audit as a test.** The reference numbers pinned; a brand palette that fails is a red
  build, not a review comment.
- **Component pins** for the SSR of each chart with canonical data, light and dark.
- **The samples are the proof.** A Charts screen in the dashboard sample and the same screen in
  the Photon desktop sample, walked by the Studio tests; both desktop samples build cold.
- **The wrappers are the oracle for parity** (slice 5): the same data rendered through the old
  `ChartJs<T>` and the new `BarChart`, side by side in the sample, until the table above is
  measured complete.

## Vocabulary — no target's word

The names are the ones a Flutter developer already knows (`fl_chart`, `charts_flutter`): `BarChart`,
`LineChart`, `PieChart`, `ScatterChart`, `RadarChart`; plus `Heatmap`, `CandlestickChart`,
`Sparkline`, `StatTile`, `Meter`, `Gauge`. Shared: `ChartSeries`, `DataPoint`, `CategoryAxis`,
`ValueAxis`, `TimeAxis`, `Legend`, `ChartFrame`. Options are typed — `LineCurve.Smooth`,
`BarLayout.Stacked`, `Orientation.Horizontal`, `LegendPlacement.Below` — never a string a library
would parse. A tick format is a .NET format string applied through the request's culture, inside
the transpilable subset the compiler already fences (EQ2108–EQ2110): `"N0"`, `"C"`, `"P0"`,
`"MMM d"`.

Factories follow the declarative surface: `BarChart(series: [...], axis: CategoryAxis(labels))`,
mirrored parameter for parameter, no `new`.

## Where it lives

- **`eQuantic.UI.Charts`** — the write-once assembly, `Primitives`-only, realized by Web and Photon
  through the vocabulary (it declares no realizer of its own). It takes the name the consumers
  already reach for; the three web-only types it holds today move into the two wrappers as private
  copies for the transition (slice 1), and the wrappers are deleted in slice 5.
- **`DataPalette` and `PaletteAudit`** — in `eQuantic.UI.Primitives.Theme`, beside `ColorToken`:
  the palette is a theme concern, and the audit is what makes it safe to change.
- **Implicit in the SDK**, like `Components`: a chart is a component. Its cost is paid only where a
  page uses one (per-page bundles on the web, trimming on Photon) — the same argument that put
  Markdown and Mermaid in every app.

## Slices

| slice | delivers | exit criterion |
|---|---|---|
| **0** | `docs/CHARTS-PLAN.md`; `DataPalette` (+ `IAppTheme.Data`, defaulted); `PaletteAudit`; the palette's web twin (`data` on the client theme — the generated design system and the SSR bridge carry it; the audit owes none); the reference numbers pinned as tests | audit reproduces the method's published numbers to 0.1; the failing case fails |
| **1** | the assembly; `ChartFrame` (title, legend, axes as nodes, plot `Canvas`, table view, empty state); `BarChart` grouped/stacked/horizontal; `CategoryAxis`/`ValueAxis`; tooltip and legend interaction; the web bridge carries `Data` to the twin; layout fixture; sample screen; wiki EN + pt-BR | bar chart identical on web SSR, web client and Photon for the fixture data; Studio walk green |
| **2** | `LineChart` (straight/step/smooth, markers, area wash, stacked area), `Sparkline`, `StatTile`, `Meter`; `TimeAxis`; crosshair | the finance page of the site can drop its hand-drawn chart |
| **3** | `PieChart`/donut, `Gauge`/radial bar, `Heatmap`, `ScatterChart`/bubble; sequential and diverging jobs in use | |
| **4** | `RadarChart` (web via `Vector`), `CandlestickChart`, annotations, enter animations, zoom/pan on time, export through a capability | |
| **5** | parity measured against the table; wrappers deleted; `Charts` wiki page rewritten; migration notes | the table has no empty cell |

## Where the slices stand

- **Slice 0** landed 2026-09-05: the plan, `DataPalette`, `PaletteAudit`, the palette's web twin.
- **Slice 1** landed 2026-09-06: `eQuantic.UI.Charts` transpiled by eqc as a runtime-provided
  library, `BarChart` in its three arrangements with legend, tooltip and table view,
  `ChartSeries`/`CategoryAxis`/`ValueAxis`/`ValueScale`, the cross-pinned layout fixture, the
  dashboard's `/charts` page and the Studio's Charts section, the wiki page. Two deviations from
  the row above, both deliberate: `ChartFrame` is not a type yet — the bar chart carries its chrome
  inline, and the frame is extracted when the second chart (slice 2) shows what it must abstract
  rather than guessed from one; and the empty state is the chrome standing without marks (title, a
  clean 0..1 axis), not a message. Two engine facts the slice surfaced and fixed: a `Fill` child
  under an unbounded axis measured zero inside a `Stack` of fixed height (the first chart drew
  nothing on Photon), and identity in a virtualized list was positional on BOTH realizers — the
  Photon path shifted with the window, and the web funnel never handed `VisualNode.Key` to the
  reconciler. A keyed child now takes its key as its path segment, and the web writes the key.
- **The mute canvas**, the third and by far the most expensive, corrected in the same slice and
  under-described in its commit. On the web, a layout node that PAINTS NOTHING is not a hit target
  (`LowerFlex` follows Flutter's `hitTestSelf`, not the DOM's every-element-is-a-target) and
  `pointer-events` inherits, so an interactive `Canvas` inside almost any unpainted `Row` or
  `Column` inherited the disclaimer and never received a pointer. Not "inside a `Stack`", which is
  how the commit put it — no `Stack` is required. It had shipped that way for four versions, and a
  consumer lost the hover, the drill-in and the marking of its main component to it without a
  single red test: the arithmetic behind the canvas was tested and correct, and a headless render
  gate draws a frame without passing a pointer through it. A mute canvas draws exactly like a live
  one. The net is `CanvasUnderALayerTests` on both realizers (Photon never had the defect and now
  says so), and it is the strongest case yet for measuring geometry and hit-testing in a browser.

## Fenced, on purpose

- **Dual axes** — never; the API cannot express one.
- **3D, treemap, box plot, funnel, polar area, maps** — out of v1. Treemap and box plot are
  legitimate later slices; 3D and funnel mislead by construction; maps are a track of their own.
- **Arbitrary paths in the painter** — the engine plan's v2 fence; this plan spells its shapes
  without them and swaps the spelling when it lifts.
- **A toolbar** — export, zoom and pan are behaviours the app wires to its own controls or a
  capability, not buttons a chart grows.

## Open, for Edgar

- Whether `eQuantic.UI.Charts` is implicit in the SDK (recommended above) or an opt-in like an
  icon pack. **Slice 1 shipped it implicit**, on the recommendation — one line in each SDK's
  `Sdk.props` reverses it, and no consumer code changes either way.
- Whether the wrappers go at slice 5 or the moment slices 1–3 land (the site and the finance
  product use neither today). **Still open**; slice 1 only gave them private copies of the three
  types they were using, so nothing forces the date.
