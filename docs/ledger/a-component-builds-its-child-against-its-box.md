- **2026-11-25 · A component builds its child against the box its parent gives it**: the vocabulary
  gains `BoxConstraints`, the room a box may take (Flutter's value by name and shape), and
  `LayoutBuilder`, a node whose child is what its builder answers for the constraints its own box has;
  `eQuantic.UI.Components` gains `ConstrainedBox`, which applies constraints an author holds. Until now
  the window's size class was all the vocabulary answered, so a card three columns wide in a wide window
  was as narrow as a phone and nothing could say so. On Photon the builder runs in the layout pass and
  again only when the room or the node changes; on the web the server writes the box empty and the
  browser builds the child after hydration, so nothing under a builder gets server data and a decision
  that must render on the server stays with `AdaptiveNode`. Email refuses a `LayoutBuilder`. Everything
  is added and nothing breaks, so there is no Upgrading entry
  ([#1555](https://github.com/eQuantic/equantic-ui/issues/1555)). Proposed and archived through
  OpenSpec (`openspec/specs/layout`).
