## Context

Every nameable of the vocabulary takes its name as a string today:
- a modal `Overlay` is announced as a dialog named by `Overlay.Label`, which the web writes as `aria-label` on the
  layer's `role="dialog"` element (`LowerOverlay`, `lowerOverlay`), and Photon gives the layer's stop
  (`SemanticsVisitor.AnnounceOverlay`);
- a `Group` and a `LiveRegion` are named by their `Label`;
- a `TextEntry` is named by its `Label` and described by its `Description`, which the web writes as a hidden twin
  inside the entry and Photon has not carried at all.

The family slices of #1545 make the content these strings came from a node (#1613's dialog title, #1609's field title
and helper, #1607's group heading), so the strings are gone, and no word of the vocabulary lets a part of the tree name
or describe what it stands beside.

The web already names one element by others without writing a string: `MergeSemantics` gives each part an id scoped by
its path (`LoweringPath.Scope`) and points `aria-labelledby` at them, so the browser computes the name and it follows
the content (`MergedReading`, `mergeReading`). Photon already derives a name from the text under a node, the name it
gives a control with no `Label` (`TextWithin`). The test harness reads `aria-labelledby` as the accessible-name rule
does (`AnnouncedTree`), so `SemanticsParityTests` can compare a named stop on both targets as it stands.

## Goals / Non-Goals

**Goals:**

- A dialog, a group, a region or a field whose title, label or helper is any node, a component included, is named and
  described by that node's words on every target.
- One rule, written once per producer and held to the same answer by the parity guards.

**Non-Goals:**

- Changing `Dialog`, the fields or `RadioGroup`. #1613, #1609 and #1607 do that, on top of this.
- Naming a button by a part. A button's content is inside it, and both targets name it by that content already.

## Decisions

### 1. Two nodes that mark the part, as Flutter's `namesRoute` does

How Flutter solves it: `AlertDialog` wraps its title in `Semantics(namesRoute: true, container: true)`, and the route's
name is that title's semantics. `InputDecorator` merges the label and the helper into the field's semantics node. The
vocabulary takes the first shape and widens it to the second: `LabelPart(child)` marks the part that names its nearest
nameable, and `DescriptionPart(child)` the part that describes it. Each is a `SingleChildNode` that draws nothing and
takes no geometry, layout-transparent like `Group` and `MergeSemantics`, and the developer writes it where the title or
the helper stands, inside the composition.

Rejected: a `VisualNode` property on each nameable (`Overlay.Title`, `TextEntry.LabelNode`). The title is drawn
somewhere among the nameable's other parts, or beside a field rather than inside it, and a property cannot say where.
Rejected: reading a `Text` out of the title when it is one. It is a guess that loses the name in silence the moment the
title is a component, which is the case this exists for.

The names. `Label` is this vocabulary's word for an accessible name (`Pressable.Label`, `Overlay.Label`), and
`Description` its word for what is read after it (`TextEntry.Description`), so the parts are named by what they supply.
The first version of this change called the naming part `TitlePart`, when it named dialogs alone. It was renamed when
fields and groups joined, since a field's label is no title.

### 2. A part reaches the nearest nameable, or the one control a node holds

Walking up from the part, it reaches the first ancestor that:
- IS a nameable container: a modal, open `Overlay`, an anchored panel whose role is a dialog, a `Group`, a
  `LiveRegion`;
- or HOLDS one nameable control (a `TextEntry`, or a checkbox, radio or switch `Pressable`) outside the part, which no
  nearer part already reached and which carries no label of its own.

So the parts beside a field in a `Column` name and describe that field, and a dialog's title, beside a field the dialog
holds, names the dialog once the field is named by its own label or its own part. A field that carries its own label is
claimed from the start: it names itself, and a title beside it is no field's.

Every `Overlay` and every anchored panel is a scope: a part inside a layer that is no dialog names nothing, and a
part outside every nameable names nothing. Several parts name or describe one nameable together, in reading order.

An ancestor that holds TWO unclaimed candidate controls refuses rather than guessing, with one sentence on every target,
telling the developer to give each field its own `Column` or `Group`. A form composed by hand is then a build-time
answer, not a field named by the wrong label.

### 3. The part wins over the nameable's string

A nameable with both a part and a string is named by the part. That is the browser's own rule, `aria-labelledby` before
`aria-label`, so Photon follows it rather than the web being made to drop one attribute. The string stays the name of a
nameable whose parts say nothing, which is every nameable written today. A description part is read after the
nameable's own description, as the web lists the part's id after the twin's.

### 4. The rule is settled as the walk unwinds, the same way on all three producers

Each producer keeps two lists in reading order, the parts that reached nothing yet and the nameable controls it met,
and each node takes a mark of both before its subtree:
- a part adds itself, and the controls inside it are no candidates;
- a container reaches every part past its mark;
- a control reaches the parts inside it, and becomes a candidate;
- a layer that is no dialog drops both lists past its mark;
- any other node hands its parts to the one unclaimed candidate past its mark, refuses two, and otherwise leaves both
  to the node above.

The web's C# lowering (`NamingParts`) and the browser's (`naming-parts.ts`) settle it in the one funnel every node
lowers through, and Photon's `NamedByParts` settles it over the laid-out frame, before a stop is announced: the page
first, each anchored panel where its anchor stands, then each layer under a root of its own, as the semantics walk reads
them.

On the web a part lowers to the semantics host every wrapper of its kind uses (`SemanticsHost`, `semanticsHost`), with
no role. It takes an id, `eq-label-<scope>` or `eq-desc-<scope>`, scoped by its path, only when it reaches a nameable,
so a part that names nothing writes nothing, and the nameable lists the id in `aria-labelledby` or `aria-describedby`.
On Photon the nameable's stop is named by the text gathered under its parts (`TextWithin`), and described by it in
`SemanticNode.Description`, which AppKit reads as the element's help, UIKit as its hint and Android as its node's hint
text (#1920 carries it to the bridges). Email has no nameable, and passes through both parts.

### 5. A component lowered again alone keeps its parts' ids

The browser lowers a component again alone when only it changed (`relowerBuilt`, #1455). A part inside it whose
nameable lies outside reaches nothing in that walk, and would lose its id while the nameable, lowered before, still
lists it. Such a walk gives every part left waiting its id anyway (`inPartsWalk(…, alone)`): the id is scoped by the
part's path, which the record keeps, so it is the id the nameable lists. A dialog whose title is a component of the
app's own keeps its name when the title draws again.

## Risks / Trade-offs

- [A part that says nothing, its text all hidden from readers] → the nameable is named by its string, as if it had no
  part. That is the browser's rule too: the accessible-name computation takes an `aria-labelledby` only when the text
  it gathers is not empty, and goes on to `aria-label` otherwise. The test harness learns the same fall-through, and the
  parity suite holds a part beside a `Label` both when the part speaks and when it says nothing.
- [A part added only when a component inside the nameable is lowered again alone] → the nameable is not lowered again,
  so its reference misses the new part until it is. A title is part of the nameable's own composition, built with it,
  so the case needs a part conditional on a nested component's state.
- [A form composed by hand with two fields in one `Column` and one label] → refused at the realizer, naming the node
  and the count. The family components (#1609) give each field its own column, so they never meet it.

## Migration Plan

Nothing migrates. Every nameable keeps its string's meaning when no part reaches it.

## Open Questions

None.
