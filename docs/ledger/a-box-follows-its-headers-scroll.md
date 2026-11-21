- **2026-11-20 · A box follows its header's scroll**: `BoxStyle.Scrolled` is what a box inside a `Pinned`
  header looks like while that header is scrolled, so a header swaps what it shows as the page scrolls (a large
  badge giving way to a small mark with the brand's name) with its boxes' own transitions and nothing rebuilt.
  The nearest header decides, and a control's hover, focus and press are laid over the diff. On the web a rule
  selects on the header's `data-eq-scrolled`, weighing between a box's resting class and its hover; on Photon the
  header carries its state to its subtree. The dashboard's `/header` page is where a browser reads it
  ([#1769](https://github.com/eQuantic/equantic-ui/issues/1769)).
