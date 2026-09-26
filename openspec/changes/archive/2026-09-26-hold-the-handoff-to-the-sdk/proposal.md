# Proposal

Closes #337, a Task under #207 (Handoff fidelity: verify and fix the visible deviations).

## Why

The design system came back from Claude Design on 2026-09-23 with a review against Apple's and
Google's guidance ("Beyond HIG and M3"). Read against the SDK, its pages called four shipped things
requests (DatePicker, NavigationRail, `VariantColors.Hover`, the `SemanticNode` state fields) and
counted 44 components where the SDK has 56. Nothing could have noticed: the figures on the pages were
prose that no pin read, the statuses were badges that no release moved, and a member added to a token
class shipped without the export hearing of it. The dark palette passed WCAG 2 while APCA measured
muted text on `SurfaceSubtle` at Lc 42.2, well under what it asks for small text.

## What Changes

- **A figure a page prints is held to its token.** A number that derives from a token is written with
  its path (`data-token`), compared with the value the pin holds, and counted against a ratchet so a
  re-export cannot pass by dropping the markers.
- **A block's status is derived from the SDK.** `docs/design/status.json` gives every item that is,
  or was, a request a probe into a `PublicAPI` file or the source, checked both ways.
- **Every public token is published or exempt.** Reflection over the token classes requires each
  public member at a `tokens.json` path some test compares, or named with the reason it is not a
  design value.
- **APCA is a second contrast gate** beside WCAG 2, and 14 dark values were re-solved to clear it,
  keeping each OKLCH hue and moving lightness. Muted text on `SurfaceSubtle` goes from 5.23:1 and
  Lc 42.2 to 7.80:1 and Lc 60.3, and every WCAG 2 ratio rises with it.
- **Three decisions are recorded, as Edgar confirmed them on 2026-09-26:** APCA with the re-solved
  dark palette; the pointer exception with a 24dp floor under a pointer, whose implementation is
  #430; and continuous corners wherever the target can draw them, on every native target and on the
  web through CSS `corner-shape` where the browser supports it (#346).
- The review's other proposals are issues #338 to #351.

For a developer using the SDK: in dark mode, the SDK's own palette changes. Fills get lighter with
dark text on them, the way Material 3's dark scheme does, and muted text clears APCA's floor. Nothing
is written differently, and an app that provides its own `IAppTheme` keeps its own values.

The parts reached are Primitives (the dark palette's values), the runtime's cross-pins and shared
fixtures, the Photon goldens (19 dark ones, regenerated through their own switch), the generated
design system and `docs/design`. No public signature changes, and the developer surface is untouched.
