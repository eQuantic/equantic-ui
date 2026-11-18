- **2026-11-18 · The look of a carried item is the theme's and the list's**: `SortableTheme` is a new slot of the
  theme (`IAppTheme.Sortable`, `With(sortable:)`), and `Sortable` takes `style`, laid over it member by member: the
  opacity of the item in its place, the copy's opacity, the copy's shadow as a level of the theme's elevation scale and
  the most the copy leans. Where neither says, the defaults are `SortPlacement`'s, written once with the arithmetic
  both targets run (`SortPlacement.Look`), and the copy's shadow becomes the theme's level 4, the offset and blur it
  had in the theme's own colour. The web's controller lays a list's own look, written on its element, over the
  browser's theme when it lifts an item, and Photon resolves the same look when it lifts one
  ([#1602](https://github.com/eQuantic/equantic-ui/issues/1602)). Proposed and archived through OpenSpec
  (`openspec/specs/sortable-lists`).
