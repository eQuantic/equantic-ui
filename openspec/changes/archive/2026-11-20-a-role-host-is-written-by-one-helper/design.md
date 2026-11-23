# Design

## Context

The question is about the third tree, the render tree (docs/PRINCIPLES.md, rule 6): what box the
element that carries a node's semantics has. Flutter answers it once. `Semantics`, `MergeSemantics`
and `ExcludeSemantics` all build a `RenderProxyBox` (`RenderSemanticsAnnotations`,
`RenderMergeSemantics`, `RenderExcludeSemantics`), which takes exactly its child's size, and the rows
of docs/FLUTTER-PARITY.md that apply (`Semantics(liveRegion: true)`, `MergeSemantics`,
`ExcludeSemantics`) read SAME. Photon gives the same answer through `MeasureWrapper`. The web has no
proxy box, so each producer writes the DOM element that stands in for one, and that is the code this
change makes single.

The two producers are the C# `WebLoweringVisitor` (the server's first render, and the tree the parity
net compares) and the runtime's `lowering.ts` (the browser's render and hydration). Each one has to
write the same host, or hydration and the parity net disagree.

## The six wrappers today

| wrapper | C# (`src/eQuantic.UI.Web/`) | TypeScript (`src/eQuantic.UI.Runtime/src/shared/lowering.ts`) | host written |
|---|---|---|---|
| `Progress` | `LowerProgress`, `WebLoweringVisitor.Interaction.cs` | `lowerProgress` | by hand |
| `Adjustable` | `LowerAdjustable`, `WebLoweringVisitor.Interaction.cs` | `lowerAdjustable` | by hand |
| `LiveRegion` | `LowerLiveRegion`, `WebLoweringVisitor.Interaction.cs` | `lowerLiveRegion` | by hand |
| `Group` | `LowerGroup`, `WebLoweringVisitor.Interaction.cs` | `lowerGroup` | by hand |
| `MergeSemantics` | `LowerMergeSemantics`, `WebLoweringVisitor.Semantics.cs` | `lowerMergeSemantics` | `SemanticsHost` / `semanticsHost` |
| `ExcludeSemantics` | `LowerExcludeSemantics`, `WebLoweringVisitor.Semantics.cs` | `lowerExcludeSemantics` | `SemanticsHost` / `semanticsHost` |

Every one writes, on both producers, a `div` whose style is `Width = fills.Width ? "100%" :
"fit-content"`, `MaxWidth = Size(CapsAt(child))` and `Height = fills.Height ? "100%" : null` (TS:
`width`, `max-width: sizeValue(capsAt(child))`, `height`), then its attributes, then its child,
lowered with no inherited axis at `LoweringPath.Child` (TS: `path + '/0'`).

`SheetSurface` writes the same width contract and is not one of the six: it bears no role since #1081,
its child is lowered WITH the inherited axis, and its host carries a class, a position and pointer
handlers of its own. It keeps its own host.

## What differs, and only that

Read on both producers, the six hosts differ in exactly these:

| wrapper | attributes the host starts with, in order | then | style beyond the width contract |
|---|---|---|---|
| `Progress` | `role=progressbar` | `aria-label` when named | none |
| `Adjustable` | `role=<derived>`, `tabindex=0` | `aria-label` when named | `pointer-events: auto` |
| `LiveRegion` | `role=alert\|status`, `aria-live=assertive\|polite`, `aria-atomic=true` | `aria-label` when named | none |
| `Group` | `role=group` | `aria-label` when named | none |
| `MergeSemantics` | none | nothing | none |
| `ExcludeSemantics` | none | nothing | none |

