## Context

#1568 gave the vocabulary `TextDirection`, `Directionality` and `ComponentContext.TextDirection` (the nearest
`Directionality`, else `CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft`, on Photon too). The web writes `dir`
on `<html>` from the culture and on a `Directionality`'s child element; the token sheet sets `--eq-inline` to 1 on
`:root` and `[dir=ltr]` and to -1 on `[dir=rtl]` (`ThemeStylesheet.cs:132-137`); start and end are logical CSS.
`TableViewport`, `SheetSurface` and `CodeSurface` hosts carry `dir="ltr"`, and `CodeEditor`, `CodeBlock`, `Mermaid`
and the entry lines build inside `Directionality(LeftToRight)`. Photon reads the direction and lays out left to
right, and stays so for 1.0.

What the SDK draws or hears by itself did not follow: glyphs, the horizontal drag, the sortable's cross travel and
placement, the horizontal arrows, and the horizontal scroll offset. #1568's design handed them here (its decision 7
and its risk on `scrollLeft`), and #1594's design handed the sortable's cross travel and arrows.

The general capability: **a node whose meaning is logical (toward the end, previous/next, a glyph that points along
the reading) is bound to physical input and output by the realizer, per the direction where its element sits.**

The catalog components this reaches, by what they name:

| names | components | node |
|---|---|---|
| a glyph that points along the reading | `Calendar` (previous/next month), `Breadcrumb` (separator), `TreeView` (closed expander), `ListDetail` (back) | `Icon` with `Icons.ChevronLeft`/`ChevronRight` |
| a drag toward the end | `Switch`, `SwipeableRow`, `Slider`, `DataTable` (column resize) | horizontal `Draggable` |
| a carry along or across a list | `DataTable` columns, trees and chip lists built on it | `Sortable` |
| previous/next by arrow | `Tabs`, `SegmentedControl`, `RadioGroup`, `Slider`, `DataTable` (separator) | `Adjustable` |
| previous/next by arrow in a grid | `Calendar`, `TreeView` | `Navigable` |

`CodeBlock`'s fold chevron, `CodeEditor`, `SheetKeymap`, `CodeKeymap` and `TableScroll`'s keys sit inside left-to-right
surfaces and must not change. `DragDismiss` is vertical only, and `Drawer` already speaks `DrawerEdge.Start/End`
with no drag to close: neither has anything to mirror.

## Goals / Non-Goals

**Goals:**

- Every catalog component that names a direction behaves by it on the web with no change to the component.
- A `dir="ltr"` surface inside an `rtl` page keeps the left-to-right behaviour, with no special case.
- A culture switch and a nested `Directionality` that changes are followed with nothing lowered again for it.
- Photon unchanged, and pinned so.

**Non-Goals:**

- Mirroring Photon (layout, shaping, input). Outside 1.0.
- Marking the icon packs' directional glyphs (Lucide `arrow-left`, Heroicons `chevron-right`, …): Iconify carries no
  right-to-left metadata, and a rule by name is a guess (`arrow-up-right` is a direction in some sets and a
  "go out" link in others). A follow-up issue under #197; an app marks a pack glyph itself with
  `glyph with { MatchTextDirection = true }`.
- Mirroring a `Canvas`, a `Listener`'s positions or a `Drawing`: a picture has physical coordinates, as Flutter's
  `CustomPaint` and `Listener.localPosition` do.
- Fixing Photon's `Adjustable` vertical arrows (below, D9).

## How Flutter answers it

`docs/FLUTTER-PARITY.md`'s `TextDirection` row (line 164) is the starting point. Flutter answers each part at one of
its three trees:

- **Icons.** `IconData.matchTextDirection` is DATA on the configuration (the widget tree). The `Icon` widget reads
  `Directionality.of(context)` (the element tree) and paints through a `Transform` flipped on x (the render tree)
  when the data says so and the direction is `rtl`. `Icons.arrow_back`, `chevron_left`, `chevron_right` set it;
  `play_arrow` does not. Our `IconGlyph` is the same data; the "read the direction and flip" happens in our
  realizer, at the render level, which on the web is the DOM where `[dir]` and `--eq-inline` live.
