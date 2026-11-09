- **2026-11-09 · A pressed fill is its surface's Pressed diff**: since #508 a pressed fill could be
  said two ways, `Pressable.PressedBackground` and the surface's `Pressed` diff, and the first had
  machinery of its own on each target: a custom property, a marker and two `!important` rules on the
  web, and on Photon a fill the first box drawn under the control took, wherever that box was. The
  components now say it the second way, on the box the handoff names, and the first way is gone
  with its machinery. The order the `!important` held comes from the selector alone, the pressed
  family's (0,4,0) over the focus's (0,3,0), the hover's (0,2,0) and the box's own (0,1,0), and a
  test reads that specificity and fails when the family weakens. The two targets also disagreed on
  WHERE three components filled: a pressed tab painted the 3dp strip under its label on Photon, and
  the navigation bar and the rail filled the whole item on the web, where the handoff draws the
  tab's cell and the pill; both targets now fill those, pinned by one test per target over the
  same eight components. The selected destination's pill takes no pressed fill, where Photon
  traded it for another colour while the finger stayed down, and neither does a selected list row.
  A control's press and focus now stop at a control nested in it, on both targets and in a
  `Simulated` picture: once the fill was a member of the diff, pressing a row filled every button
  in it, and on the web a pressed button had always pressed its row too. A break:
  `Pressable(box, …, pressedBackground: theme.Surface)` becomes `Pressable(box, …)` over a box
  whose style says `Pressed = new StyleDiff { Background = theme.Surface }`, and
  `PressedBackground = x` moves the same way
  ([#616](https://github.com/eQuantic/equantic-ui/issues/616)). Proposed and archived through
  OpenSpec, in `interaction-states`.
