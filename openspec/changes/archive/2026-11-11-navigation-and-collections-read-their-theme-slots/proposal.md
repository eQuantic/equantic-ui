# Proposal

Closes #1244 (E6d), the last slice of #938, per-component theme slots, on the mechanism of E6a
(#1241). The design is `docs/ELEMENT-TREE-PLAN.md` section 3.10.

## Why

Tabs, the navigation bar and rail, the app bar, breadcrumbs, pagination, page indicators, lists,
dividers, accordions, swipe rows, tables, badges, avatars, empty states and the calendar still wrote
their look as constants: `MaterialTheme` could repaint them and could not make a navigation bar 80dp
or a list item 56dp, as Material 3 specifies. And the rendered document's style, `MarkdownStyle`,
was the one style a theme could not give: a page set it one Markdown at a time.

## What Changes

- `IAppTheme` gains six slots, each a default member that answers null, so every theme an app wrote
  keeps compiling: `Navigation` (`NavigationTheme`), `List` (`ListTheme`), `Table` (`TableTheme`),
  `Badge` (`BadgeTheme`), `Calendar` (`CalendarTheme`) and `Markdown` (`MarkdownTheme`). Every member
  is nullable data: heights, widths, paddings, gaps, border widths, radii, elevations, the label's
  size, line height and weight, the roles a calendar and a document set their text in, and the
  colours each component chose itself. A colour taken from `Colors(variant)` stays there, and
  behaviour (a pagination's window, a page indicator's dot limit) stays in the component.
- Each of the twenty components reads its family's slot, then its own default. A list's divider still
  starts where the row above starts its text, from the same slot as the row's own padding and gap.
- `MarkdownStyle` becomes `MarkdownTheme`, a record in Primitives: `Markdown.Style` is the first step
  of the order, member by member, then the theme's slot, then the default.
- `theme.With(…)` takes the six slots after the two it took.
- `PhotonTheme` fills the six with today's constants, so its pins are byte for byte. `MaterialTheme`
  fills Navigation, List, Badge and Calendar from Material 3's specifications and leaves the rest to
  the components, since Material 3 specifies no table, avatar, empty state or rendered document.

Breaks: `MarkdownStyle` is gone (`MarkdownTheme` takes its place, its members nullable), and
`SwipeableRow.ActionWidth` and `DataTable.RowHeight` become the slot members `ListTheme.SwipeActionWidth`
and `TableTheme.DataRowHeight`. The developer surface does not move.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `theme-slots`: navigation and collections read their slots, and the document's style is one.

## Impact

Primitives (the six records, `IAppTheme`, `OverriddenTheme`, `AppThemeExtensions`, `PhotonTheme`),
twenty components, `MaterialTheme`, the runtime's slot mirrors, builders, interface defaults and
exports, the generated Photon theme, the dashboard sample's Markdown screen, and the wiki's
DesignSystem and Markdown pages in five languages.
