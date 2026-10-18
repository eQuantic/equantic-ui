# Design

## Context

After #975 the state a Photon node keeps lives on its element, and the scroll and drag regions carry
the element. Everything else the host does across frames still finds a node by its layout path:

- `PhotonHost` keeps `_pressedPath`, `_focusedPath`, `_textPath` and a hover chain of paths, finds the
  current frame's region by string equality, decides focus within a subtree and the scroll view that
  holds a stop by prefix (`IsAncestorPath`), and remembers each code surface's revealed version in a
  dictionary by path that nothing clears.
- Every region (`HitRegion`, `HoverRegion`, `TextRegion`, `CodeRegion`, `SheetRegion`, `CanvasRegion`,
  `LinkRegion`) and `FocusStop` carry a path; `ScrollRegion` carries one beside its element for the
  containment questions above. A linked run inside a paragraph is named by the paragraph's path and
  `#` and its index.
- The live region announcer remembers what each region said, and when it last spoke, by path.
- `SemanticNode.Path` is what the shells name a node by: macOS and iOS map their native accessibility
  objects to it, and all three call `ActivatePath` and `AdjustPath`. Android's input connection checks
  `FocusedPath` to know it still serves the same field.
- The layout forms a path for every node of every frame (`LayoutContext.ChildPath`, a cache that
  survives frames so the strings are not made again), and the realizer names each overlay layer
  `ov<i>`.

Since the host cannot say which component input reached, every frame after input rebuilds every
component. Measured on main `f6f1e375a` with recycled frames, the stateful dashboard costs 28,534
bytes a frame at rest and 94,190 bytes in a frame after a press on one card's button, which is the
frame a person causes.

## Goals / Non-Goals

**Goals:**

- The host finds a control by its position: a press, the focus, the field being edited and the hover
  stay on their control while its position holds it, and never pass to a node that takes its place.
- The shells name a node by an integer id its position carries.
- The path goes from the layout, the realizer and the host.
- A frame after input builds what the input reached.

**Non-Goals:**

- Matching a list's unkeyed children the way Flutter does, from both ends. With the paths gone it
  would be free to change, but the browser matches by index until E3 drives it with the same engine,
  and a write-once component has to keep its state the same way on both targets. So an unkeyed row
  keeps its focus when a sibling is inserted after it, and a keyed one wherever it moves, exactly as
  before; the change belongs to E3.
- Android's virtual view ids: its provider names a node by its index in the frame's semantic list, and
  stays so; the index now leads to the node's id. A stable virtual id is the provider's own change.

## Decisions

### An id per position, and per part

*How does Flutter solve it?* A `SemanticsNode` has an integer `id` from a counter, kept for as long as
the render object that made it, and a `RenderParagraph` caches the child semantics nodes it makes for
the spans that take gestures, so a link in a sentence keeps its id. `docs/FLUTTER-PARITY.md`'s
`SemanticsNode` row is the one this touches.

Here an element takes an id when it mounts, from its owner's count, and keeps it while it lives; a
node at another's place mounts a new element and so has a new id. A part a realizer draws as a target
of its own, a linked run, takes an id from the same count the first time its position is asked for it
(`NodeElement.PartId`), kept on the position, so a run keeps its id while its paragraph's position
lives and two ids never collide. An integer, because that is what every bridge needs: Android's
virtual view ids are ints, and macOS and iOS key their native objects by it.

### What the host holds

A node's region, and a focus stop, carry the node's element. The host holds an element for what is a
node (the pressed control, the field, code surface or spreadsheet being edited, the hover chain, the
canvas under the pointer), and an id for the focus, because a linked run is a focus target with no
element of its own: a stop carries both, the id it is focused by and the element it sits in. A control
from another frame is found in this one by its element, which is the same object while its position
holds it, and by its element alone: an app can keep one node and hand it to two pages, so the same
node at a new position is not the control that was pressed. The first version also took the press's
node as an answer, and review found the release reaching the replacement. A press the system
cancels lets its position go too, or the paint walk, which asks by position, kept it drawn pressed;
main kept the path the same way.

Containment walks the parents: focus within a shortcut's subtree, the scroll views that hold a stop or
a caret, and a press swallowed by an inert control unless the control lies under the pressable that
owns the gesture. The prefix test and its segment rule (`r/1` is not above `r/10`) go with it.

### What the announcer and the reveal remember

