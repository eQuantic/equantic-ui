# A dialog opened from a menu item gives the focus back to the menu's trigger

Closes #1812, a bug found reviewing #1811 (a closed dialog gives the focus back to its opener).

## Why

A row's menu offering Delete closes and opens a confirm in one pass, and the confirm gave the focus back
to nothing: a keyboard user was back at the top of the page. Two causes, and the case needs both cured.

1. A menu's item took the focus on its press and left with its menu in the pass that opened the dialog,
   so when the dialog recorded its opener the focus was on the body (the web) or dropped with the item
   (Photon, #976).
2. On the web, the traps were reconciled at the end of a pass, before the render manager writes the tree
   the pass produced, so they read the DOM the pass before had written. A dialog a render opened took no
   focus and recorded no opener until some later pass came, which also hit #1811's own case. The cases
   that call `commitFocusTraps` by hand could not see the order.

## What changes

- `MenuPanel`'s items, which `Menu`, `ContextMenu` and `TreeView` share, leave the keyboard where it is
  (`CanRequestFocus = false`), as the menu pattern keeps it on the trigger while the arrows walk the items
  as its `aria-activedescendant`. A dialog an item opens records the trigger, and gives the focus back to
  it. A context menu leaves the focus where it was when the menu opened.
- The browser reconciles the traps in the microtask after the write, where the in-view observers attach.

For a developer nothing is written differently: a dialog opened from a menu gives the focus back to the
menu's trigger on both targets, and a dialog a render opens takes the focus in the pass that shows it. No
public surface and no developer surface moves; the `MenuPanel` twin is generated again.

## Principles

It follows 4 (written once: the cure is in the shared component, so both targets get it) and 9 (the
structural fix: the traps read the DOM the pass wrote, rather than a later pass covering for them).
