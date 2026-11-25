# Proposal

Closes #1111, a Bug under #849.

## Why

On macOS, VoiceOver could not walk into a Photon window: it stopped at the title bar's buttons, and the
application's focused element was the window itself, while a control of the page held the keyboard.
Measured from outside with the AX API on the PhotonDesktop sample, three defects stood behind it:

- Every read of the tree built new elements and released the ones before, so an element an assistive app
  held answered `kAXErrorInvalidUIElement` once anything read the tree again. VoiceOver reads it at its own
  moments, so the element its cursor stood on died under it, and only the title bar's buttons lived.
- Each element named the content view its parent, but the view was no accessibility element: the window
  listed the elements as its own children while each named a parent that nothing listed.
- Nothing answered the view's focused element, and nothing told VoiceOver when the keyboard moved.

## What Changes

- **One element per node, for as long as the node lives.** Each read finds a node's element by the id of
  the node's position, tells it what the node says now (a value, a pick or an open list it no longer has
  is taken away), and releases it only when the node leaves the tree. A position that holds another kind
  of node gets a new element. Flutter's macOS embedder keeps one native node per semantics node the same
  way.
- **The content view is the group the window lists**, an `AXGroup` element and its elements' parent.
- **The focused element is the element of the node with the keyboard**, answered from the host's
  `FocusedId`, or the view when no node holds it, and `NSAccessibilityFocusedUIElementChangedNotification`
  is posted on it when the keyboard moves, once an assistive app has read the tree, so a person with no
  assistive app pays nothing for it.
- AppKit's notifications are posted through one place, `AppKitNotifications`, which the announcements use
  too.

A developer writes nothing different. No public surface and no developer surface moves.

## Impact

- `eQuantic.UI.Native.Shell.MacOS`: the accessibility bridge, the content view, the window's frame loop and
  the announcements.
- The iOS and Android bridges are not touched.
