# Design

## How Flutter solves it

The question does not arise there: Flutter has no list of node types that carry a size, because a
widget's size is the constraint its own render object answers. Here the declaration lives on the node
and the engine has to read it, so the answer is a classifier and an instrument that keeps it complete.
No row of `docs/FLUTTER-PARITY.md` applies.

## Decisions

**A list of arms, held complete by a test that enumerates the vocabulary.** Reading the declaration
by reflection at layout time would make the engine pay on every slot and lean on metadata a trimmed
native build may not keep. So `MainSizeKind` stays a switch, and `MainSizeKindCoverageTests` takes
every concrete node type from the Primitives assembly, finds the ones that declare a width or a height
(a `SizeValue` of their own, one carried by a style, or a number their constructor demands), builds
each with a declared size and no constructor run, and fails when the classifier does not read it. A
fix that added only the camera preview Copilot named fails it eight times, once per remaining type
and axis.

**A window-relative size is a declared size.** It is the node's own, decided by the window and not by
the slot, and Chrome keeps it in a share narrower than it.

**A scroller's width is a ceiling.** The web realizer writes a `ScrollView` `max-width: 100%`, so
Chrome caps a 400-wide scroller at its 300 item and keeps a 100-wide one at 100. `MainSizeIsACeiling`
says so for the width, and the Slot keeps the scroller's size up to the slot. Its height is not capped
there.

**`MainSizeKind` no longer asks `CrossSizeKind`.** The two answered the same question on the two axes,
which is how the gap crossed from one to the other. `CrossSizeKind` has the same gap, and it stays as it
is here: this pull request takes no new issue.

## Risks

- The coverage test finds a node type's size by its member names (`Width`, `Height`, `Size`) and by a
  style that carries them. A node that declared a size under another name would not be asked about,
  and the test's first fact checks that the fourteen known ones are found, so an enumeration that
  breaks fails rather than passing over nothing.
