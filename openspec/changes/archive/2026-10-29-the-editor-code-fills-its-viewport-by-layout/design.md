## Context

The editor's tree, bounded: the slab, a vertical `ScrollView`, a `Row` of the gutter and a `Flexible`
holding a horizontal `ScrollView`, the `CodeSurface`, and the `CodeBlock` inside it. The surface takes the
pointer on both targets, so it must be as tall as the room the code is shown in. The width already had a rule
(`CodeBlock.ViewportWidth`, the width the sideways scroll view reported), kept because the marks need the
number to paint across. The height had the same kind of rule, `CodeBlock.MinHeight` set to the vertical
scroll view's reported height, and it counted room the code does not have wherever a scrollbar takes space.

## Goals / Non-Goals

**Goals:** the code is exactly as tall as the room under the sideways scroll view's scrollbar when the file is
short, as tall as the file when it is long, on the web and on Photon, from the first frame, and a short file
never makes the editor scroll.

**Non-Goals:** styling the web's scrollbars (the runtime leaves them to the platform), and the width, whose
reported number stays because the marks paint across it.

## Decisions

- **How Flutter does it.** `SingleChildScrollView` gives its child unbounded room on its axis, and its
  documentation makes content at least as tall as the viewport with a `LayoutBuilder` that reads the
  viewport's constraints and a `ConstrainedBox(minHeight: viewportConstraints.maxHeight)` around the content,
  with an `IntrinsicHeight` when a child must stretch to it. All of it is decided in the one layout pass, and
  Flutter's scrollbars paint over the content, so they take no room. Across its axis the scroll view hands its
  child its own constraints, so one stretched by a row forces its child to the row's height.
- **The scroll view says it, each realizer satisfies it in its own layout.** Our two targets do not share a
  layout pass: the web's is the browser's. A `LayoutBuilder` on the web is a measurement read after the frame
  and built back, which is the number this replaces. So the statement lives on the scroll view, the one node
  that knows its viewport on both targets, as a flag: `FillViewport`. The name is the vocabulary's own words
  (the viewport of `OnViewportChanged`).
- **On the web, a flex line over the content box.** A scroller that fills is `display: flex` on its axis and
  its child `flex-grow: 1; flex-shrink: 0`. Growth is measured against the scroller's content box, which a
  scrollbar that takes space is outside of, and needs no percentage to resolve. Across, `align-items`'
  stretch takes the content box's breadth when the child states none. The child's `flex-shrink` is replaced
  rather than added, so one declaration carries it and the C# and TypeScript producers write the same class.
- **On Photon, a second measure at the viewport's size.** After the viewport is resolved, a child that came out
  smaller on an axis is measured again with that axis decided at the viewport's extent, stretched. A child
  that sizes itself to what it holds and takes no stretch while it measures, a text, is given the extent as a
  row's stretch gives it, through the wrappers that take its size. A size a child states stands on both
  targets: the web grows the item only when it states no length on the scroll axis (first written as a floor
  whatever the child asked; Copilot found it left a fixed child laid out for its old size, #1158). A cap
  stands too, and the bounds-only stretch is the text's alone: a container measured at less than the extent
  keeps that size with what it holds arranged in it. A stack takes a flex line's stretch while it measures,
  as the web's grid does, so it aligns its children in the stretched box.
- **A stretched scroll view takes the stretched size on Photon.** A scroll view hugging on an axis its parent
  stretches now resolves to the stretched extent, as a box and a row do there and as the web's
  `height: 100%` does. Without it the row moved the scroll view's bounds after the scroll view had measured
  its content against its own height, and nothing inside could fill the row.
- **The row is always there.** With no gutter the sideways scroll view stood straight inside the vertical one,
  where its `height: 100%` resolved to exactly the viewport, cutting a long file. In the row, the sideways
  scroll view is a stretched flex item: as tall as the row, which is as tall as the viewport or the code,
  whichever is more. `Cross = Stretch` gives the gutter the same height.
