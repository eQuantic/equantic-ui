- **2026-11-24 · A glyph turns, a drag, a sortable list and the arrow keys follow the direction the page
  reads**: #1568 mirrored what the layout says, and what only the SDK knows still read left to right: a
  back chevron pointed at the wrong edge in Arabic, a `Switch` dragged toward its end, the arrow keys of
  a tab strip and a horizontal scroll offset all meant the physical side. `IconGlyph` gains
  `MatchTextDirection` (Flutter's `IconData.matchTextDirection`), which the curated `ChevronLeft` and
  `ChevronRight` set and the web draws with `scale: var(--eq-inline, 1) 1`; a horizontal drag, a
  sortable list and a scroll offset are read toward the end and from the start; and
  `NavigableKeys.Move` takes the direction, so ArrowLeft steps to the next item where the page reads
  right to left. A subtree under `Directionality(TextDirection.LeftToRight)`, and the code, table and
  sheet hosts, keep the English reading. Photon binds physical as logical and stays outside 1.0.
  `CatalogReadsNoDirectionTests` refuses a catalog component that picks a glyph from
  `ComponentContext.TextDirection` ([#1569](https://github.com/eQuantic/equantic-ui/issues/1569)).
  Proposed and archived through OpenSpec (`openspec/specs/text-direction`).
