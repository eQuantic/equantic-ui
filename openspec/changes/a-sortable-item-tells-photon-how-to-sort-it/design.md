## Context

`SemanticNode` is the snapshot every shell reads to build its platform's accessibility tree. The Photon semantics walk
(`SemanticsVisitor`, one arm per vocabulary node) produces it. A `SortableItem` and its `Sortable` are wrappers there:
the list is a labelled group, the item says nothing of its own, and the controls inside it are the stops. The web
describes the focused control inside an item with the list's instructions (`dom/sortable.ts`, `describe`), and Photon has
nowhere to put them.

## Goals / Non-Goals

**Goals:**

- A semantic node can carry a hint, and every shell with an accessibility bridge sets it in its platform's word.
- On Photon, the keyboard stops inside a sortable item carry the list's instructions, as the web describes them.

**Non-Goals:**

- A hint for any other node. The field is general, and only the sortable walk fills it here.
- Windows, whose UI Automation provider does not exist yet (#310).
- Making a sortable item with no control of its own a stop (#1599).

## Decisions

**A field of the snapshot, named as Flutter names it.** `Hint` joins `SemanticNode` as an optional string, appended, as
every field after the first ones was. Flutter's `SemanticsProperties.hint` is "a brief textual description of the
result of an action", the same meaning as UIKit's `accessibilityHint`. AppKit's `accessibilityHelp` and Android's
tooltip text are the nearest words those platforms have for it.

**The walk carries the list's words down, and the item opens the hint.** `Visit(Sortable)` walks its children with the
list's `Announcements` in scope, and `Visit(SortableItem)` walks its own with the hint set to their `Instructions`, when
the item is enabled and the words say something. It is the shape `Visit(Box)` already has for the content style: the
recursion runs inside the arm, around a field restored after it. A nested list carries its own words to its own
items.

**The stops are the hint's, not the item.** The web describes the element that has the focus, which is a control inside
the item (or the item itself when it is the stop, which on Photon is #1599). So the pressable, link and adjustable arms,
the keyboard stops, take the hint in scope, and a field does not: on the web `describe` leaves a field alone, because
Space types a space there.

**Android from API 28.** `AccessibilityNodeInfo.TooltipText` exists from API 28, and the shell's floor is 26. Below 28
the node carries no hint, which is what it carried before. `HintText` is not used, because Android gives it to editable
fields only, and a field here already uses it for its label.

## Risks / Trade-offs

- [A reader hears the instructions on every control of an item, a card with three buttons included] → that is what the
  web says too, since every focused control inside an item is described. A card has one stop in the usual case.
- [The iOS and Android bridges cannot be driven by a test on this repository's hosts] → the AppKit bridge is read back
  through the real dispatch, and the iOS and Android shells build with the new field set.
