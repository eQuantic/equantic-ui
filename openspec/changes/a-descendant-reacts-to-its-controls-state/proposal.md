# Proposal

Closes #614, the third slice of Feature #504 (StyleDiff gains transform and shadow, a pressed state,
and a group hover).

## Why

A box's `Hover`, `Focus` and `Pressed` say what the box itself looks like. The equantic.tech handoff
and #498 ask for something else: what a thing INSIDE a control looks like while the control is in a
state. The primary button's sheen band crosses it on hover and its trailing arrow nudges 2px; a link
or a row brightens its label and its arrow under the pointer. Today a descendant can react only to
the pointer over ITSELF, a `Text` or an `Icon` with no colour of its own is painted `TextPrimary`
whatever surrounds it, and a translation is in dp, so the band's `translateX(-130%)` cannot be said.

Flutter answers both from the control: a `ButtonStyle` resolves its foreground per state and hands it
down through `DefaultTextStyle` and `IconTheme`, and a child reacts to the control's states, never to
its own pointer, because a child inside a button has no pointer region of its own.

## What Changes

- **A hover is its control's, as a focus and a press already are.** Inside an enabled `Pressable`, a
  box's `Hover` diff applies while the CONTROL is hovered, so the arrow nudges when the button is
  hovered, wherever the pointer is on it. A box outside every control still hovers itself, as before.
  The control is the nearest one, the rule #616 gave the press and the focus: a box of a control
  nested in another reacts to its own control. Nothing declares itself a group: the control IS the
  group, as in Flutter. On the web the hover family becomes the control's
  (`.eq-pressable:hover .x`, leaving out a box under a control nobody hovers) for a box inside a
  control, still gated on `@media (hover: hover)`; Photon lends its tracked hover to the control's
  subtree, as it lends the press. A hover of the box itself inside a control is no longer said.
  Measured first: every hover a component in this repository states inside a control sits on the
  control's own surface, the box the `Pressable` wraps, where the control's hover and the box's are
  the same (the list is task 1.1), so none of them changes where it applies.
- **A foreground that descendants inherit.** `BoxStyle.Foreground` and `StyleDiff.Foreground`
  (`ColorToken?`): the colour a `Text`, an `Icon` and a `Vector` inside the box take when they
  name none of their own, resolved per state like any member, so `Hover = new StyleDiff { Foreground
  = theme.TextInverse }` brightens the label and the arrow together. With no ancestor that names
  one, the theme's `TextPrimary`, as today. On the web the box writes `color` and a text or an icon
  with no colour writes none, inheriting it (`currentColor` for an icon's fill), and the pressable's
  button and the link inherit their colour instead of the user agent's. On Photon the inherited
  foreground travels down the walk with the state, as `DefaultTextStyle` does.
- **A translation relative to the box's own size.** `Transform2D.FractionalTranslate(x, y)` and
  `TranslateFractionX`/`TranslateFractionY`, Flutter's `FractionalTranslation`: `-1.3` is 130% of the
  box's width to the left. It composes with the dp translation. The web writes `translate(-130%, …)`
  (a `calc` when both are set), and Photon multiplies by the box's laid-out size.
- **The handoff's primary button** uses all three: the sheen band translates by its own width on the
  control's hover, the arrow nudges, and the label's foreground follows the state.

## Parts reached

The vocabulary (`BoxStyle`, `StyleDiff`, `Transform2D`), the components that state a foreground, the
web realizer and its TypeScript twin (the hover family, the foreground's lowering, the text and icon
lowering), the generated stylesheet (button colour), and Photon's emit walk (the hover lent to a
control's subtree, the inherited foreground, the fractional translation). The public surface grows
(the two `Foreground` members, the fractional translation); `BoxStyle.Hover` keeps its signature and
changes its owner inside a control, which the migration line states. The developer surface does not
move.

**A behaviour change, allowed in preview.** A `Hover` on a box inside a `Pressable` now applies while
the control is hovered, not only the box. A box that should react to its own pointer inside a control
is its own control (a nested `Pressable`, or a `Hoverable`).

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `interaction-states`: a hover belongs to the nearest control like a focus and a press; a foreground
  descends to the text and icons that name none; a translation can be a fraction of the box's size.
