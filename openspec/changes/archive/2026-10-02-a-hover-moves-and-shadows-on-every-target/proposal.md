# Proposal

#507, the first slice of #504, a Feature under #190 (Flutter parity: the gaps that are work): a
hover can move a box and change its shadow, on the web and on Photon. The second slice, #508 (the
pressed state, the focus diff on Photon and the group hover, with #498's foreground), follows in
its own pull request.

## Why

A `StyleDiff` is what a box looks like while hovered (or focused), and it could only tint a box:
colours, border, elevation, opacity, gradient and backdrop blur. The equantic.tech handoff lifts a
button by a pixel and a card by two on hover and deepens the primary button's glow, and there was
no declarative way to say either. Measuring the diff on both targets found it was not even whole
where it existed:

- On Photon, a hover applied three of its seven members (background, border colour, border width).
  A hover that raised the elevation, faded the opacity, swapped the gradient or blurred the
  backdrop did nothing natively.
- On the web, a hover's backdrop blur was never written, and a hover that raised the elevation
  REPLACED the whole `box-shadow` list, so a card with a glow or an inset highlight lost both under
  the pointer.

## What Changes

- **`StyleDiff` gains `Transform` and `Shadows`.** `Transform` is a `Transform2D`, the same
  center-anchored, paint-only transform `BoxStyle.Transform` already is, replacing the base's while
  the state is active. `Shadows` replaces the box's custom shadows (`Shadow` and `Shadows`) while
  the state is active.
- **A hover applies every member on both targets.** On Photon, one effective style per hovered box
  feeds every paint the box makes: fill, border, elevation, custom shadows, gradient, backdrop
  blur, opacity and transform, each still gliding under the box's `Transition`. On the web, the
  hover's backdrop blur is written.
- **A state's shadow composes the way the base does.** The shadow list while a state is active is
  the state's elevation (or the base's), then its custom shadows (or the base's), then the base's
  inset highlight, so a hover that deepens one keeps the others, on the web as on Photon.
- **A state's background layers compose too.** A hover gradient is the first of the box's layers,
  so the base's glow and pattern stay under it, with their sizes, where the hover used to write
  the gradient alone.
- **The base list is one list on both web producers.** The C# realizer and its TypeScript twin
  wrote the custom `Shadow` and `Shadows` in opposite orders, and a shadow with no geometry was a
  `none` inside the list in C# (which CSS rejects with the whole declaration) and a zero shadow in
  TypeScript. Both now write the C# order and leave such a shadow out, as Photon draws nothing for
  it.
- **An element carries each atomic class once.** The two spellings of `backdrop-filter` hash to one
  class, and the element listed it twice.

What a developer writes:

```csharp
new BoxStyle
{
    Shadows = [new ShadowSpec(12, 30, -12, Brand.Glow)],
    Transition = new TransitionSpec(StyleChannels.Transform | StyleChannels.Shadow),
    Hover = new StyleDiff
    {
        Transform = Transform2D.Translate(0, -1),
        Shadows = [new ShadowSpec(16, 36, -12, Brand.GlowStrong)],
    },
}
```

## Impact

- **Developer surface:** none outside C#.
- **Public surface:** `StyleDiff.Transform` and `StyleDiff.Shadows` (additions). Nothing goes.
- **Parts reached:** Primitives (the record), the web realizer and its TypeScript twin (the hover's
  declarations, the same strings on both producers), the two style atomizers, the runtime's
  vocabulary twin and wire shape, and Photon's emit visitor (the effective style).
- **Markup:** an element with a backdrop blur loses the duplicate of its blur class, and a box
  whose `Shadow` and `Shadows` are both set lowers its list in the C# order in the browser too.
  Server and browser change together, so hydration still meets the classes it expects.
- **Docs:** the wiki's styling section, English and Portuguese, and `docs/FLUTTER-PARITY.md`, which
  gains the rows for `Transform` and for state-resolved styles.
