# Design

## How Flutter answers it

- **Text width.** A Flutter `Text` under a tight width (a sized `Container`, a stretching `Column`)
  takes that width, so its alignment and its paint read across it. A block Text on the web does the
  same in a block parent. Photon's Text sizes itself in a Box and is never stretched by a flex
  container; that divergence is Photon's, and is filed on its own.
- **The scrolled header.** `AppBar.scrolledUnderElevation` reacts to the scroll of the scrollable
  under it (`ScrollNotification`, depth 0), and a pinned `SliverAppBar` to its own scroll view's.
  The header's surface here is the one `position: sticky` pins to: the nearest scroll view, or the
  page. Photon has no page scroll, so outside every ScrollView a header is never scrolled there.
- **A drag over a transformed child.** A `Transform` around a child composes with the child's own;
  the web's individual `translate` property is the one that composes with `transform`.
- **Density.** `VisualDensity.adaptivePlatformDensity` is decided once at start-up, with no server
  in between. Here the server has to be told, and only the browser knows.

## Decisions

- **The scrolled state is the header's, as an attribute the lowering never writes.** The reconciler
  diffs the tree it lowered, so it never takes away what it never wrote, and the attribute survives
  a re-render. One class on `<html>` could not tell a header in a scrolling panel from one on the page.
- **The lift is marked by the producers, not reached by a selector.** CSS cannot lift through a chain
  of `display: contents` wrappers of any depth with child combinators, and a descendant combinator
  would position elements deep inside a card that are nobody's slop. Both producers walk the wrappers
  from the same lowered tree, so hydration compares equal.
- **Density through a session cookie, then hydrate-then-switch.** No header carries the pointer, so
  the first request of a session cannot know it: it renders Comfortable, hydration lowers at what the
  server rendered, and the whole page switches once. Every later request is right from its first
  byte. A session cookie holds a display fact about the device and goes when the browser closes.
