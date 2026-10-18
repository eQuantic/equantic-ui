- **2026-10-18 · The Photon host finds a control by its position**: the second half of E2 of the
  element tree (docs/ELEMENT-TREE-PLAN.md). The press, the keyboard focus, the field being edited and
  the hover chain follow the element of the control they belong to, where they followed its layout
  path, and a path is a place, not a control: a page that replaced another under the focus handed it
  to the new page's button, and a key that changed on a slot holding one child left a press on the
  newcomer, so a release ran a handler nobody pressed. A focus whose position leaves the tree is
  forgotten, as a browser's is. Every position takes an integer id when it mounts (`Element.Id`), a
  linked run takes one its paragraph's position keeps (`NodeElement.PartId`), and the macOS, iOS and
  Android bridges name a node by it (`SemanticNode.Id`, `PhotonHost.Activate`, `Adjust`, `FocusedId`).
  The path is gone from the layout, the realizer and the host. A frame after input builds what its
  handlers marked with `SetState`, where it rebuilt every component: a press on one card of a dashboard
  went from 94,190 to 31,246 bytes a frame. A field's or a surface's handler that calls no `SetState`
  still gets a frame that rebuilds everything, so a key always shows. Work posted to the thread that
  draws no longer rebuilds every component either
  ([#976](https://github.com/eQuantic/equantic-ui/issues/976)). Proposed and archived through OpenSpec
  (`openspec/specs/element-tree`).
