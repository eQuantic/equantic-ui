## Why

Closes #677, a sub-issue of #190 (Flutter parity: the gaps that are work).

A `Select` takes one flat list of options, so an app whose design draws the choices under headings
("open", "next", "other") composes its own listbox and takes on its keyboard and its screen-reader
behaviour itself. The general capability is missing from the SDK, not from one app: **a Select cannot
group its options under labelled headings that a reader announces as groups**. Nothing in the
vocabulary can say "a labelled group that a reader names and then walks into" either, so the gap is
one word in the vocabulary and one parameter on the component.

## What Changes

- **New vocabulary node `Group`** (`eQuantic.UI.Primitives`): a layout-transparent wrapper with a
  `Label`, which every realizer announces as a labelled group and then walks into. The web writes
  `role="group"` with `aria-label`; Photon announces `SemanticRole.Group` (already appended, already
  bridged to AXGroup, UIKit's container and Android's group). Its factory is `Group(child, label)`.
- **New record `OptionGroup(string Label, IReadOnlyList<string> Options)`** (`eQuantic.UI.Components`),
  the data of one group, beside `MenuItem` and `NavItem`.
- **`Select` widens**: `options` becomes optional and a trailing `groups` parameter is added. The
  ungrouped options come first, then each group under its heading. The selected index and the index
  `onChanged` reports count every option in that reading order, across the groups, as the web's
  own `select.selectedIndex` counts across its `optgroup`s.
- **BREAKING (binary only, preview)**: the constructor and the factory signatures change.
  - Goes: `Select(IReadOnlyList<string> options, int selectedIndex = -1, Action<int>? onChanged = null, string? placeholder = null)`
    and the `UI.Select` factory of the same shape.
  - Replaces it: `Select(IReadOnlyList<string>? options = null, int selectedIndex = -1, Action<int>? onChanged = null, string? placeholder = null, IReadOnlyList<OptionGroup>? groups = null)`,
    mirrored by `UI.Select`.
  - Migration line: none in source. `Select(options, index, onChanged)` compiles unchanged; recompile
    against the new packages. A grouped Select writes `Select(groups: [new OptionGroup(label, names)], selectedIndex: i, onChanged: f)`.

What a developer writes, the issue's case under this design:

```csharp
Select(groups:
[
    new OptionGroup(Strings.Waitlist_Open, [.. open.Select(c => c.Name)]),
    new OptionGroup(Strings.Waitlist_Next, [.. next.Select(c => c.Name)]),
], selectedIndex: _chosen, onChanged: i => SetState(() => _chosen = i))
```

What the browser does differently: the open panel's `role="listbox"` holds one `role="group"` per
group, named by its label, with the heading drawn inside it and its options after it. The arrows walk
from the last option of one group to the first of the next, and `aria-activedescendant` names an
option inside the group it belongs to. What the native shell does differently: the menu draws a section
heading over each group's options, and VoiceOver and TalkBack hear the group's name as a group before
its options.

The issue's `SelectOption(c.Id, c.Name)` (a choice whose value is the app's, not its position) is
NOT part of this change: it is a separate capability that would reshape `Select` and, by the same
argument, `SegmentedControl`, `Tabs` and `RadioGroup`, and the library has no generic component to
build it on. It is left open (see design.md).

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `anchored-panels`: an anchored listbox's options may stand in labelled groups, numbered and walked
  across the groups as one list.
- `screen-reader-parity`: a labelled `Group` is named and then walked into, the same on both targets.

## Impact

Parts reached, in the order of the work:

- **Vocabulary** (`eQuantic.UI.Primitives`): `Nodes/Group.cs`, `Nodes/IVisualNodeVisitor.cs`
  (`Visit(Group, TState)`), `Elements/ChildSlots.cs`. `PublicAPI.Unshipped.txt` moves.
- **Components** (`eQuantic.UI.Components`): `OptionGroup.cs` (new), `Select.cs`, `UI.cs` (`Select`
  widened, `Group` added). `PublicAPI.Unshipped.txt` moves, with two `*REMOVED*` lines.
- **eqc**: no change. The transpiled twins `shared/components/Select.ts`, `OptionGroup.ts`, `UI.ts`
  and `ChildSlots.ts` are regenerated (`EQ_UPDATE_TRANSPILED=1`).
- **Web realizer and its TypeScript twin**: `WebLoweringVisitor.cs` and `.Interaction.cs` (`LowerGroup`,
  `Fills`, `CapsAt`) with `src/eQuantic.UI.Runtime/src/shared/lowering.ts`, `vocabulary.ts`, `nodes.ts`
  and `node-kinds.generated.ts` (`EQ_UPDATE_NODE_KINDS_TS=1`). The served runtime grows by one node;
  `ServedRuntimeBudgetTests` decides whether its budget moves.
- **Photon**: `Native.Framework/Layout/MeasureVisitor.cs`, `Native.Components/EmitVisitor.cs`,
  `Native.Components/SemanticsVisitor.Interaction.cs`. No shell changes: `SemanticRole.Group` is bridged
  on every platform already.
- **Email realizer**: `EmailWalk.cs` walks into a `Group`.
- **SDKs, templates, eqc, `appsettings`**: nothing. `developer-surface.baseline.txt` does not move.
- **Docs**: the wiki's component page for Select in every language it is written in, the Upgrading
  page (the binary break), one ledger entry under `docs/ledger/`. `docs/FLUTTER-PARITY.md` gains no
  row (design.md says why).

Rules of `docs/PRINCIPLES.md` followed: 2 (the capability, not the app's select), 4 (written once in
`Select`, realized by both), 5 (`Group` and `OptionGroup` pass the "would it exist without the web"
question), 6 (Flutter answered first), 9 (a node the realizers' exhaustive visitors must name, so a
realizer that forgets fails the build), 13 (the old constructor goes, nothing kept beside it). None is
bent.