- **Gestures.** `HorizontalDragGestureRecognizer` reports physical deltas (render tree); each widget that means a
  direction negates them itself under `rtl`: `Switch` (`ToggleableStateMixin`'s drag update), `Slider`
  (`_RenderSlider` reads `textDirection`), `Dismissible` (`DismissDirection.startToEnd` resolved through
  `Directionality`). Flutter can do that in the widget because its one engine mirrors its layout. Ours cannot
  (Photon does not mirror yet), so the negation moves down one level: into the realizer that binds the gesture,
  where it is done once for every drag rather than in each component.
- **Keys.** Flutter's `Slider` maps arrowRight to increase under `ltr` and to decrease under `rtl` in its own
  shortcuts; `DirectionalFocusIntent(TraversalDirection.left)` is physical. Same conclusion: the meaning is
  logical, the binding reads the direction.
- **Scrolling.** A horizontal `Scrollable`'s `AxisDirection` comes from `textDirectionToAxisDirection`, so
  `ScrollPosition.pixels` is 0 at the reading start in both directions. That is D6.

So the design keeps Flutter's names and Flutter's meaning, and binds at the render level because our widget level is
written once for two targets of which one does not mirror. `MatchTextDirection` passes "would this word exist if the
web did not?": it is Flutter's.

## Decisions

### D1. The realizer binds the direction; no component reads it

No component picks a glyph, a sign or a key from `ComponentContext.TextDirection`. That property answers the culture
on Photon too, and Photon lays out left to right for 1.0, so a component-level flip would mirror Photon's controls
inside an unmirrored layout: a back chevron pointing right in a layout whose start is on the left. Components keep
speaking logically (start/end, previous/next, "toward the end"); the vocabulary documents that meaning; the web binds
physical input and output to it per the direction where the element sits; Photon's binding stays physical, which is
logical left to right, until Photon mirrors.

Held by a guard that fails (`CatalogReadsNoDirectionTests`): no component of the catalog reads
`ComponentContext.TextDirection`, bound by symbol. Writing a `Directionality` stays allowed. And by the variant
matrix's right-to-left cell, whose Photon reading must equal its left-to-right one (D8).

### D2. Icons: `IconGlyph.MatchTextDirection`, drawn through `--eq-inline`

`IconGlyph` gains a trailing positional `bool MatchTextDirection = false`; `CuratedIcons.Resolve` sets it for
`ChevronLeft` and `ChevronRight`, the only sideways glyphs of `Icons` (`Play` is media and keeps its direction, as
Flutter's `play_arrow` does). Both web producers write, on the `<svg>` of a glyph that sets it:

```css
scale: var(--eq-inline, 1) 1;
```

**Changed from the brief's `transform: scaleX(...)`:** the individual `scale` property, for the reason #511 moved the
drag offset to `translate`: CSS composes `translate`, `rotate`, `scale` and `transform` instead of letting one
replace the other, so any transform an author or a parent writes on the element still applies. `HtmlStyle` gains
`Scale`, transcribing CSS one to one as `Translate` does (`HtmlStyle.cs:227`).

Because the sign is a custom property inherited from the nearest `[dir]`, the glyph turns under `rtl`, stays put
inside a `dir="ltr"` fence (the code block's fold chevron), and follows a culture switch or a nested
`Directionality` that changes with no re-lowering. The markup is the same in both directions, so the server's and
the browser's agree and the parity fixture holds them equal. Photon draws it unmirrored (`EmitVisitor.Media.cs`
rasterizes through `IconRasterCache`, keyed by the glyph; the new field only adds to the key).

The TypeScript twin: `IconTsGenerator` writes the set of curated glyphs that match, derived from
`CuratedIcons.Resolve`, into `icons.generated.ts`; `CuratedIcons.resolve` passes it to `IconGlyph`.

`Vector` draws the same glyph data through `LowerGlyph` and mirrors with it: the glyph says what it is, whoever
draws it.

The wiki's Localization page teaches choosing `ChevronRight` under right to left from `context.TextDirection`
(`Localization.md:144`, and `WikiClaimsCompile.cs:808-818`'s `BackLink`). With the flag that double-flips, so the
page changes to "draw the glyph and it turns", and that is a break an app meets (proposal).

### D3. A horizontal `Draggable` is logical on the web

The controller (`dom/draggable.ts`) reads the direction where the surface sits at pointerdown. For a horizontal
surface under `rtl` it multiplies the travel by -1 before it is compared with the slop, added to the rest and
clamped, and multiplies the painted `translate` by -1. Min, Max, the rest and the reported offset are all in the
logical space. A vertical surface is unchanged.

Both lowerings write a horizontal rest through `--eq-inline` (`translate: calc(var(--eq-inline, 1) * -96px)`, and
`calc(var(--eq-inline, 1) * 25%)` for a normalized rest), through one `TokenCss` helper and its TS twin, as
`Positioned.ShiftX` is (`TokenCss.cs:196-203`, `lowering.ts:2144-2148`).

**Clarified:** the brief asked for "the normalized fraction measured from the start edge". The controller measures no
edge: a normalized offset is `rest + travel / extent` (`draggable.ts:83-87`), relative to where the gesture started,
and `Slider` places its thumb by its value through flex weights in a `Row`, which already mirrors. Negating the
travel is the whole of it. Should a seek-to-pointer ever be added, it measures from the start edge.

`Switch` (Min/Max signed by state, thumb `Positioned` at start or end), `SwipeableRow` (Min `-actionWidth`, action
`Positioned` at the end), `Slider` (normalized, does not follow) and `DataTable`'s resize (rest = width, edge on
`BorderSides.End`) then drag correctly with no change to them. `gestureSpace` (a turned or scaled ancestor) is
applied first, and the sign after it.

### D4. A horizontal `Sortable` reads in the reading order on the web

`dom/sortable.ts` reads the direction of the list at pickup:

- A horizontal list under `rtl` hands `SortPlacement.place` each item's start as `-box.right` and the pointer as
  `-x`, so the starts ascend in reading order as `Place` assumes (`SortPlacement.cs:80-90`); the extents are
  unchanged. `SortPlacement` itself does not change: it is shared, transpiled, and Photon's.
- A vertical list's depth travel (`sortable.ts:176`) is negated under `rtl`, so a carry toward the end is deeper.
- The keyboard carry (`sortable.ts:676-698`) swaps ArrowLeft and ArrowRight under `rtl`: along a horizontal list
  (next/previous) and across a vertical one (deeper/shallower).

The carried copy's position (`sortable.ts:246`, `:319`), the lean (`:248`) and the edge scroll (`:202`) are
physical on purpose: they follow the finger on the screen.

### D5. The horizontal arrows: one table that takes the direction, read where the element sits

`Navigable` and `Adjustable` keep their logical meaning (`NavigableMove` already is logical; `OnAdjust(+1)` is
"toward the end / next"). The binding of ArrowLeft and ArrowRight to it takes the direction:

- `NavigableKeys.Move(string key, bool shift, TextDirection direction)` replaces the two-parameter shape. Under
  `RightToLeft` ArrowLeft answers `NextItem` and ArrowRight `PreviousItem`; every other key is unchanged. The TS
  twin `navigableMove(event, direction)` follows, and `NavigableKeyTableTests` cross-pins both directions.
  **Changed from the brief** (which swapped in the web handler only): the direction is a property of what a key
  means, so it belongs in the table both realizers read. Photon's call site then says explicitly what it binds
  (`TextDirection.LeftToRight`, with the reason), and when Photon mirrors it passes its own direction and nothing
  else moves.
- The `Adjustable` keydown, which is inline in each web twin (`WebLoweringVisitor.Interaction.cs:165-179`,
  `lowering.ts:5298-5320`), swaps ArrowLeft and ArrowRight under `rtl`. It stays inline: Photon has its own
  mapping (D9) and a new public table for one C# reader would be surface with no second reader.

**Where each twin reads the direction (the question the brief left open).**

- **The browser's handler reads it at the key**, from the DOM: `readsRightToLeft(event.currentTarget)`, the nearest
  `[dir]` ancestor-or-self saying `rtl`, `<html>` included. One helper in `src/dom/reading-direction.ts` serves the
  keys, the drag (D3), the sortable (D4) and the scroll (D6). It reads `[dir]` and not
  `getComputedStyle(el).direction` because `[dir]` is exactly what the SDK writes (the culture on `<html>`, a
  `Directionality` on its child, a fence on its host), and jsdom does not compute `direction` from `dir`, so the
  computed style would make every spec lie (claimed from jsdom's lack of a layout engine; the specs of group 7
  measure it). `:dir(rtl)` was the other candidate; jsdom's selector engine is not known to support it, and
  `closest('[dir]')` answers the same for every attribute the SDK writes.
- **The server's handler resolves it at lowering**, because it has no DOM. The visitor keeps the direction where it
  lowers, as it keeps `_inControl` and `_simulated`: the UI culture's at the root, the `Directionality`'s under one
  (`Visit(InheritedComponent)`, `WebLoweringVisitor.cs:289-293`), `LeftToRight` under a host it writes `dir="ltr"`
  on, each restored on the way out. The C# delegate never runs in a browser ("The server never dispatches a key, so
  this delegate does not run", `WebLoweringVisitor.Interaction.cs:327-330`): it is the declaration the parity net
  compares, and a C# test invokes it.
- **Why the two agree:** the attributes the browser reads are exactly the ones the server's visitor tracks while
  writing them, from the same three sources, so on the first render (and on hydration, which lowers what the server
  drew) both answer the same direction for every element. After that only the browser's runs, and it reads the DOM
  as it is then.

**Why the browser reads at the key and not at lowering (the culture-switch question).** A culture switch does lower
everything again: `setCulture` installs the culture and calls the invalidator (`utils/culture.ts:439-451`;
`installCulture` sets `document.documentElement.dir` at `:362`), the invalidator boot registers schedules the page's
render (`Sdk/Resources/boot.ts:327-330`), and a render that is not an owner's partial one "builds the page and
rebuilds every position" (`core/component.ts:321`, `requestPageBuild` at `:433`). So a lowering-time direction would
be right after a switch. It would be wrong after a `Directionality` that changes inside a nested component: that
render "builds the marked positions and nothing else" (`component.ts:316-320`), the `Directionality`'s
`updateShouldNotify` rebuilds only the components that READ the direction, and a kept child whose `Tabs` baked
`ltr` into its handler would keep it while the `dir` above it says `rtl`. #1568 chose this already: "a direction
that changes rewrites one attribute rather than the subtree's classes" (`WebLoweringVisitor.cs:281-283`). Every part
of this change follows it: the glyph and the rest offset through `--eq-inline` (CSS), the drag, the sortable, the
keys and the scroll through `[dir]` at the event.

ArrowUp and ArrowDown, PageUp/PageDown, Home/End are unchanged (Home/End are already the row's start and end).

### D6. A horizontal `ScrollView`'s offset counts from the start on the web

Under `rtl` the CSSOM's `scrollLeft` is 0 at the start and negative toward the end (CSSOM View; Chromium, WebKit and
Gecko agree since 2020; claimed, measured by task 7.8 in a browser). The scroll handler reports `-scrollLeft`
(`lowering.ts:2816`) and `scrollTo` sets `scrollLeft = -offset` (`scroll-viewports.ts:134-136`, used by the initial
offset and by a `ScrollRequest`). This is Flutter's `ScrollPosition.pixels`. No catalog component scrolls
horizontally outside a left-to-right fence (`Mermaid`, `CodeBlock`, `CodeEditor`, `TableScroll`, and `CodeDiff`
after D7), so the change is for apps. The table viewport's own `scrollLeft` readers (`table-viewports.ts`) are inside
`dir="ltr"` and unchanged.

### D7. Fenced surfaces keep their keymaps; `CodeDiff` joins them

`CodeKeymap`, `SheetKeymap` and `TableScroll`'s keys are their own and stay left to right. Since every reading of the
direction is `[dir]` where the element sits, a node inside a fence reads `ltr` with no special case.

**Added:** `CodeDiff` builds two horizontal `ScrollView`s and a gutter `Row` around `CodeSurface`s
(`CodeDiff.cs:309`, `:360-375`). The surfaces' hosts carry `dir="ltr"`, but the rows and scrolls around them do not,
so under `rtl` the gutter stands on the right and the overflow opens to the left. It builds inside
`Directionality(LeftToRight)` as `CodeBlock` does, and the spec's left-to-right requirement names it.

### D8. Photon does not change, and a pin says so

`PhotonHost` passes `TextDirection.LeftToRight` to `NavigableKeys.Move`; its `Adjustable` arm, its drag and its glyph
drawing are untouched. `TextDirectionPhotonTests` pins that under `ar` and under `Directionality(RightToLeft)` a
matching glyph draws, an arrow steps and a drag reports exactly as under `en`. The variant matrix gains one
right-to-left cell per control whose Photon reading is asserted equal to its left-to-right cell's: that is the
instrument that fails if a component starts flipping by the context (D1).

### D9. Photon's `Adjustable` vertical arrows (not this change)

`PhotonHost.cs:3685-3696` maps ArrowUp to +1 for every role, where the web maps ArrowDown to +1 for a tab list and a
radio group (`downIsNext`, WAI-ARIA's reading order). A pre-existing disagreement between the twins, unrelated to
direction: a bug to file under #1587, not fixed here (fixing it would be the Photon behaviour change D8 excludes).

### D10. Documentation

`docs/FLUTTER-PARITY.md` gains a row (`IconData.matchTextDirection` → `IconGlyph.MatchTextDirection`, SAME for the
web) and its `TextDirection` row says what follows the direction now. `docs/I18N-PLAN.md` D9 gets a dated line. The
wiki's Localization page (five languages) and Upgrading page (English and Portuguese) change, and one ledger file.

## Alternatives considered

- **The component flips by `context.TextDirection`** (Flutter's place). Rejected by D1: it mirrors Photon's controls
  inside an unmirrored layout, and it would be repeated in every component that names a direction.
- **Swap in the web handler only, `NavigableKeys` physical.** Cheaper by one signature, but the table is "a key as
  the move it means", and what ArrowLeft means depends on the direction; leaving the swap at call sites puts it in
  three places (C#, TS, the future Photon) instead of one cross-pinned table.
- **Bake the direction at lowering in the browser too** (the TS lowering can ask `directionSetBy`). Rejected by D5's
  measured answer: right after a culture switch, wrong after a nested `Directionality` that changes under a kept
  position.
- **`transform: scaleX(var(--eq-inline))` on the glyph.** Replaced by the individual `scale` property (D2).

## Risks / Trade-offs

- A horizontal `ScrollView`'s offset changes meaning under `rtl` for an app that compensated for `scrollLeft`
  itself. Listed as a break.
- An app that drew a pack glyph as directional gets no mirror until it sets `MatchTextDirection` (follow-up issue).
- The server's declared handlers carry a direction resolved at lowering; if a C# host ever dispatched keys after a
  client-side direction change, it would hold the old one. No such host exists.
- One extra variant-matrix cell per control adds pins (one file section each).

## Measured

- `.NET`: `CultureInfo(c).TextInfo.IsRightToLeft` is `true` for `ar`, `he`, `fa`, `ur` and `false` for `en`,
  `pt-BR` (`dotnet fsi`, .NET 10, 2026-11-24). This is the direction a page with no `Directionality` reads.
- The culture switch path, by reading the code (file:line in D5); the engineer's spec of group 7 asserts it in the
  runtime.
- Not measured, claimed: jsdom computing no `direction` from `dir`; `scrollLeft`'s negative origin under `rtl` in the
  three engines; the record struct's `PublicAPI` lines (the analyzer names them in task 1.5).

## Open Questions

- Does the visitor's direction field (D5) survive a component boundary in the C# lowering? If a component's subtree
  is lowered by another visitor instance (a partial render, an adaptive arm), the engineer either carries the field
  into it or resolves the direction from the position as `ComponentContext.TextDirection` does
  (`UiComponent.cs:80-84`, `DependOn(Directionality)`); a C# test with an `Adjustable` inside a component under
  `Directionality(RightToLeft)` decides which.
- Whether the variant matrix's right-to-left cell should also pin its web markup, or only assert Photon's equality.
  The tasks pin it; drop the web pin if it proves to be the left-to-right pin plus one `dir` attribute everywhere.
