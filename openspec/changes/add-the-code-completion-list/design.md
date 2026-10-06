# Design

## How Flutter solves it

`docs/FLUTTER-PARITY.md` has no row for completion, and Flutter has no code editor. Its nearest
pieces are three. `RawAutocomplete` draws its options through an `optionsViewBuilder`, as an overlay
that a `CompositedTransformFollower` keeps glued to the field, and passes the highlighted option down
with `AutocompleteHighlightedOption`. A row of those options is an `InkWell` the keyboard never lands
on (`canRequestFocus: false`), so the field keeps the focus while the person points. And the field
itself tells assistive technology what it offers.

The list here takes all three, and changes where it lives: not a page overlay that follows the
field, but a part of the code surface, in the code's own coordinate space (the plan's §7). A page
overlay cannot escape the editor's scrollers on the web (the vocabulary's `Anchored` says as much
in its fences), and one positioned from outside would trail the code by a frame on every scroll.
Inside the surface the list moves with the code because it is drawn with it.

## The surface offers options

`CodeSurface` grows three members:

- `Options`: a node drawn over the code and over its caret, at `OptionsOrigin`, a point in the
  surface's own coordinates (the same ones `CaretRect` answers in).
- `HighlightedOption`: which of the option rows inside `Options` the keyboard is on, counted in tree
  order over the pressables whose role is `Option`, or -1.

The surface, not a node beside it, is what owns them, because the INPUT is what assistive technology
asks. On the web the input (the `textarea` the surface already keeps at the caret) gets
`aria-autocomplete="list"`, and while options show, `aria-expanded`, `aria-controls` naming the list
and `aria-activedescendant` naming the highlighted option, with the ids numbered the way an
`Anchored` listbox numbers its rows. A node beside the surface could hold the list, never the
pointer to it. On Photon the code field announces as before, and the options under it announce as
options, the highlighted one selected.

## A row that leaves the keyboard where it is

`Pressable.CanRequestFocus`, true by default, is Flutter's `canRequestFocus`. A row of the list sets
it false: on the web the press is cancelled before the browser moves the focus to the row (the
`textarea` keeps it, and the list does not close under the click as the editor loses the keyboard),
and on Photon a press ends no editing and focuses nothing. The press itself still runs: the surface
already lets a pressable drawn inside it take its own press, as a diff's folded row does.

## Placement, and only the rows in view

The list's top left is the start of the word being completed, one line below it: the labels line up
with what was typed, as VS Code's do. The room below is measured against the viewport the editor
already tracks (its offset and height, and, for the right edge, a horizontal offset it tracks now as
well). When a page of rows does not fit below and fits above, the list goes above the line; when
neither fits, it takes the larger side and shows as many rows as fit, never fewer than one. Its right
edge is kept inside the viewport by moving it left. An editor that hugs its code has the code's
height as its viewport, and a two-line editor shows a short list.

The rows are the code's lines: the editor's line height and its code font. The list shows a page of
`PageSize` rows (the engine's page keys step by it) and builds only those, the first one following
the selection as it moves past either end, so a language service's thousand entries cost a page of
nodes per keystroke. A column on the right says where the page is in the whole.

## What a row says

The entry's kind as a glyph, its label with the characters the word matched in the accent colour,
and its detail, muted, after it. The selected entry is washed in the selection colour, and its
documentation, once its provider resolved it, shows under the list.

## What an editor completes from

`CodeEditor.Completions` is the list of providers the editor's completion asks. Null, the default,
is the built-ins: the language's words and the document's. An empty list is no completion. A
read-only editor asks nothing whatever it was given. The editor hands the providers to its
controller's completion once, and again only when the list itself changes, so a session in flight is
not dropped by a rebuild.

## Fences

- A wheel over the list does not scroll it: the arrows and the page keys do, and the visible page
  follows them. A wheel arrives with the view's scroll work.
- The documentation is plain text. Markdown in it arrives with the hover card, which renders the same
  thing.
