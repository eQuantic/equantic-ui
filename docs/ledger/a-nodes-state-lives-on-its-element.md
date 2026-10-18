- **2026-10-18 · What a Photon node keeps lives on its element**: the first half of E2 of the
  element tree (docs/ELEMENT-TREE-PLAN.md). A scroll view's offset, a drag, an `InView`'s last report,
  a `Presence`'s entrance and a transition's tracks moved from five stores keyed by layout path onto
  the node's element (`NodeElement.State`), and leave the tree with it, so a node drawn at the place of
  another starts with its own: a page that replaces another no longer opens its list scrolled where the
  old page's was, a box mounts at its width instead of gliding from the old one's, and a `Presence`
  replaced at its place fades out while the new one enters, as on the web. A presence hands its last
  paint to the host's clock when its element leaves, and the realizer ends the frame before it draws
  the exits, so a layer that closes fades from the frame it closed in. What was left of each store is
  the host's clock for it (`ScrollClock`, `DragClock`, `TransitionClock`, `PresenceClock`), and
  `InViewStore` is gone: an `InView` reports in a tree that is drawn again. A transition's tracks are
  found by a value now, not by strings formed every frame, and a new scene of gliding cards went from
  73,669 to 39,133 bytes a frame ([#975](https://github.com/eQuantic/equantic-ui/issues/975)).
  Proposed and archived through OpenSpec (`openspec/specs/element-tree`).
