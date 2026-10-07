# Proposal

#508, the second slice of #504 (a Feature under #190, Flutter parity: the gaps that are work): a
control's press and focus show on the boxes inside it, on the web and on Photon. The third slice,
#614 (a descendant reacting to its control's hover, and the foreground its text and icons inherit,
#498), follows in its own pull request.

## Why

The equantic.tech handoff presses its buttons with `:active { transform: scale(.985) }` and its cards
with `scale(.995)`, and there is no declarative way to say it: `Pressable.PressedBackground` swaps a
fill and nothing else. Measuring the states that do exist found the focus one dead on both targets:

- `BoxStyle.Focus` is written on the web as `.cls:focus-visible` on the box, and a box is never
  focusable. The focusable element is the `Pressable`'s, and the box is inside it, so the rule cannot
  match. Photon never applies the diff at all; it draws the focus ring and nothing else.
- The web's focus ring is a `box-shadow` rule that outranks the box's own, so keyboard focus takes a
  raised or glowing control's elevation and glow away. Photon draws the ring beside the shadows, and
  the handoff says the ring "coexists with any fill" and is "a second rrect border, not a shadow".

## What Changes

- **`BoxStyle` gains `Pressed`**, a `StyleDiff` that applies while the control the box is inside is
  pressed: a pointer held down on it, on both targets, and a held key on the web.
- **`Focus` is the control's focus.** It applies while the control the box is inside has keyboard
  focus, on both targets, where it applied nowhere.
- **Pressed beats focus, and focus beats hover**, on both targets, as the handoff's §10 orders them.
  A state's diff composes the lists it shares with the base, as a hover's does since #507.
- **The focus ring keeps the box's shadows** on the web: it is drawn the same way, the 2dp Surface gap
  and the 2dp ring, inside the box's own shadow list instead of over it. A `Simulated` focus draws it
  too, as it already does on Photon.
- **Photon glides a state's custom shadows** under `Transition(Shadow)`, as it glides the elevation's
  and as the browser glides the list. A shadow that comes or goes fades, as CSS pads the shorter list
  with transparent ones.

What a developer writes:

```csharp
new BoxStyle
{
    Transition = new TransitionSpec(StyleChannels.Transform, Motion.BaseMs),
    Pressed = new StyleDiff { Transform = Transform2D.Scale(0.985f) },
}
```

## Impact

- **Developer surface:** none outside C#.
- **Public surface:** `BoxStyle.Pressed` (an addition). `BoxStyle.Focus` keeps its signature and
  changes its meaning, from a focus nothing could have to the focus of the control the box is in. No
  component sets it, so no app meets the change except one that set it and saw nothing.
- **Markup:** every `box-shadow` the web writes for a box starts with the focus ring's slot,
  `var(--eq-ring, 0 0 #0000)`, a transparent shadow until the control is focused, so the class names of
  every box with a shadow change. Server and browser change together, and hydration still meets the
  classes it expects. A simulated focus adds `eq-focused` to its control.
- **Parts reached:** Primitives (the record), the web realizer and its TypeScript twin, the two style
  atomizers (the new pressed and focus rule families), the generated stylesheet (the ring), the
  runtime's vocabulary twin and wire shape, Photon's emit visitor and press scope, and the transition
  store.
- **Docs:** the wiki's styling section in English and Portuguese, the `WidgetStateProperty` row of
  `docs/FLUTTER-PARITY.md`, and the ledger.
