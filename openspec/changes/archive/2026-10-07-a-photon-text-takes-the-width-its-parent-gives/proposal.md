# Proposal

#659 (a Bug of the bug sweep, found while fixing #495): on Photon a `Text` sizes itself where Flutter's
takes the width its parent gives, and the web's does too since a Text lowers as a block.

## Why

Photon measured a Text at its own width in every parent. A Box's block stretch reached a Box, a Row
and a Column and never a Text, and a stretching Row or Column stretched every child but a Text. So a
single-line `Text` with `Align = TextAlignment.Center` in a sized Box, or in a Column that stretches
its children, sat at the start on Photon: its line centred inside a box as wide as itself. The same
tree centres on the web, where a block takes the width of a block parent and a flex item is
stretched, and in Flutter, where a tight constraint hands a Text its width and `textAlign` reads
across it.

## What Changes

- **A Text takes the width its parent decides**, where the parent decides one: a Box with a width, a
  Row or a Column that stretches its children on that axis, the wrapping pass, and the page itself
  (the base layer stretches its root's width, as a `body` does). Its lines align across that width.
- **Its height stays its lines'** inside a Box, as a block's does, and a stretching Row hands it the
  row's height as it hands any flex item, with the line at the top.
- **The inline fence holds**: a block stretch stops at a Pressable, a Link or an input, so a button
  in a sized Box still hugs its label.

## Parts reached and surfaces moved

Photon's measure pass alone: the Text's measurement and the flex pass's cross stretch. No public or
developer surface moves, and the web already lays out this way.

## Migration

- On Photon, a `Text` with a non-start alignment in a stretching parent now aligns across the
  parent's width rather than its own. A label that should hug sits in a Row, or names its width.
