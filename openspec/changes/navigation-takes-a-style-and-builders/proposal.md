## Why

Closes #1610, the style and builder half of #1549, a family slice of #1545 (the catalog takes the developer's
content, builders and style), whose design is #1580's
(`openspec/changes/archive/2026-11-16-the-catalog-takes-the-developers-content-and-style`, decisions 4 to 9).
The family's content half, the app bar's leading, title and actions and a destination's icon as nodes, is
#1611, a later pull request.

A developer who wants one navigation component drawn otherwise than the theme draws every one (a tab strip
taller than the app's others, a rail without its trailing edge, a breadcrumb in the brand's colour) cannot say
so today: the seven components read `theme.Navigation` and nothing else. A theme can restyle every tab strip at
once; one tab strip cannot restyle itself, and a tab, a destination or a crumb is a string drawn the one way
the component draws it.

## What Changes

- **Style.** Every component of the family takes `style:`, a `NavigationTheme`, laid over the theme's slot with
  the record's `Merge`, the first step of §3.10's order, as the inputs already do with `InputTheme`: `Tabs`,
  `AppBar`, `BottomNavigation`, `NavigationRail`, `Breadcrumb`, `Pagination` and `PageIndicator`. One record
  styles the whole family, so no component needs a second one. `Pagination` hands the merged slot to the page
  cells and the ellipsis it draws, which read the theme's slot again today; its Previous and Next steps are
  `Button`s, styled by the button's own record, which a style of the pagination does not reach.
- **Builders.** What the family repeats is drawn by a builder over the part's index, `Func<int, VisualNode>`,
  named as decision 5 names it:
  - `Tabs(tabBuilder:)` draws a tab's content, by its index in `labels`, the index `selected` and `onSelect`
    count;
  - `BottomNavigation(destinationBuilder:)` and `NavigationRail(destinationBuilder:)` draw a destination's
    label, by its index in `items`, under the pill that keeps the destination's glyph, its badge and the
    selection;
  - `Breadcrumb(crumbBuilder:)` draws a crumb, by its index in `crumbs`.
  The component keeps its chrome (the tab's indicator and pressed fill, the destination's pill, the
  breadcrumb's separators and links), as decision 5 says, and hands the content the look it resolved for the
  part, its colour and its type, so a builder that answers `Text(labels[i])` draws as the tab did. The part's
  text stays its accessible name (decision 4). `Pagination` and `PageIndicator` repeat cells drawn from a
  number the component computes, never the developer's items, and `AppBar` repeats nothing, so they take no
  builder.
- All of it is optional named constructor parameters, mirrored by the factories (decision 7). `Breadcrumb`,
  `Pagination` and `PageIndicator` have no factory today and are reached only with `new`; each gets the factory
  that mirrors its constructor, so the style is reached by the declarative surface, as principle 15 asks. None
  of the seven is stateful, so no `AdoptConfig` moves: a stateless component is built again from the
  configuration its parent hands it.

What a developer writes:

```csharp
Tabs(sections, chosen, i => SetState(() => chosen = i), style: new NavigationTheme { TabHeight = 56 })

NavigationRail(destinations, page, Go,
    style: new NavigationTheme { RailBorderWidth = 0 },
    destinationBuilder: i => Text(destinations[i].Label, TypeRole.Label))

Breadcrumb(path, crumbBuilder: i => i == 0 ? Icon(Icons.Home) : Text(path[i].Label))
```

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `catalog-customization`: the navigation components take their family's record as their style, and a tab
  strip, a bottom bar, a rail and a breadcrumb draw their parts with the developer's builder.

## Impact

- **Components** (`eQuantic.UI.Components`): the seven components and their factories in `UI.cs`, three of the
  factories new. Each widened constructor and the factory that mirrors it declare their new signature and
  retire their old one in `PublicAPI.Unshipped.feat-navigation-takes-a-style-and-builders.txt`, as a preview
  allows. `NavigationTheme` itself does not move: its twin's `merge` already takes `undefined`.
- **Runtime**: the components' transpiled twins and `UI.ts`, regenerated. No runtime code is written by hand.
- **Web realizer, Photon, eqc, the SDKs, the templates**: untouched; the components are written once. The
  developer surface (`developer-surface.baseline.txt`) does not move.
- **The guard**: the family's seven `style` entries leave `navigation.baseline.txt`; the `kept:` text entries
  stay, and `AppBar.Title` stays for #1611.
- **Call sites**: the samples and tests that write `new Breadcrumb(…)`, `new Pagination(…)` and
  `new PageIndicator(…)` move to the factories where they import the declarative surface.
- **Docs**: the wiki's Components page in every language it is written in; a ledger entry.

A break, in preview: each widened constructor's old shape goes. An app that calls a component positionally past
its last parameter writes the new parameter by name; named arguments compile unchanged. The three new factories
shadow their types' names in a file that imports the declarative surface: `Pagination.Pages`,
`Pagination.Window`, `Pagination.AlwaysShow` and `PageIndicator.MaxDots` there fail with CS0119 (measured, see
the design's "The shadowing, measured"), and the migration line is to write them through their namespace,
`eQuantic.UI.Components.Pagination.Window`.

## Principles

- **1, never a platform artifact**: a navigation component restyles itself in C#, with the record its theme
  already speaks.
- **4, written once**: the components are shared, so both targets take the style and the builders at once.
- **6, Flutter**: a builder over the index is `ListView.builder`'s and `TabBar`'s `Tab(child:)`, and a
  per-instance style record is Material 3's `ButtonStyle` shape, as decision 6 says; `NavigationTheme` already
  answers `TabBarTheme`, `NavigationBarThemeData`, `NavigationRailThemeData` and `AppBarTheme`.
- **13, break freely in preview**: the widened constructors replace the old ones, with no overload kept.
- **15, customized all the way**: the three components without a factory get one, so their style is reached
  by the surface an app writes.
