## Context

A context request is a gesture every platform has and spells differently: a right-click, a Control-click on a
Mac, a long press on a touch screen, the keyboard's menu key, Shift+F10. The browser folds most of them into one
`contextmenu` event (iOS Safari's long press excepted); Photon's shells deliver none of them today.

## Goals / Non-Goals

**Goals:** one block that turns all of these into one report, on both targets; a menu that opens where it was
asked for; a ready-made context menu built from public blocks only.

**Non-goals:** the native menus of an operating system (an app's menu is drawn by the app, as `Menu` is); a
reader's own "show menu" action (VoiceOver's AXShowMenu, TalkBack's custom actions), a follow-up; a flip or a
clamp at the viewport's edge, which `Anchored` does not do for any placement yet; the menu key routed to a
composite's active item (a tree's, a grid's), whose focus stays on the composite, which the tree's menu per node
(#1532) brings.

## Decisions

- **A wrapper node, not a `Pressable` member.** A capability is a block that wraps any child and adds one
  thing (the composite rule). `Pressable.OnContext` would give a menu to controls alone; a tree's row, a
  paragraph, a chart or a canvas asks for one too. The keyboard reaches a target through the focus inside it,
  as a `Shortcut` scoped to its subtree does.
- **Where, in the target's box.** `ContextRequest.At` is the point relative to the target's top-left corner,
  null when the keyboard asked. That is the space `Anchored.At` takes, so `ContextTarget` around an `Anchored`
  opens the panel where the pointer was with no arithmetic in the app, and at the target's own placement when
  the keyboard asked.
- **The nearest target answers.** Nested targets (a menu for a file inside a menu for its folder's panel) are
  resolved like a press: the innermost one under the pointer or around the focus.
- **A finger's hold is 500 ms**, the platforms' long press, longer than a sortable item's 250 ms lift: inside
  a sortable item the lift takes the hold, and the menu is asked for by a hold that never moved and never
  lifted anything.
- **The browser's own menu stays out** only where a target answers; everywhere else it opens as always.

## Risks / Trade-offs

- iOS Safari fires no `contextmenu` on a long press, and Android Chrome does: the controller counts a finger's
  hold itself and ignores a touch's `contextmenu`, so both phones ask once.
- A shell that cannot be run here (Windows, Android, iOS) is proven by its own build and by the host's tests,
  which drive the same `ContextRequest` the shells call.
