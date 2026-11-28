## 1. Style

- [x] 1.1 `style:` (a `NavigationTheme`) on `Tabs`, `AppBar`, `BottomNavigation`, `NavigationRail`, `Breadcrumb`, `Pagination` and `PageIndicator`, a constructor parameter and a `Style` property, merged where the component read its slot (`var slot = NavigationTheme.Merge(theme.Navigation, Style);`); `Pagination` hands the merged slot to `Number` and `Ellipsis` instead of their reading `theme.Navigation` again
- [x] 1.2 The factories in `UI.cs` mirror the widened constructors, and `Breadcrumb`, `Pagination` and `PageIndicator` get the factory they lack; the call sites that write `new` keep it (the two in the dashboard are README pictures, and nothing there is shadowed), and a static of those types is reached through its namespace where the surface is imported
- [x] 1.3 `AdoptConfig`: none of the seven is a `StatefulComponent`, so none carries one; the check below shows `adopt-config.baseline.txt` and `AdoptConfigCoverageTests` unchanged
- [x] 1.4 `./scripts/public-api.sh update`, and read the diff it writes into `PublicAPI.Unshipped.feat-navigation-takes-a-style-and-builders.txt`: each old constructor and factory `*REMOVED*`, each new one added
- [x] 1.5 Check: the family's seven `style` entries leave `navigation.baseline.txt` (`EQ_UPDATE_CATALOG_CUSTOMIZATION=1`), its `kept:` entries and `AppBar.Title` stay, and a theory over the seven components (one record, `NavigationTheme`) shows a member the instance sets win over the theme's on Photon (`tests/eQuantic.UI.Native.Engine.Tests`) and on the web (`tests/eQuantic.UI.Web.Tests`, the realized page), each case failing without the merge and, for `Pagination`, failing when the merge is made in `Build` but not handed to the page cells

## 2. Builders

- [x] 2.1 `tabBuilder` on `Tabs`, `destinationBuilder` on `BottomNavigation` and `NavigationRail`, `crumbBuilder` on `Breadcrumb`, each a `Func<int, VisualNode>` after `style`, drawn inside the part's chrome in a box that hands the content the part's `Foreground` and `Content` (role, type style, one line), the table of the design's "The part hands its builder's content the look it resolved"; the box is written only when a builder is given, so the tree with none is main's; the part's text stays its accessible name (`Pressable.Label`, `Link.Label`)
- [x] 2.2 Check: on Photon and on the web, a tab strip, a bottom bar, a rail and a breadcrumb with a builder show the builder's node in every part, keep the indicator, the pill and the separators, announce the item's text as the part's name, and call `onSelect(1)` when the second part is pressed; a builder answering `Text(label)` draws as the part with no builder; each test fails without the builder, and the colour and type test fails when the box hands no look

## 3. Both targets

- [x] 3.1 The transpiled twins and `UI.ts` regenerated (`EQ_UPDATE_TRANSPILED=1`); the conformance suite and the variant pins unchanged with no style and no builder; the component parity fixture unchanged
- [x] 3.2 Check: the Components, Web, Photon (Native.Engine) and Conformance suites pass, through `scripts/one-build-at-a-time.py`, and vitest (`dotnet build src/eQuantic.UI.Runtime -t:TestRuntime`)

## 4. Docs and archive

- [ ] 4.1 The wiki's Components page in every language it is written in (`Components.md` and `locale/<culture>/Components-<culture>.md` for pt-BR, es, fr and de), in one commit on a wiki branch named `feat/navigation-takes-a-style-and-builders`: the navigation family's record, the builders, and the three new factories
- [ ] 4.2 A ledger entry under `docs/ledger/`, citing #1610
- [ ] 4.3 `./scripts/check-openspec.sh` passes, then `openspec archive navigation-takes-a-style-and-builders --yes`
