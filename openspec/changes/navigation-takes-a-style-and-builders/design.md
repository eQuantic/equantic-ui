## Context

#1580 decided how every family of the catalog takes the developer's content, builders and style (its design,
decisions 1 to 9) and left the families to slices. This one is #1610, #1549's style and builder halves; the
content half is #1611, which makes the app bar's leading, title and actions and a destination's icon nodes.
The inputs and pickers took the same two halves in #1608
(`openspec/changes/archive/2026-11-26-inputs-and-pickers-take-a-style-and-builders`), and this change follows
its shape.

The family's record is `NavigationTheme` (`src/eQuantic.UI.Primitives/Theme/NavigationTheme.cs`), on main
since #938, every member nullable, with a `Merge`. Its runtime twin (`theme-slots.ts`) already takes
`undefined` as well as `null` in `merge`, which the inputs' records had to learn in #1608 because a transpiled
component hands a style no initializer set as `undefined`; nothing moves there.

What the seven components read today, from the code on main:

| Component | Reads its slot | `NavigationTheme` members it draws | Repeats |
|---|---|---|---|
| `Tabs` | `theme.Navigation`, once in `Build` | `TabHeight`, `IndicatorThickness`, `IndicatorInset`, `IndicatorRadius`, `InactiveColor`, `TabLabelSize`, `TabLabelLineHeight`, `TabLabelWeight`, `TabSelectedLabelWeight`, `TabLabelTracking`, `QuietFill` | a tab per label |
| `AppBar` | `theme.Navigation`, once in `Build` | `TitleColor`, `TitleSize`, `TitleLineHeight`, `TitleWeight`, `TitleGap`, `TitleInset`, `AppBarHeight`, `AppBarPaddingX`, `Background`, `ScrolledElevation` | nothing (its actions are content, #1611's) |
| `BottomNavigation` | `theme.Navigation`, once in `Build` | `InactiveColor`, `BarIconSize`, `BarPillWidth`, `BarPillHeight`, `PillRadius`, `PillPressOpacity`, `PillLabelGap`, `BarLabelSize`, `BarLabelLineHeight`, `BarLabelWeight`, `BarLabelTracking`, `BarHeight`, `Background` | a destination per `NavItem` |
| `NavigationRail` | `theme.Navigation`, once in `Build` | `RailGap`, `InactiveColor`, `RailIconSize`, `RailPillWidth`, `RailPillHeight`, `PillRadius`, `PillPressOpacity`, `PillLabelGap`, `RailItemHeight`, `RailSelectedLabelSize`, `RailSelectedLabelLineHeight`, `RailSelectedLabelWeight`, `RailWidth`, `RailPaddingY`, `Background`, `RailBorderWidth`, `RailBorderColor` | a destination per `NavItem` |
| `Breadcrumb` | `theme.Navigation`, once in `Build` | `CrumbGap`, `SeparatorSize`, `SeparatorColor`, `CurrentCrumbColor`, `CrumbColor` | a crumb per `Crumb` |
| `Pagination` | `theme.Navigation` THREE times: in `Build`, and again in its helpers `Number` and `Ellipsis` | `PageGap`, `PaginationGap` (in `Build`); `PageCellSize`, `PageColor`, `PageRadius`, `QuietFill` (in `Number`); `PageCellSize`, `EllipsisColor` (in `Ellipsis`) | a cell per page number it computes |
| `PageIndicator` | `theme.Navigation`, once in `Build` | `PageColor`, `DotSize`, `DotGap`, `CurrentDotWidth`, `DotColor`, `DotRadius` | a dot per page it counts |

Every member of the record is read by at least one of the seven, and no component outside them reads
`theme.Navigation`. Each component reads one slot of its own; none reads none and composes one that does, so no
component hands its style down inside the family. What the seven draw from `IAppTheme.Colors(...)` rather than
the slot is the selection (the active tab's label and indicator, the active pill's fill and tint, the current
page's cell, the current dot), which the record's own summary keeps in the theme's colours on purpose: a
selection colour is the variant's, never a navigation member. None of them draws a member the record names from
the tokens instead, as `FileUpload`'s border and fill did in #1608.

## Decisions

### The style is merged once, where the component read the theme

The merge replaces the read: `var slot = NavigationTheme.Merge(theme.Navigation, Style);`, the first step of
§3.10's order. Everything the component derives from the slot follows the instance's record, with nothing else
changed. One record styles every component of the family, so none takes two (the date pickers' `calendarStyle`
has no counterpart here).

`Pagination` is the one place where replacing the read is not enough: its `Number` and `Ellipsis` helpers read
`theme.Navigation` again, so a style merged in `Build` alone would reach the gaps and miss every cell. The
helpers take the merged slot as a parameter instead, and the theme's slot is then read in one place. The test
of the style asserts a cell's member (`PageCellSize` or `PageColor`), so it fails on that most plausible wrong
merge, not only on a missing one. Its Previous and Next steps are `Button`s, which take the button's record:
a style of the pagination does not reach them, as a style of `CultureSwitcher` does not reach its menu.

`ListDetail` (the lists' family) composes an `AppBar` with its own `ListTheme`; a style of the list does not
reach the bar. A list detail that wants its bar styled is #1551's question, not this slice's.

### The builders draw the part's content, and the text stays its name

Decision 5 names them: `tabBuilder`, `destinationBuilder`, `crumbBuilder`, each a `Func<int, VisualNode>` over
the index the component already counts, after `style` in the constructor, as `SegmentedControl(style:,
segmentBuilder:)` and `Select(style:, optionBuilder:)` are.

- **A tab** (`Tabs`): the builder answers what sits centred in the tab above the indicator, in the place of the
  label. The tab keeps its indicator, its pressed fill, `PressableRole.Tab`, `Selected` and the arrow keys of
  the strip; `Pressable.Label` stays `labels[i]`.
- **A destination** (`BottomNavigation`, `NavigationRail`): the builder answers what sits under the pill, in
  the place of the label. The pill, with the destination's glyph, its badge, the selection's fill and the
  pressed preview, is the selection mark of spec B4 and C16, so it stays the component's; the glyph as a node is
  #1611's. `Pressable.Label` stays `item.Label`. The bar and the rail take the same name, since they take the
  same `NavItem` list and a shell hands both the same builder inside an `AdaptiveNode`.
- **A crumb** (`Breadcrumb`): the builder answers the crumb. The separators between crumbs stay the
  component's; an ancestor with a `Destination` is still a `Link` around the content with `Label = crumb.Label`,
  and the current crumb is still never a link.

Rejected: a destination builder that answers the whole cell, pill included. The pill is chrome, and a builder
that replaced it would make every app redraw the selection, which decision 5 keeps in the component. Rejected:
one builder for the glyph and the label together, drawn in two places: a function answers one node, and the
glyph is the content half's (#1611).

### The part hands its builder's content the look it resolved

#1608 learned it on review: a select's option drew its own text in the colour it resolved and handed a builder's
content nothing, so a `Text` with no colour of its own was drawn in the default ink. #1577 has since given a box
the whole look its content takes (`BoxStyle.Foreground` and `BoxStyle.Content`, Flutter's `DefaultTextStyle` and
`IconTheme`). So each part puts the builder's node in a box that states what the part's own text was drawn with:

| Part | `Foreground` | `Content` |
|---|---|---|
| a tab | `primary.Base` when selected, `InactiveColor ?? TextMuted` otherwise | `Caption`, the tab's `TypeStyle` by selection (`TabLabelSize`, `TabLabelLineHeight`, the two weights, `TabLabelTracking`), one line |
| a bar's destination | the tint (`primary.OnSubtle` or `InactiveColor ?? TextMuted`) | `Caption`, the bar's `TypeStyle` (`BarLabel*`), one line |
| a rail's destination | the tint | `Caption`, the rail's selected `TypeStyle` when selected and none otherwise, one line |
| a crumb | `CurrentCrumbColor` for the last crumb, `CrumbColor` for the others | `Caption`, one line |

A builder that answers `Text(labels[i])` then draws as the part did with no builder, which is the test that the
look reaches the content; a node that names its own look wins, as everywhere.

### Breadcrumb, Pagination and PageIndicator get their factories

The three have no factory in `UI.cs`: an app reaches them only with `new`, which the declarative surface does not
write (the samples' `ConsoleLayout`, `PaymentsPage` and the Studio's gallery do), and principle 15 asks for a
style the factory reaches. Each gets the factory that mirrors its widened constructor. The call sites that write
`new` today keep it: the two in the dashboard (`ConsoleLayout`, `PaymentsPage`) sit in screens that already mix the
two shapes, nothing there is shadowed, and both screens are README pictures (`assets/readme/captures.json`), which a
move would make taken again for a screen that does not change.

A factory named like its type shadows the type in a file that imports the surface by `using static`, so a static
of the type is no longer reached by its simple name there: `Pagination.Pages`, `Pagination.Window`,
`Pagination.AlwaysShow` and `PageIndicator.MaxDots`. None of the four is a construction path, so
`AStaticFactoryBehindAMirroredName_HasANamedFactory` does not apply, and no call site in the tree reads one
through the surface (`ConsoleComponentTests` and `GalleryComponentTests` import no `using static`). What an app
meets is measured in "The shadowing, measured" below.

### Pagination and PageIndicator take no builder

They repeat cells drawn from numbers the component computes (the elided window of page numbers, a dot per
page), never the developer's items, and the guard lists no text of theirs. A page number's look is the record's
(`PageCellSize`, `PageRadius`, `PageColor`), which the style now reaches. A builder for them is a later question
if an app asks for one, not this issue's (#1610 names the tabs, the destinations and the crumbs).

## Risks / Trade-offs

- **A builder's content that takes more room than the text did** is laid out by the part as any child is: a tab
  keeps the strip's height, a destination its cell's, a crumb the row's line, all from the slot.
- **The current crumb's name with a builder**: an ancestor's name is the `Link`'s `Label`, kept. The current
  crumb is not a control, so its name is the text its content carries; a builder that answers an icon alone
  leaves it unnamed (see Open Questions).
- **The conformance and variant pins** must show the same output with no style and no builder: the merge of a
  null record is the theme's own, and a missing builder draws the text as before. The box that hands the look
  is written only when a builder is given, so the tree with no builder is the tree on main.
- **A new factory's name** shadows four statics in an app that imports the surface, as a compile error that
  names the method (CS0119), measured below; it goes into the release notes with its migration line.

## The shadowing, measured

Measured with the Roslyn of the pinned SDK (10.0.101), compiling in memory from `dotnet fsi` a `Lib.Pagination`
type with a const and a static method beside a `Lib.UI.Pagination(int)` factory, imported as the SDK imports
them (`global using Lib;` and `global using static Lib.UI;`), and a page in another namespace:

| The page writes | Roslyn answers |
|---|---|
| `Pagination(3)` | compiles, the factory |
| `new Pagination(3)` | compiles, the type |
| `o is Pagination` | compiles, the type |
| `Pagination.Pages(2)` | CS0119: 'UI.Pagination(int)' is a method, which is not valid in the given context |
| `Pagination.Window` | CS0119, the same |
| `Lib.Pagination.Pages(2)`, `Lib.Pagination.Window` | compile |

So the break is a compile error, never a silent rebinding, and its migration line is one: an app that reads
`Pagination.Pages`, `Pagination.Window`, `Pagination.AlwaysShow` or `PageIndicator.MaxDots` in a file that
imports the declarative surface writes `eQuantic.UI.Components.Pagination.Pages(…)` (or a `using` alias). The
same holds for `Spacer.Fixed` today, which CLAUDE.md records; here no named factory is owed, since none of the
four builds the type. Rejected: keeping the three without factories, which leaves their style reachable only by
`new`, against principle 15.

## Open Questions

- **The current crumb drawn by a builder** takes its name from what the builder returns. A leaf names itself
  (`Icon(glyph, size, color, label)`, `Image(…, label)`, `Drawing(…, label)`, `Vector(…, label)`), so an icon-only
  crumb is `crumbBuilder: i => Icon(Icons.Home, label: "Home")`, and the docs show it. What the vocabulary cannot
  name yet is a composite part with no labelled leaf (a Row of a glyph and a decoration), which today is only merged
  or hidden: that is Flutter's `Semantics(label:)`, #1935, and it does not block this change.
- **#1611 and the destination's label** (settled: #1611's text now says the icon becomes a node and the label stays `NavItem.Label`). It said "a destination's icon and label" as nodes, while
  `navigation.baseline.txt` keeps `NavItem.Label` as the destination's name, drawn by the destination builder.
  Decision 5 rejects a node per item beside a builder (two places to draw one part), so #1611 should take the
  glyph as a node and leave the label to `destinationBuilder`.
