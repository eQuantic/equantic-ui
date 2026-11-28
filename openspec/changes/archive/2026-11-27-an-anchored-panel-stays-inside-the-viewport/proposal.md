## Why

Closes #1933, a Bug under #202, asked for by Edgar ahead of the sweep.

`DatePicker` hung its calendar off the opener at the end of its field with `Anchored`'s default `BottomStart`. So the
panel started at the glyph and grew to the right, and on a field that spans its column it left the viewport at every
width. Measured on francinebolos's order form, on preview.73, with the panel 334px wide:

| Viewport | Panel | Overrun |
|---|---|---|
| 390px | 306 to 640px | the page scrolls sideways to 640 |
| 768px | 669 to 1003px | past a 753px layout |
| 1280px | 955 to 1289px | past a 1265px layout |

Hung from the end instead, the panel stays inside at 1280px, but at 390px it starts at -8px: the calendar is wider than
the room left of the glyph. `Anchored` documented the fence: no viewport flip or clamp.

## What Changes

Nothing an app writes changes.

- **A date picker hangs its calendar `BottomEnd`**, under the end of its field, where the opener is.
- **An anchored panel past the viewport's start or end edge is moved back inside**, `Anchored.ViewportInset` (8dp) from
  the edge, and a panel wider than the room between them starts at the start edge's inset. This holds for every
  `Anchored` panel, a menu's, a select's and a tooltip's as much as a calendar's, on both targets, by one rule.
  - **Photon:** the realizer moves the panel once its layer is laid out and its width is known
    (`PhotonRealizer.KeepInside`), through the laid-out tree's new `LayoutNode.Offset`.
  - **Web:** the panel is placed by CSS alone, which cannot know where it lands in the viewport. The runtime measures it
    after each pass is written for the panels the page holds open, when the pointer or the focus reaches a
    hover-revealed one, and for all of them when the viewport changes width (`dom/anchor-inset.ts`). It moves the panel
    by its `translate`, a property of its own beside the `transform` a centred placement and an open/close glide
    already write.
- A vertical flip stays the fence.

Parts reached: the vocabulary's `Anchored` and its placement's docs, the components' `DatePicker`, Photon's realizer
and layout tree, and the web runtime. `Anchored.ViewportInset` joins the contract, and `LayoutNode.Offset` the
experimental native surface.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `anchored-panels`: an anchored panel stays inside the viewport's width, and a date picker's calendar hangs under the
  end of its field.

## Impact

- `AnAnchoredPanelStaysInTheViewportTests` opens a date picker whose field spans 390, 768 and 1280dp, and finds the whole
  calendar inside each. Its end sits under the opener's, and a panel past the start edge, or wider than the viewport,
  comes back at the inset. All six fail without the move and the placement.
- `anchor-inset.spec.ts` holds the web's rule on the issue's own measurements, and its commit and hover paths.
- Measured in a browser, a calendar-sized panel under a field's end opener moves from -3..301 to 8..312 in 330px, and
  stays inside at 768 and 1280, with no sideways scroll.
