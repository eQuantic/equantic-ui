## Why

Closes #1569, a sub-issue of #197 (semantics and text direction), part of the block catalog 1.0 waits for.

#1568 made a page read the way its language does: the web writes every start and end as logical CSS, so an Arabic
page lays out mirrored. What only the SDK draws or hears did not follow. A chevron that means "back" still points
left in Arabic. A horizontal `Draggable` measures `clientX - startX` against limits written for left to right and
paints a physical `translate`, so under `rtl` a `Switch`'s thumb clamps when dragged toward its end and leaves its
track when dragged toward its start, and a `SwipeableRow` opens by a swipe the wrong way. ArrowRight is "next" in
every tab list, radio group, slider, calendar and tree, where an Arabic reader's next is to the left. A horizontal
`Sortable` hands its items' left edges to `SortPlacement.Place` as starts, which breaks the placement under `rtl`,
and a horizontal `ScrollView` reports `scrollLeft`, whose origin moves under `rtl`. #1568's design handed exactly
these to this issue (its decision 7 and its risks), and #1594's handed the sortable's cross travel and cross arrows.

## What Changes

For a developer: the C# they write does not change. A component written in start and end, previous and next,
"toward the end", now behaves that way under `Directionality(TextDirection.RightToLeft)` or an `ar` culture on the
web, for icons, drags and keys, with no `context.TextDirection` test in their code. The one new thing they may
write is `MatchTextDirection: true` on a glyph of their own.

- **`IconGlyph.MatchTextDirection`** (Flutter's `IconData.matchTextDirection`), default `false`: the glyph is drawn
  mirrored where its element reads right to left. The curated `Icons.ChevronLeft` and `Icons.ChevronRight` set it.
  The web draws such a glyph with `scale: var(--eq-inline, 1) 1`, so it turns under `rtl`, stays put inside a
  `dir="ltr"` surface, and follows a culture switch with no re-render. `HtmlStyle` gains the CSS `Scale` property
  it needs (the escape hatch transcribes CSS one to one).
- **A horizontal `Draggable` is logical on the web**: travel toward the end is positive in both directions. The
  controller reads the direction where the surface sits at pointerdown and negates horizontal travel and the
  painted offset under `rtl`; both lowerings write a horizontal rest offset through `--eq-inline`, as
  `Positioned.ShiftX` is. `Switch`, `SwipeableRow`, `Slider` and `DataTable`'s column resize drag correctly with no
  change to them.
- **A horizontal `Sortable` follows the reading order on the web**: its items' starts are measured from the
  reading start, depth travel toward the end is deeper in both directions, and the keyboard carry's horizontal
  arrows swap under `rtl`.
- **The horizontal arrows mean previous and next by the direction on the web**: `Navigable` and `Adjustable` keep
  their logical moves, and their keydown (the browser's handler and the server's declaration) swaps ArrowLeft and
  ArrowRight where the element reads right to left. `NavigableKeys.Move` takes the direction: the old two-parameter
  shape goes.
- **A horizontal `ScrollView`'s offset counts from the start** on the web, in what it reports and what it is asked.
- **`CodeDiff` builds left to right** inside `Directionality(LeftToRight)`, as `CodeBlock` does: its gutter and its
  horizontal scroll were the one code view #1568 left unfenced.
- **Photon does not change.** It reads the direction and lays out left to right for 1.0, so its binding of a key,
  a drag and a glyph stays physical, which is logical left to right. A pin holds it.

### Breaks (preview)

- `IconGlyph`'s positional constructor and `Deconstruct` gain a trailing `MatchTextDirection`; a call with five
  arguments still compiles, a five-output deconstruction does not. Migration: `var (name, path, style, box, width, _) = glyph;`.
- `NavigableKeys.Move(string key, bool shift)` goes; `NavigableKeys.Move(string key, bool shift, TextDirection direction)`
  replaces it. Migration: pass `TextDirection.LeftToRight` for the old answer.
- An app that chose `Icons.ChevronRight` for "back" under right to left by reading `context.TextDirection` (as the
  wiki's Localization page taught) now draws it double-flipped. Migration: draw `Icons.ChevronLeft` and let it turn.
- A horizontal `ScrollView`'s `Offset`, `ScrollRequest` and `OnScrolled` read from the right under `rtl`, where they
  read from the left; for `ltr` nothing changes.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `text-direction`: a glyph that matches the direction, a horizontal drag, a horizontal sortable list, the
  horizontal arrows and a horizontal scroll offset follow the direction where they sit; `CodeDiff` joins the code
  views that read left to right.
- `adjustable-keyboard`: an `Adjustable`'s horizontal arrows swap where it reads right to left on the web, from the
  table both targets read (`AdjustableKeys`), while Photon keeps left to right.

## Impact

- `eQuantic.UI.Primitives`: `IconGlyph`, `CuratedIcons`, `NavigableKeys`; documentation of `Draggable`, `Sortable`,
  `ScrollView`, `Adjustable`, `Navigable`. `PublicAPI.Unshipped` moves (the `IconGlyph` constructor and
  `Deconstruct`, the `MatchTextDirection` property, `NavigableKeys.Move`).
- `eQuantic.UI.Web`: `LowerGlyph`, `LowerDraggable`, `LowerAdjustable`, `LowerNavigable`, the visitor's direction,
  `TokenCss`, `HtmlStyle.Scale`. `PublicAPI.Unshipped` moves (`HtmlStyle.Scale`).
- `eQuantic.UI.Web.Build`: `IconTsGenerator` carries the flag into `icons.generated.ts`.
- `eQuantic.UI.Runtime`: `lowering.ts`, `vocabulary.ts`, `nodes.ts`, `dom/draggable.ts`, `dom/sortable.ts`,
  `shared/scroll-viewports.ts`, a new `dom/reading-direction.ts`.
- `eQuantic.UI.Components`: `CodeDiff` only (and its transpiled pin). The rest of the catalog is unchanged.
- `eQuantic.UI.Native.Components`: `PhotonHost` passes `TextDirection.LeftToRight` to `NavigableKeys.Move`.
- No change to eqc, the SDKs, the templates or the server. `developer-surface.baseline.txt` does not move.
- Docs: `docs/FLUTTER-PARITY.md`, `docs/I18N-PLAN.md` (D9), the wiki's Localization and Upgrading pages in every
  language they are written in, a ledger file under `docs/ledger/`.

## Principles

- Follows 4 (written once, realized per target): no component changes; the realizer binds the vocabulary's
  logical meaning to physical input and output. A component-level flip would mirror Photon's controls inside a
  layout Photon does not mirror.
- Follows 5 (no target's language) and 6 (Flutter first): `MatchTextDirection` is Flutter's word, and "toward the
  end" and previous/next are the vocabulary's own. `--eq-inline` and `[dir]` stay inside the web realizer.
- Follows 9 (the structural fix, the instrument that fails): the direction is read where the element sits, so a
  fence, a nested `Directionality` and a culture switch need no special case; a guard fails when a catalog
  component reads `TextDirection`, and a Photon pin fails when a right-to-left cell measures differently.
- Follows 13 (break freely): `NavigableKeys.Move` and the `IconGlyph` record change shape with nothing kept beside.
- Bends nothing.
