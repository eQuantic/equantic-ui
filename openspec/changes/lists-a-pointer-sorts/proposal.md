## Why

Closes #1535, a user story of #1534 (sortable lists and drag and drop across containers), under the epic
#1510 (the components eQuantic Space needs).

eQuantic Space moves its board, its list and its tree to eQuantic.UI, and all three put things in order by
dragging them: a card inside a column and across columns, a row of a list, a column of a header. The SDK's
`Draggable` is one surface following a finger along one axis, and its `DropTarget` takes files only, so none
of it can be written in C# today. **Nothing in the vocabulary can say "these items are put in order by drag,
and these lists take each other's items".**

## What Changes

- **Vocabulary** (`eQuantic.UI.Primitives`), all added:
  - `Sortable(child, id, onDropped)`: a container whose `SortableItem`s are put in order by drag along its
    `Axis`. Sortables of one `Group` take each other's items, an empty one included. On a drop it reports a
    `SortDrop`: the item's key, the container it left, the one it lands in and its index there. While a drag
    is over it, `OnHovered` reports where the item would land (null when it leaves), so the component draws its
    own indicator from the theme; `Accepts` is the app's guard, asked before a place is offered. A container
    that `Nests` also takes an item INTO one of its items, which `SortDrop.Into` names (the tree's nesting).
  - `SortableItem(child, key)`: an item a pointer picks up. A press that does not travel is still a press.
  - `SortDrop(Item, From, To, Index)` with `Into`.
- **Web realizer**: the client does the physics, as it does a `Draggable`'s. A mouse picks an item up past
  5 px; a finger after holding it still for 250 ms, so a list still scrolls under a swipe. A copy of the item
  follows the pointer, the item itself is dimmed, the scroller under the pointer scrolls near its edge, Escape
  puts the item back, and the click a drag would end in is swallowed. The server declares the events and runs
  none.
- **Photon**: the nodes are layout-transparent and take no drag yet, a fence #1537 lifts. The keyboard's way to
  sort (#1536) is the next change.
- **Sample**: the dashboard gains a board whose cards move inside and across columns, with an indicator where
  a card would land.

What a developer writes, a board's column:

```csharp
Sortable(
    Column(gap: Space.S2, children: [.. cards.Select(card => SortableItem(Card(card), card.Id))]),
    id: column.Id,
    onDropped: drop => SetState(() => Board.Move(drop.Item, drop.To, drop.Index)),
    group: "board",
    onHovered: drop => SetState(() => _landing = drop))
```

## Capabilities

### New Capabilities

- `sortable-lists`: what `Sortable` and `SortableItem` report, how a pointer picks an item up and where it
  lands, and the guard.

### Modified Capabilities

None.

## Impact

- **Vocabulary**: `Nodes/Sortable.cs`, `Nodes/SortableItem.cs`, `Nodes/SortDrop.cs`, `IVisualNodeVisitor`
  (two visits), `Elements/ChildSlots.cs`. `PublicAPI.Unshipped.txt` moves, additions only.
- **Components**: `UI.cs` (the `Sortable` and `SortableItem` factories). No component of its own: a list, a
  board and a tree build on the nodes.
- **Web realizer and its TypeScript twin**: `WebLoweringVisitor` (two lowerings), `lowering.ts`, `nodes.ts`,
  `vocabulary.ts`, `value-types.ts`, `node-kinds.generated.ts`, a new `dom/sortable.ts` controller. The served
  runtime grows; `ServedRuntimeBudgetTests` decides whether its budget moves.
- **Photon**: `MeasureVisitor`, `EmitVisitor`, `SemanticsVisitor`: wrappers that pass their child through.
- **Email realizer**: walks into the child, a list drawn as it stands.
- **eqc, SDKs, templates, `appsettings`**: nothing.
- **Docs**: the wiki's Components page in every language, a row in `docs/FLUTTER-PARITY.md`, a ledger entry.

No break. Rules of `docs/PRINCIPLES.md` followed: 2 (lists for every app, the Space's board, list and tree
among them), 4 (written once, realized by the web now and by Photon in #1537, said so), 5 (`Sortable` is
Flutter's and the platforms' word for it, not the DOM's), 6 (Flutter's `ReorderableListView`, `Draggable` and
`DragTarget` answered first), 14. Bent: 4, for one story: Photon renders the items and takes no drag until
#1537, as `DropTarget` did before its shells.