The name is written in the same place by all four that have one, right after the fixed attributes, and
by the same test (C# `Label is { Length: > 0 }`, TS truthy `label`). Everything else each wrapper
writes comes after that and differs per wrapper: the value and value text (`Progress`, `Adjustable`),
the orientation and the key handler (`Adjustable`), the merged reading (`MergeSemantics`),
`aria-hidden` (`ExcludeSemantics`). Those stay in the wrapper.

So the helper's inputs are the child, the attributes the role starts with, the name, and whether the
host takes the pointer. Nothing else differs.

## Decision

Widen the existing helper on each producer, in place, and send the four hand-written copies through it.

C#, in `WebLoweringVisitor.Semantics.cs`:

```csharp
private RealizedElement SemanticsHost(
    VisualNode child, Dictionary<string, string> attributes, string? label, bool takesPointer)
```

It builds the `div` with the width contract, `PointerEvents = takesPointer ? "auto" : null`,
`RawAttributes = attributes`, then writes `aria-label` when `label` is non-empty, and returns the host.
It does not lower the child.

TypeScript, in `lowering.ts`:

```ts
function semanticsHost(
  child: VisualNodeValue, attributes: Record<string, string>, label: string | undefined, takesPointer: boolean,
): HtmlNode
```

It calls `element('div', { width, 'max-width', 'pointer-events': takesPointer ? 'auto' : undefined,
height })`, in that key order, then copies `attributes` onto `host.attributes` in their order, then
`aria-label` when `label` is truthy, and returns the host.

The callers:

- `LowerProgress` / `lowerProgress`: `SemanticsHost(progress.Child, { role: progressbar }, progress.Label, false)`.
- `LowerAdjustable` / `lowerAdjustable`: `{ role, tabindex: 0 }`, `adjustable.Label`, `true`.
- `LowerLiveRegion` / `lowerLiveRegion`: `{ role, aria-live, aria-atomic }`, `live.Label`, `false`.
- `LowerGroup` / `lowerGroup`: `{ role: group }`, `group.Label`, `false`.
- `LowerMergeSemantics`, `LowerExcludeSemantics` and twins: an empty map, no name, `false`.

Each keeps lowering its own child and adding it, as all six do today. The comments that each wrapper
carries about the width contract ("the fifth wrapper…", "Same rule as LowerProgress…", "BOTH axes…")
move to the helper's summary, once per producer, citing #239, #241 and the bounds suites; a wrapper's
summary says only what its host adds.

### Why the output cannot move

- **C# style.** `HtmlStyle` writes its properties in its own fixed order (`AddProperty` in
  `Dom/HtmlStyle.cs`), not in the initializer's, and skips a null one. A host without the pointer gets
  `PointerEvents = null`, the property it has today. Read, not measured.
- **C# attributes.** The caller's dictionary is created with the same initializer it has today and
  `aria-label` is added next, so the same `Add` calls run in the same order. The change relies on no
  ordering of `Dictionary` beyond what today's code already relies on.
- **TS style.** `atomizeEntries` (`style-atomizer.ts`) skips an `undefined` value and sorts the class
  names, and these hosts carry no custom property, so the class string is the same whatever the key
  order. The key order is kept anyway (width, max-width, pointer-events, height, Adjustable's today),
  so the order in which `ensureRule` first registers a rule does not move either.
- **TS attributes.** `element` writes `class` first; the role's attributes and then `aria-label` are
  assigned after it, in the order each wrapper assigns them today.

## Alternatives

- **A new `RoleHost` beside `SemanticsHost`.** Rejected: two helpers for one rule is the duplication
  this change removes, and two of the six (`MergeSemantics`, `ExcludeSemantics`) bear no role, so the
  name would be wrong for a third of its callers. `Semantics` is Flutter's word for the proxy, and the
  name both producers already use.
- **The helper lowers and appends the child too.** Rejected: `MergeSemantics` reads the lowered child
  after appending it, so the helper would hand the child back or the merge would dig it out of
  `Children[0]`. The issue's shape, the role's attributes in and the host out, is the smaller one.
- **An open style bag in place of `takesPointer`.** Rejected: one wrapper differs, by one declaration
  with one meaning. A bag would invite the next wrapper's style into the one host the rule is about.
- **`SheetSurface` through the helper as well.** Rejected: it bears no role, lowers its child with the
  inherited axis, and adds a class, a position, an outline and pointer handlers, so the helper would
  grow inputs for one caller that is not a semantics host.

## Risks

- **A wrong helper the suites do not see.** `RoleBearingHostBoundsTests` and `role-host-bounds.spec.ts`
  assert `width: fit-content` and `width: 100%` only. A helper that drops `max-width` or `height` passes
  both. Task 1.3 finds which net catches each mutation, and adds the assertion to both suites where
  none does.
- **Hydration.** The browser adopts the server's markup only when the two producers agree, so the
  proof runs the parity net on both sides and a sample in a browser, not only the unit suites.

## Open questions

None for the engineer. Reported as not this issue, in the report:

- `Navigable` bears a role (`grid`, `tree`) and is a tab stop, and its host has no width contract on
  either producer: a block `div` whose focus ring and outline span the container. The two producers
  agree, so it is not a divergence, but it is the case the rule was written for.
- The bounds suites do not assert the cap or the height (above), and have no rows for
  `MergeSemantics` and `ExcludeSemantics` (task 1.2 adds them).
