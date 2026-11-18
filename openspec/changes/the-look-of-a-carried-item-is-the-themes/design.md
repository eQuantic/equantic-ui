## Context

`Sortable` and `SortableItem` carry an item by pointer and keyboard on both targets (#1535, #1536, #1537). While an item
is carried, the item is dimmed in its place and, for a pointer, a copy of it follows the pointer under a shadow and
leans with the pointer's speed across. Every number of that look is a constant of `SortPlacement`, the transpiled
class both targets run: `LiftedOpacity`, `CopyOpacity`, `CopyShadowOffsetY`, `CopyShadowBlur`, `CopyShadowAlpha` and
`TiltMax`. The theme's slot records (`ButtonTheme`, `TableTheme`…) are how a design system sets a family's look, a
component's `style:` lays a record over its theme's (#1546), and `ThemeBridge` writes every slot into the theme's JSON
the browser reads.

## Goals / Non-Goals

**Goals:** a theme and a list set the dimmed opacity, the copy's opacity and shadow and the most the copy leans, on
both targets, the defaults drawing what they draw now but for the shadow's colour.

**Non-goals:** the line a component draws where an item lands, which is the component's (a tree's `TreeDropColor`,
a table's `ColumnDropColor`); the keyboard's look, which only dims the item; the time a stopped copy keeps leaning and
the lean per speed, which stay the arithmetic's.

## Decisions

### How Flutter does it

Flutter's `ReorderableListView` takes a `proxyDecorator`, a builder that wraps the dragged item with whatever look the
app wants, and its default lifts it with a `Material` of elevation 6 animated from 0. The look is the app's per list,
and the default reads the theme's elevation. Here a builder would hand the app a node to draw where the realizer draws
a copy of the item it already has, on two targets; the decision on #1545 is the record the rest of the catalog uses,
which a theme sets once for every list. The default reads the theme's elevation scale, as Flutter's does.

### A slot record, named for its family

`SortableTheme` holds `LiftedOpacity`, `CopyOpacity`, `CopyElevation` and `MaxLean`, each nullable, merged member by
member like every slot. The shadow is an elevation level, as `CardTheme.Elevation` and the panels' `PanelElevation`
are: the theme turns the level into its shadow (`IAppTheme.Elevation`), so a theme's own shadows carry over and a dark
theme's shadow is its dark one. The level the copy took before matched Photon's level 4 in offset and blur, 12 and 28,
and the colour was a black at 0.28 in both modes; the default becomes the theme's level 4, whose colour is the
theme's.

`IAppTheme.Sortable` defaults to null, and the defaults live in `SortPlacement`, beside the arithmetic both targets
already run: `LiftedOpacity`, `CopyOpacity`, `CopyElevation` (4) and `TiltMax`. `Tilt` takes the most the copy leans,
and 0 keeps it upright, which is what asking for less motion already does.

Alternatives: three shadow members (offset, blur, colour) would make the copy's shadow a second shadow scale beside
the theme's. A builder of the copy (Flutter's `proxyDecorator`) is answered above.

### A list's own record, laid over the theme's

`Sortable(child, id, onDropped, announcements = null, style = null)`: a constructor parameter, so the factory reaches
it (`UI.Sortable(…, style:)`), as `style:` reaches a component's. The look of a carried item is its source list's,
as its words are (#1564): a card carried into another column keeps the look it was picked up with.

### Each target resolves it once

The web lowering resolves `SortableTheme.Merge(theme.Sortable, list.Style)` with the defaults and writes the four
values on the list's element (`data-eq-sort-lifted`, `data-eq-sort-copy-opacity`, `data-eq-sort-copy-shadow`, the
shadow in `TokenCss.Shadow`'s spelling, and `data-eq-sort-lean`); the controller reads them from the source list when it
lifts the item. Photon resolves the same record when the host lifts the item and keeps it on the item's
`SortLiftState`, which the emit walk reads to dim the item and record the copy's shadow, and the host to draw the copy
and lean it.

## Risks / Trade-offs

- The default copy's shadow changes colour: the theme's level 4 instead of a black at 0.28, lighter in a light theme
  and darker in a dark one. Its offset and blur stay.
- Every sortable list's element carries four attributes more; the parity fixture and the served runtime grow by them.
