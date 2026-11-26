## Context

A modal `Overlay` is announced as a dialog named by `Overlay.Label`, a string: the web writes it as `aria-label` on the
layer's `role="dialog"` element (`LowerOverlay`, `lowerOverlay`), and Photon names the layer's stop with it
(`SemanticsVisitor.AnnounceOverlay`). The `Dialog` component hands its title string to that label. #1613 makes the
title a node, so the string is gone, and no word of the vocabulary lets a part of the tree name the layer it stands in.

The web already names one element by others without writing a string: `MergeSemantics` gives each part an id scoped by
its path (`LoweringPath.Scope`) and points `aria-labelledby` at them, so the browser computes the name and it follows
the content (`MergedReading`, `mergeReading`). Photon already derives a name from the text under a node, the name it
gives a control with no `Label` (`TextWithin`). The test harness reads `aria-labelledby` as the accessible-name rule
does (`AnnouncedTree`), so `SemanticsParityTests` can compare a named layer on both targets as it stands.

## Goals / Non-Goals

**Goals:**

- A dialog whose title is any node, a component included, is announced by that title's words on every target.
- The node is written once, read by every producer, and pinned on both targets by the parity guards.

**Non-Goals:**

- Changing `Dialog`. #1613 does that, on top of this.
- Naming a `Group` or a `LiveRegion` by a part. The node is general enough to grow there, and the family that first
  needs it widens the requirement.

## Decisions

### 1. A node that marks the part, as Flutter's `namesRoute` does

How Flutter solves it: `AlertDialog` wraps its title in `Semantics(namesRoute: true, container: true)`, and the route's
name is that title's semantics. The vocabulary takes the same shape: `TitlePart(child)` marks the part that names the
layer around it. It is a `SingleChildNode` that draws nothing and takes no geometry, layout-transparent like `Group`
and `MergeSemantics`, and the developer writes it where the title stands, inside the layer's own composition.

Rejected: a `VisualNode` property on `Overlay` (`Overlay.Title`). The title is drawn somewhere inside the layer's card,
among its other parts, and a property cannot say where. Rejected: reading a `Text` out of the title when it is one. It
is a guess that loses the name in silence the moment the title is a component, which is the case this exists for.

The name. AppKit calls the element that names a container its title element (`accessibilityTitleUIElement`), Compose
calls a pane's name its `paneTitle`, and Flutter's flag `namesRoute` names a route nobody here has. `TitlePart` says
what the node is in the words two native platforms use. Refused: `LabelledBy` (the web's attribute), `Title` (it would
collide with the `Title` member of every component that has one, inside which `new Title(…)` cannot bind), and
`Heading` (`Text.HeadingLevel` already says a heading, and a title need not be one).

### 2. It names the nearest dialog, and a layer starts a scope of its own

The layer a part names is the nearest one around it that is a dialog: a modal, open `Overlay`, or an anchored panel
whose role is `AnchorPanelRole.Dialog` (a date picker's calendar), which Photon lays out as a layer and announces as a
dialog too. Every `Overlay` and every anchored panel starts a scope, whether it is a dialog or not, because each is a
layer of its own on Photon: a part inside a toast laid over a dialog names neither, on either target. A part outside
every layer names nothing.

Several parts name the layer together, in reading order, as `aria-labelledby` reads a list of ids.

### 3. The part wins over `Label`

A layer with both a part and a `Label` is named by the part. That is the browser's own rule, `aria-labelledby` before
`aria-label`, so Photon follows it rather than the web being made to drop one attribute. `Label` stays the name of a
layer with no part, which is every layer written today.

### 4. The web names the layer by reference; Photon gathers the words

On the web the part lowers to the semantics host every wrapper of its kind uses (`SemanticsHost`, `semanticsHost`), with
no role and an id `eq-title-<scope>`, the path scope `MergedReading` uses. The nearest layer hands a list down the walk
(a field of `WebLoweringVisitor`, beside `_content`, and a member of the browser's `LoweringContext`), each part adds its
id to it, and the layer writes `aria-labelledby` from the list when it is a dialog. The name is the browser's to compute,
so it follows the title's text. A part lowered again alone in the browser keeps its path, so its id, and the layer's
reference stays good.

Photon's `AnnounceOverlay` looks for the parts under the layer's root, in tree order and without entering a layer opened
inside it, which Photon lays out apart anyway, and names the stop with the text under them, joined (`TextWithin`). The
part itself is no stop: what it holds is read where it stands, as it is on the web.

Email has no layer, and passes through the part.

## Risks / Trade-offs

- [A part that says nothing, its text all hidden from readers] → the layer is named by its `Label`, as if it had no
  part. That is the browser's rule too: the accessible-name computation takes an `aria-labelledby` only when the text
  it gathers is not empty, and goes on to `aria-label` otherwise. The test harness read an empty reference as an empty
  name, so it learns the same fall-through, and the parity suite holds a part beside a `Label` both when the part
  speaks and when it says nothing.
- [A part added only when a component inside the layer is lowered again alone in the browser] → the layer is not
  lowered again, so its reference misses the new part until the layer is. A dialog's title is part of the layer's own
  composition, built with it, so the case needs a part conditional on a nested component's state.

## Migration Plan

Nothing migrates. `Overlay.Label` keeps its meaning for a layer with no part.

## Open Questions

None.
