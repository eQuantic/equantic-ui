## Why

Closes #1602, a task of #1534 (sortable lists and drag and drop), under the epic #1510 (the components eQuantic Space
needs). The look of an item carried in a sortable list is constants of `SortPlacement` that both targets read: the
item dimmed to 0.4 in its place, the copy that follows the pointer at 0.92 under a black shadow 12 px down and 28 px
blurred at 0.28, and the copy leaning up to 12° with the pointer's speed. A theme cannot change it, and neither can an
app for one list. Decided with the roadmap session on #1545 (2026-11-15): the lift's look is a slot record of the
theme, with a list's own laid over it.

## What Changes

- **A slot for the lift.** `SortableTheme` is a new slot record of `IAppTheme` (`Sortable`, null by default): the
  opacity of the item in its place while carried (`LiftedOpacity`), the copy's opacity (`CopyOpacity`), the copy's
  shadow as a level of the theme's elevation scale (`CopyElevation`), and the most the copy leans, in degrees
  (`MaxLean`, 0 for a copy that stays upright). It merges as every slot does, `IAppTheme.With(sortable:)` lays one over a
  theme, and it crosses to the browser in the theme's JSON.
- **A list's own look.** `Sortable` takes `style`, a `SortableTheme` laid over the theme's, member by member; so does
  `UI.Sortable(…, style:)`. A list that gives none looks as its theme says.
- **The defaults.** What neither says is 0.4, 0.92, the theme's elevation level 4 and 12°. The shadow is the
  theme's level 4 rather than a black one: the same offset and blur, the theme's own colour in light and in dark.
- **The web.** The lowering writes a list's own look on its element, and the controller lays it over the browser's
  theme when it lifts an item from that list. **Photon** resolves the same look when it lifts the item and draws the
  dimmed item, the copy and its lean from it.

In C#:

```csharp
ui.UseTheme(PhotonTheme.Instance.With(sortable: new SortableTheme { CopyElevation = 5, MaxLean = 6 }));

Sortable(column, id: "todo", onDropped: Move, style: new SortableTheme { LiftedOpacity = 0.25f, MaxLean = 0 })
```

Parts reached: the vocabulary (`SortableTheme`, `IAppTheme.Sortable`, `AppThemeExtensions.With`, `Sortable.Style`,
`SortPlacement`'s defaults), the components (`UI.Sortable`), the web realizer (C# and TS lowering of a sortable), the
runtime's theme twin and sortable controller, and the Photon host and emit walk. The public surface grows;
`Sortable`'s constructor, `UI.Sortable` and `IAppTheme.With` widen with an optional parameter; `SortPlacement`'s three
`CopyShadow*` constants go, replaced by `CopyElevation`, and `SortPlacement.Tilt` takes the most the copy leans. The
developer surface does not move.

Principles followed: 1 (the look is C#, a record a theme or a list sets), 4 (written once: the defaults and the lean's
arithmetic stay in the transpiled `SortPlacement`, both realizers resolving the same record), 13 (signatures widen and
the constants they replace go), 15 (a look a node needs is a member of its slot record, which a theme sets for every
list and a list for itself).