The live region announcer's memory per region (what it last said, when it last spoke) and a code
surface's last revealed version become state on their elements (`NodeStates`), as #975 did for the
stores. They leave with the element, which is what the announcer's "forget the regions this frame no
longer holds" and the reveal's never-cleared dictionary were each approximating.

### A frame after input builds what the input reached

Input changes what the host paints (a press's fill, the focus ring, a caret, a hover style, an offset)
and reaches the tree through the handlers it runs, which call `SetState` for what they change, as on
the web. So input asks for a frame that builds only the marked positions.

What the host routes into a field, a code surface or a spreadsheet it hands to the app: a field's
`OnChanged` with the new value, and a surface's `OnChanged` once its model took the input, which the
surface's contract makes its component's cue to `SetState`. The host does not guess where the value
lives. The first version of this change marked the stateful component nearest above the surface,
and review found the case that guess gets wrong: `TextInput` keeps state of its own (its focus) and
holds the field but not the value, so a form keeping the value without a `SetState` had the wrapper
rebuild with the old text, and every key typed was lost. A Photon field draws the value the app hands
it, so when a handler calls no `SetState` the host cannot know which component would show the key,
and that frame rebuilds every component, as every frame after input did before.

The owner counts every `SetState`, including one on a position already waiting to build
(`BuildOwner.Marks`), and the host reads the count around the handler. The request for a frame
cannot say it: a position asks for its frame once, so a key typed while the editor waited to build
for its own viewport looked like a handler that said nothing. The structural answer is the browser's
and Flutter's, a field that keeps what was typed and takes the app's value only when it changes, and
it changes what a handler that rejects a key sees on Photon, so it is its own change (#996).

An invalidation the host is told of, a resize, a density change and a hot reload still rebuild every
component, because each changes what every build reads.

Work posted to the thread that draws (`PhotonDispatcher`) woke each host with `Invalidate`, so every
result a worker posted rebuilt every component. It asks for a frame now: the work runs at the top of
that frame, before the build, and marks what it changes with `SetState`, which is how an awaited
result reaches the tree on the web too.

### A position that leaves takes the focus with it

After a frame, a focus or a field being edited whose position left the tree is forgotten, as a
browser's focus falls back to the page when the focused element is removed: `FocusedId` answers
null, and Enter presses nothing. Holding the id of a position that is gone would have the bridges
report a node no reader can land on, and the next Tab starts from the first control, as it did when
a path stopped resolving.

### The path goes

The measure walk carried a position, the path and the element together, so the two could not drift;
it carries the element alone now, and `Position` goes. The layout's error messages name the node's
type where they named its path. The grid's hug-column probe measured at `…/probe` with the child's
own element, and now measures with the element alone. The realizer's layers lose their `ov<i>` names,
and the host its cache of path strings.

## Risks / Trade-offs

- [An app that changes what a build reads in a press handler without `SetState`] → its frame no
  longer shows the change until something rebuilds it, which is the web's behaviour and E1's rule for
  frames drawn for motion. A field's or a surface's handler that calls no `SetState` still gets a
  frame that rebuilds everything, so a key always shows.
- [Work posted to the thread that draws, which changes what a build reads without `SetState`] →
  it no longer shows by itself, since the wake builds nothing; the web behaves the same, and the
  samples' awaited results all set state.
- [A field in a row of a virtualized list] → when the row scrolls out of the list's window its
  position leaves, and editing ends with it, where a keyed row's path came back and editing resumed;
  a browser and Flutter's `ListView.builder` drop the focus the same way.
- [A focus target that is not a node] → a linked run is the one there is, and it is named by its
  paragraph's position and its index there, as its path named it.
- [Ids grow for the life of a tree] → an int counts two billion mounts; a list that mounts a
  thousand rows a second takes twenty-four days of scrolling to wrap it.

## Measurements

`PerfHarnessTests`, recycled frames:

| | main `f6f1e375a` | this change |
|---|---|---|
| a frame after a press (stateful dashboard) | 94,190 B | 31,246 B |
| stateful dashboard at rest | 28,534 B | 28,718 B |
| dense scene | 38.1 KB | 38.1 KB |
| gliding scene | 39,133 B | 39,117 B |
| an overlay layer | 482 B | 482 B |
| dense scene, frames not recycled | 88.7 KB | 86.8 KB |

The press builds the one card its handler marked, and its ceiling is set at 34 KB. The dashboard at
rest pays 184 bytes more: each of its 24 focus stops carries an id beside its element where it
carried a path, eight bytes each in the list a frame makes. Without recycling, the paths the layout
formed are gone.
