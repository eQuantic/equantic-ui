# Proposal

Closes #976, the second half of #973 (E2), which is a sub-issue of #937 (the element tree, E1 to E5)
under the 1.0 epic #898. The design it follows is `docs/ELEMENT-TREE-PLAN.md`, section 3.7. The first
half, #975, put what a node keeps between frames on its element.

## Why

The Photon host still finds a control again by its layout path: the press, the focus, the field
being edited, the hover chain, every hit region and focus stop, the live regions and the shells'
accessibility ids. A path is not a control. When a page replaces another, or a key changes on a slot
that holds one child, the new node at that place inherits the old one's focus, its hover and even a
press in progress, so a release can run the handler of a button the user never pressed. And because
the host could not say which component input reached, every frame after input rebuilds every
component: a press on one card of a dashboard builds all twenty-four, measured at 94,190 bytes a
frame against 28,534 for the same screen at rest.

## What Changes

- Every position carries an id, an integer assigned when it mounts and kept while it lives, and a
  part a realizer draws as a target of its own (a linked run in a paragraph) gets one from the same
  count.
- The host addresses elements: a hit region, a hover region, a field, a code or spreadsheet surface,
  a canvas, a link and a focus stop carry the element of their node, and the press, the focus, the
  field being edited and the hover chain hold it. Focus within a subtree, and the scroll view that
  holds a focused control, are found by walking the element's parents. A control's focus, hover or
  press never passes to a node that takes its place, and a focus whose position leaves the tree is
  forgotten.
- What the live region announcer and the code surface's reveal remember lives on their elements.
- The shells name a node by its integer id: `SemanticNode.Id`, `PhotonHost.Activate(int)`,
  `PhotonHost.Adjust(int, int)` and `PhotonHost.FocusedId`.
- The path goes: from the laid-out node, the measure walk, the layout context's path cache, the
  realizer's layer names and the host.
- A frame after input builds what the input reached: the components a handler's `SetState` marked.
  A field's or a surface's handler that calls no `SetState` gets a frame that rebuilds every
  component, as before, since the host cannot see where the value lives. Work posted to the thread that draws builds what it marks, where it woke the host with an
  invalidation. An invalidation, a resize, a density change and a hot reload still rebuild every
  component.
- What a developer sees: nothing they write changes, and a frame shows what a `SetState` said, after
  input as after motion, which is the web's rule. On a native shell, focus and a press stay with the
  control they belong to.
- **BREAKING** (Primitives' host-only contract and the experimental native assemblies, none of it
  called in `samples/`): `SemanticNode.Path` becomes `SemanticNode.Id`; `PhotonHost.FocusedPath`,
  `ActivatePath` and `AdjustPath` become `FocusedId`, `Activate` and `Adjust`; `PhotonHost.Focus`
  takes the element; the regions and `FocusStop` carry an element in place of a path, and
  `ScrollRegion` loses the path it kept for this slice; `LinkRegion.Path` and
  `LiveAnnouncement.Path` become `Id`; `ShortcutBinding.Scope` is the element of the shortcut's
  subtree; `LayoutNode.Path` and `LayoutContext.PathCache` are gone, `LayoutEngine.Layout` loses its
  `rootPath` parameter, and `PhotonRealizer.Realize` takes elements and an id where it took paths. The migration line: a bridge names a node by
  `SemanticNode.Id` and activates it with `Activate(id)`, and a test that named a path reads the
  region's element.

## Capabilities

### New Capabilities

### Modified Capabilities
- `element-tree`: a frame after input builds what the input reached instead of every component, the
  host finds a control by its position, and a node's accessibility id is its position's.

## Impact

- `eQuantic.UI.Primitives`: `Element.Id` and `NodeElement.PartId`; `BuildOwner.Marks` counts every
  `SetState`; `SemanticNode.Id` replaces `Path`.
- `eQuantic.UI.Native.Framework`: the measure walk carries the element alone; `LayoutNode.Path`,
  `Position`, the path cache and the probe path go.
- `eQuantic.UI.Native.Components`: the regions, the focus stops, the live region marks, the press
  scope, the semantics walk, the text input session, the announcer and the host.
- The shells: macOS, iOS and Android name a semantic node by its id; Windows reads nothing that moved.
- Not reached: eqc, the runtime, the web realizer, the server, the SDKs, the templates, the developer
  surface (`developer-surface.baseline.txt`).
