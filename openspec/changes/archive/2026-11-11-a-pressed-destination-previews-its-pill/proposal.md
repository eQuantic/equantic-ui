# Proposal

Closes #1254, a Bug under #1174.

## Why

B4 says "Press-down shows the pill at 40% instantly", and C16 speaks the same selection vocabulary.
The bar and the rail pressed the pill into `SurfaceSubtle`, the colour B4 gives the hover ("Item
hover = SurfaceSubtle pill"), so a press previewed nothing of the selection it was about to make.

## What Changes

- The pill of every destination but the selected one is pressed into the selection's own pill,
  `Primary.Subtle` at 40% opacity, on `BottomNavigation` and `NavigationRail`. The selected
  destination keeps its pill.
- `PressedFillPlacementTests` and `PressedFillPlacementRealizerTests` pin the press's colour on
  Photon and on the web.

Two halves of the handoff stay out of this change. The hover: a box's hover is the box's own on both
targets, so a hover on the pill would light only while the pointer is over the pill, not over the
destination; a hover scoped to the control is a mechanism of its own, #1456. The filter chip:
reading the handoff's frames showed that the unselected filter chip RESTS outlined, so its press is
not fixed without its rest, which is #1460.

## Impact

- `src/eQuantic.UI.Components/BottomNavigation.cs`, `NavigationRail.cs` and their transpiled twins.
- An app's navigation bar and rail press in a colour that previews the selection. No API changes.
