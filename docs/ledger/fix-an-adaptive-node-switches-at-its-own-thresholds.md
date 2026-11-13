- **2026-11-11 · An adaptive node switches at its own thresholds on every target**: the web gated an
  `AdaptiveNode`'s arms with the node's own `MediumFrom` and `ExpandedFrom`, while Photon's layout and the
  element tree resolved them by the window's size class, at the spec's 600 and 840 dp, so the dashboard's
  payments page (`ExpandedFrom = 1024`) drew its grid on Photon between 840 and 1024 dp where the web
  stacked it, and an arm the web showed could be one no element held. The element tree's owner is now
  handed the window's width and Photon's layout reads it, both resolving the arm with `ResolveWidth`;
  `AdaptiveNode.Resolve(WindowSizeClass)` and `LayoutContext.SizeClass` are gone. A test holds the element
  tree, Photon and the web's gates to the same arm at 900 dp and at the six widths #735 measured, and five
  of the seven fail with the size-class rule put back
  ([#1465](https://github.com/eQuantic/equantic-ui/issues/1465),
  [#735](https://github.com/eQuantic/equantic-ui/issues/735)). Proposed and archived through OpenSpec
  (`openspec/specs/adaptive-layout`).
