## 1. The vocabulary

- [x] 1.1 `SortableTheme` (`LiftedOpacity`, `CopyOpacity`, `CopyElevation`, `MaxLean`) with its `Merge`;
  `IAppTheme.Sortable` (null), `OverriddenTheme` and `AppThemeExtensions.With(sortable:)`; the runtime's twin, its
  builder in the theme's JSON and the interface default.
- [x] 1.2 `SortPlacement`: `CopyElevation` (4) replaces the three `CopyShadow*` constants, and `Tilt` takes the most the
  copy leans; its twin regenerated.
- [x] 1.3 `Sortable(child, id, onDropped, announcements = null, style = null)` and `UI.Sortable(…, style:)`.
- [x] 1.4 Check: `ThemeSlotsTests` carries the new slot across the bridge; the factory guard passes; `Tilt` keeps to its
  most on both sides.

## 2. The web

- [x] 2.1 The C# and TS lowering of a sortable write the list's own look on its element, and nothing for a list
  with none.
- [x] 2.2 `dom/sortable.ts` resolves the look with `SortPlacement.Look` over the browser's theme when it lifts an item,
  and dims the item, styles the copy and leans it from it.
- [x] 2.3 Check: realizer tests read the list's own members and none for a list without; vitest carries a card under
  a theme's look and a list's own and reads the item's opacity, the copy's opacity, shadow and lean; the parity fixture
  lowers a styled list identically on both sides. Each fails without its change.

## 3. Photon

- [x] 3.1 The host resolves the look when it lifts an item and keeps it on `SortLiftState`; the emit walk dims the item
  and records the copy's shadow from it, and the host draws the copy at its opacity and leans it no further than its
  most.
- [x] 3.2 Check: `SortableHostTests` read the dimmed layer, the copy's layer and shadow and the lean for a theme's and a
  list's look; each fails without its change. The `PhotonDesktop` sample builds.

## 4. Records

- [x] 4.1 Public API in the branch's files; `docs/FLUTTER-PARITY.md`'s sortable row names the look; the ledger entry.
- [x] 4.2 The wiki's Sortable section and its theming page say how a theme and a list set the look, in five languages,
  on the branch's wiki branch.
- [x] 4.3 Check: the affected suites, the runtime's tests, the dashboard's board in a browser, `check-openspec.sh`.
