# Design

## How Flutter answers it

Flutter resolves a style PER PROPERTY against the set of states a control is in:
`ButtonStyle(elevation: WidgetStateProperty.resolveWith((states) => ...))`, where the states are
hovered, focused, pressed, dragged, selected, disabled. A lift on hover is a `Transform` or an
`AnimatedSlide` driven by a `MouseRegion`, and a deeper glow is the elevation and shadow colour the
button style resolves for the hovered state. This SDK resolves PER STATE instead: a `StyleDiff` is
the partial style a state lays over the base, which a realizer can write as one pseudo-class rule on
the web and Photon applies as one overlay. The two models say the same thing for a fixed order of
states; `docs/FLUTTER-PARITY.md` gains the rows that record it.

## Decisions

### Transform and shadows REPLACE while the state is active

A `transform` in CSS is one property, and a hover rule that sets it replaces the base's; so does
`StyleDiff.Transform`, and a box that is rotated at rest and lifted on hover says both in its
hover transform. `StyleDiff.Shadows` replaces the custom shadows the same way. Elevation stays its
own member, because it is a level the theme resolves rather than a list the author wrote.

### The shadow list is composed, not replaced

The base writes one `box-shadow`: elevation, then custom shadows, then the inset highlight. A state
that changes one of them must write the WHOLE list again, since CSS replaces the property, which is
why a hover that only raised the elevation lost the glow and the highlight. The web realizer and
its TypeScript twin now compose the state's list from the state's members and the base's, in the
base's order, and Photon draws the same list.

### One effective style on Photon

The hover was applied inside the box's chrome, member by member, and three members made it there.
Photon now answers the box's effective style in ONE method, the base with the active state's
members laid over it, and both halves of the box ask it: the emit visitor's entry, where the
opacity layer and the transform wrap the box, and the chrome, which fills, borders and shadows it.
Every paint reads the same values, the two halves cannot disagree about whether the box is
hovered, and a member added to `StyleDiff` has one place to land. It allocates nothing: the style
is a struct, copied only while the box is hovered. The pressed fill still beats the hover's (§10).

### One list on both web producers

The class an atomic declaration hashes to is how a hydrating page recognises the server's markup, so
the two producers must write the same text. They did not for the base's custom shadows: C# wrote
`Shadow` before `Shadows` and TypeScript the reverse, and a shadow with no geometry was `none` in
C#, inside a comma list, where CSS rejects the whole declaration. Both now build the list in one
helper each (`ShadowList`, `shadowList`) in the C# order, leaving out a shadow that draws nothing,
and the component parity fixture compares them on a box that sets every part.

### What stays out

- The focus diff on Photon, which no component uses yet, and the pressed state: both are the state
  of the CONTROL around the box rather than of the box under the pointer, and they land together
  in the next slice (#508), with the group hover and #498's foreground.
- Gliding a state's custom shadows on Photon. The elevation's shadow glides under
  `Transition(Shadow)` as before; a custom shadow snaps there, where the browser glides the list.
  #508 carries it.
- A pinned header's scrolled style, the other `StyleDiff` in the vocabulary, which the web writes
  four members of and Photon does not draw: #506.
