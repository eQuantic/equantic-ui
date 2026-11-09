# Design

## Context

Since #976 the host finds a control by its position and a frame after input builds what the input's
handlers marked. A field is the exception: it draws the value the app hands it (`TextEntry.Value`), so
a key shows only once something builds the component that holds the value, and the host cannot see
which one that is. `RebuildUnlessTold` answers it by rebuilding every component after a field's handler
that called no `SetState`.

Between a key and the frame that draws it the host already keeps what was typed, in `_edited`, because
a phone's keyboard edits twice between two frames and the second edit has to start from the first. It
is dropped at the next frame, and every other reader (the measure, the paint, the semantics) reads the
node's value.

## Goals / Non-Goals

**Goals:**

- A field's position keeps what was typed, and the field shows it until the page builds the field again.
- When the page builds the field, it shows what the page holds, as the web does after a render (#1026).
- Every reader of a field's text reads that one text, through one function.
- A frame after a key builds what the field's handler marked, with no fallback.

**Non-Goals:**

- A code surface and a spreadsheet. Their text lives in their models, which the paint reads, and they
  keep the fallback for a handler that called no `SetState`: what it guards is a component that shows
  what the model holds, which the host cannot see either.
- A field with no `OnChanged`. On Photon it takes no keys (the caret moves, the text does not), and on
  the web the browser lets a person type into it. That difference is older than this change and is not
  the one #996 names.
- Where the caret lands when the field takes the page's value. It stays where it was, clamped to the
  new text, as before this change.

## Decisions

### How Flutter solves it

`EditableText` keeps the text in its `TextEditingController`: a key changes the controller, and
`onChanged` tells the app. The app's value reaches the controller only when the app sets it, which a
field driven by a value does from `didUpdateWidget`, when its parent configures it again.
`docs/FLUTTER-PARITY.md`'s Element tree row names this as the difference left on Photon.

Here the controller's part is the field's position: it keeps the text, and a new configuration of the
field (the page building it) sets it.

### The page's value, every time the page builds the field

#996 proposed taking the app's value "only when the app hands a different one", which is how the
browser's runtime compared one tree with the last. #1026 found that rule wrong on eQuantic.Auth's
sign-in: the page held `""`, a code was typed, and an answer reset the field to `""` before the frame
that would have drawn the typing. The page held `""` both times, nothing was written, and the typed
code stayed on screen over an empty field. Its fix makes the web show what the page holds after every
render, whether or not the value changed.

Taking #996's rule here would put that defect on Photon. So the field takes the page's value whenever
the page builds it, and keeps what was typed between builds. The two targets then agree on all four
cases of #996's proof: a handler that ignores a key leaves it showing, one that rejects it and calls
`SetState` puts the field back, a formatter shows its output, and a reset shows, even to the value the
page held before the typing.

The position tells a build by the node: a build hands the position a new `TextEntry`, and a position
nothing marked keeps the one it had. The field keeps the node it last took its text from, and takes
the page's value when it is handed another.

### One function, every reader

`FieldText.Of(at, entry)` answers what the field at a position shows, and `FieldText.Typed(at, entry,
text)` records a key. The measure, the paint, the semantics, and every place the host reads the field
(the caret and its clamp, the selection, word and line moves, a click's caret, a triple click, the
phone keyboard's editing value and its composition) call the first. `_edited` goes: the position's text
is what it held, and it no longer ends at the next frame.

The state is internal to `eQuantic.UI.Native.Framework`, on the field's position through `NodeStates`,
as a scroll view's offset is. The measure lives in that assembly and the host, the paint and the
semantics in `eQuantic.UI.Native.Components`, so the two functions are public, on the experimental
surface (`EQX0001`). A position keeps one kind of state, and a `TextEntry`'s keeps no other: a flex
weight's track lives on its `Flexible`, and a box's glide on its box.

### A frame after a key builds what was marked

`Commit` records the key on the position and hands the value to `OnChanged`, and the frame builds what
the handler marked. `RebuildUnlessTold` stays for the two surfaces only.

A frame that rebuilds every component (an invalidation, a resize, a density change, a hot reload)
builds every field too, so a field shows its page's value after one. A page that ignores what is
typed, and so holds the old value, shows the old value then. That is what the web shows after a render
of such a page, and on Photon those frames are the ones that build everything.

## Risks / Trade-offs

- [A handler that rejected a key by ignoring it used to see the key vanish at once on Photon, through
  the fallback.] Now the key shows until the page builds the field, as it does in a browser. The
  migration line is to call `SetState`, which puts the field back on both targets.
- [The web's half of the proof, a rejecting handler, holds only once #1026 is merged.] Until then the
  web keeps the rejected key after a render. The Photon tests state the rule; the web's tests are
  #1026's.
