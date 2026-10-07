## Why

Photon takes the pointer where it laid a control out, not where it draws it, in five ways the
web never does. Closes #630 and #635 (sub-issues of #194, pointer, gestures and focus), #513 (of
#504, the state diff's transform), #629 (of #203, style semantics) and #285 (of #160, instruments
that fail).

- #630: every target grows evenly around its box (§08 under a finger, WCAG's floor under a
  pointer), and the host gave a press to the topmost region that held it. In a list of rows shorter
  than the minimum, the row drawn after took its neighbours' presses: under a finger a press in the
  middle of a 20dp row ran the row below it.
- #513: a transformed box (a hover lift, a press, a static transform) drew where its transform put
  it and registered every region at its layout rect.
- #635: a text field and a spreadsheet that ran past their scroll view took the presses aimed at
  whatever stands outside it, as a code surface did until #297.
- #629: a bordered box laid its child over its border, where the web insets it, and a hugging box
  came out two borders smaller than its web twin.
- #285: the stand-in text measurer gave a lone space zero width, so every gap of a rich paragraph
  cost nothing, and the goldens and layout tests measured rich text narrower than any shell draws.

## What Changes

For a developer, an app behaves on Photon as it does on the web, with no line of their code
changed:

- A press inside a control's own box is that control's, before any neighbour's slop. A slop still
  answers where no box beside it is drawn, and in front of the box it stands in (an icon button's
  target over its card).
- A transformed box takes the pointer where it is drawn: a press, a hover, the cursor, a drag and a
  scroll. A rotated box is tested against its shape. A pressable that wraps a moved box follows it.
- A text field and a sheet take presses only on the part of them on screen.
- A box keeps its child inside its border on the sides the border is drawn on, and a hugging box
  counts the border in its size.
- The stand-in measurer charges a space its advance, so a rich paragraph measures as its plain twin.

The parts it reaches: the Photon realizer and host (`eQuantic.UI.Native.Components`), Photon's
layout and the stand-in measurer (`eQuantic.UI.Native.Framework`). Nothing in eqc, the runtime or
the web realizer moves.

The public surface moves, in preview: `HitRegion` gains `Drawn` (the box its pressable is drawn in),
`TextRegion` and `SheetRegion` gain `Visible`, and `PhotonRealizer.Realize` gains `sizedLike` (the
frame before, which sizes the frame's region lists). A host that builds these regions itself adds
the argument. The developer surface does not move.

## Capabilities

### New Capabilities

- `box-layout`: what a box keeps between its edge and its child.
- `text-measurement`: what the stand-in measurer charges for the text it measures.

### Modified Capabilities

- `hit-targets`: a neighbour's slop, a transformed box, and an editing surface past its view.

## Impact

Photon only. Goldens move where a bordered box or a rich paragraph is drawn, and each one is read
and regenerated in this change with what moved. The pooled steady frame allocates less than it did
(71.2 KB from 73.2), and its ceiling comes down to 72 KB.
