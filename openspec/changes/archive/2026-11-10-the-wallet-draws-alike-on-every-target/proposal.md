# Proposal

Closes #1342 and #1343, Bugs under #166.

## Why

The Wallet sample, recorded on four targets, drew the same tree three ways.

- On the web, the cards page stood at its content's width (481px of 1100): its title bar's action sat in the middle of the window and its empty state off-centre. The rail of the wide layout stopped at its last destination and stood centred in the window. Photon draws both edge to edge.
- On Android and macOS, the "Dispute" action of a `SwipeableRow` was a 40dp strip at the top of a 78dp row. The web fills the row.

Both come from a wrapper or a stack deciding a child's size before it can know it.

- The web lowered a `SafeArea` to a padded `div` that hugged its content, where Photon measures it as its child plus the insets. The readers that decide whether a node fills kept their own lists of wrappers: `Fills` on the web, its TypeScript twin and `FillsOf` on Photon. All three had left `SafeArea` out, although the vocabulary already states which wrappers are layout-transparent (`IsLayoutTransparent`).
- Photon measured a `Stack`'s positioned children beside its flow children, before the stack had a size. A filling child anchored top and bottom took the room it was offered and stood at its content's height.

## What Changes

- A `SafeArea` on the web takes the fill and the width cap its child lowered to, on each axis, with the insets inside (`box-sizing: border-box`), in the C# lowering and its TypeScript twin. It reads them off what the child lowered to, because the child is a component as often as not. Around an `AdaptiveNode` it reads every arm and fills an axis every arm fills, since the page's media rules show one arm at a time.
- `Fills` on the web and `FillsOf` on Photon look through every wrapper `IsLayoutTransparent` names. The TypeScript `fills` lists the same kinds, and a test fails when it leaves one out.
- Photon measures a `Stack`'s flow children first, resolves the stack's size, and then measures each positioned child in the room its anchors leave in that size. An axis the child spans is decided by the stack, whatever the parent left open.
- An app writes nothing new: the same C# draws alike on every target. No public surface and no developer surface moves.

## Impact

The web realizer, the browser runtime's lowering and Photon's layout engine. The Wallet sample is the proof, on the web, macOS and Android. #1344 records what proving it on Android found: Photon antialiases two layers that share an edge on a fractional pixel, so a closed row shows a one-pixel line of the action behind it.
