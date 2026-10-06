# Design

## How Flutter solves it

`docs/FLUTTER-PARITY.md` had no row for completion, and Flutter has no code editor. Its nearest
pieces are three. `RawAutocomplete` draws its options through an `optionsViewBuilder`, as an overlay
that a `CompositedTransformFollower` keeps glued to the field, and passes the highlighted option down
with `AutocompleteHighlightedOption`. A row of those options is an `InkWell` the keyboard never lands
on (`canRequestFocus: false`), so the field keeps the focus while the person points. And the field
itself tells assistive technology what it offers.

The list here takes all three, and changes where it lives: not a page overlay that follows the
field, but a part of the code surface, in the code's own coordinate space (the plan's §7). A page
overlay cannot escape the editor's scrollers on the web (the vocabulary's `Anchored` says as much
in its fences), and one positioned from outside would trail the code by a frame on every scroll.
Inside the surface the list moves with the code because it is drawn with it. Both rows are in
`docs/FLUTTER-PARITY.md` now.

## The surface offers options

`CodeSurface` grows three members:

- `Options`: a node drawn over the code and over its caret, at `OptionsOrigin`, a point in the
  surface's own coordinates (the same ones `CaretRect` answers in).
- `HighlightedOption`: which of the option rows inside `Options` the keyboard is on, counted in tree
  order over the pressables whose role is `Option`, or -1.

The surface, not a node beside it, is what owns them, because the INPUT is what assistive technology
asks. On the web, while options show, the input (the `textarea` the surface already keeps at the
caret) gets `aria-autocomplete="list"`, `aria-controls` naming the list and
`aria-activedescendant` naming the highlighted option (and no `aria-expanded`, which ARIA allows on a
combobox and not on the textbox a textarea is), with the ids numbered the way an `Anchored`
listbox numbers its rows. A press on the list is the list's on both targets: it never reaches the
code under it, which would move the caret. A node beside the surface could hold the list, never the
pointer to it. On Photon the code field announces as before, and the options under it announce as
options, the highlighted one selected.

## A row that leaves the keyboard where it is

`Pressable.CanRequestFocus`, true by default, is Flutter's `canRequestFocus`. A row of the list sets
it false: on the web the press is cancelled before the browser moves the focus to the row (the
`textarea` keeps it, and the list does not close under the click as the editor loses the keyboard),
the row leaves the Tab order, and on Photon a press ends no editing, focuses nothing and makes no Tab
stop. The press itself still runs: the surface already lets a pressable drawn inside it take its own
press, as a diff's folded row does. The declarative `Pressable(…)` takes it too, since a control that
is pressed beside the text it acts on (a formatting toolbar's button) is an author's need, not one
component's machinery.

## Placement, and only the rows in view

The list's labels line up with the start of the word being completed, one line below it, as VS Code's
do: the list stands as far left of the word as its labels are inside it. The room below is measured
against the viewport the editor already tracks (its offset and height, and, for the right edge, a
horizontal offset it tracks now as well) and against the surface, since the scrollers around the
surface clip whatever leaves it. When a page of rows does not fit below and fits above, the list goes
above the line; when neither fits, it takes the larger side and shows as many rows as fit, never fewer
than one. Its right edge is kept inside the viewport by moving it left. An editor that hugs its code
has the code's height as its viewport, and a two-line editor shows a short list.

A bounded editor's code was as tall as the file, so a short file in a tall pane left the list a few
lines of room, and a press under the last line landed on the scroll view around the code (#599). The
block takes a floor (`CodeBlock.MinHeight`), which a bounded editor sets to its viewport, so the code
fills the pane and the room under the last line is the code's: a press there is the end of the
document, which the engine now answers for any point under the last line. A hugging editor sets no
floor, since its viewport is its code and a floor would keep it from shrinking.

The rows are the code's lines: the editor's line height and its code font. The list shows a page of
up to twelve rows (the engine's `PageSize` is set to the rows shown, so the page keys step by them)
and builds only those, the first one following the selection as it moves past either end, so a
language service's thousand entries cost a page of nodes per keystroke. A column on the right says
where the page is in the whole, and its room is kept whether or not the whole list shows, so the list
does not narrow as the word filters it. The list is as wide as its widest entry needs, within 24 to 60
columns, and only widens while it shows.

## What a row says

The entry's kind as a letter, in the colour the code gives what it names (a type's letter the type
colour, a keyword's the keyword colour); the kinds a code editor draws with one icon share a letter, as
VS Code's icons share a glyph. Then the label, with the characters the word matched in the accent
colour, and the detail, muted, after it. The row is named by its label and its detail, so the letter is
not read out. The selected entry is washed in the selected coat a `Select` uses, and its documentation,
once its provider resolved it, shows as up to four lines of plain text on the side away from the line:
under the list when it stands below the line, over it when it stands above, so the rows stay against
the line whatever the documentation's length.

## What an editor completes from

`CodeEditor.Completions` is the list of providers the editor's completion asks. Null, the default,
is the built-ins: the language's words and the document's. An empty list is no completion. A
read-only editor asks nothing whatever it was given. The editor hands the providers to its
controller once, and again only when the list holds other providers, compared one by one, so a
parent that builds a new list around the same providers on every build changes nothing, and a
session in flight is not dropped by a rebuild.

## On the way

- eqc refused any member access named `Value` or `HasValue` before it asked the model, to leave
  `Nullable<T>`'s two members alone, and an enum member of that name went out as a property read
  (#631). The names now count only where the model cannot answer.
- The list's view is the shared library's first helper class to take the build context, and the
  runtime's build refused it three ways (#632): the context's annotation was `RenderContext`, a name
  only the app-facing exports carry, and nothing imported it; the context's `typeScale` was optional
  where C# has a float; and the runtime's `TypeStyle` lacked `ScaledSize` and `ScaledLineHeight`. The
  context is `BuildContext` on every path and its helper's module imports it, `typeScale` is required
  as `density` is, and the two measures are pinned against values measured on .NET.

## Fences

- A wheel over the list does not scroll it: the arrows and the page keys do, and the visible page
  follows them. A wheel arrives with the view's scroll work.
- The documentation is plain text. Markdown in it arrives with the hover card, which renders the same
  thing.
- The list lives inside the editor's viewport, where an overlay would escape it.
- Photon lays a bordered box's child over its border, where the web insets it (#629): the list's labels
  stand one border's width left of the word there until it is fixed, and the placement, which is the
  component's, is what the tests pin.
- Under `Density.Comfortable` (a phone's), a row's §08 touch margin reaches over the row above it and
  takes its presses (#630). The desktop shells run `Compact`, where a press lands where it is aimed.
- A null-conditional read is `undefined` in the browser where C# answers `null` (#633): the list's
  view takes the documentation the editor already read rather than reaching for it through `?.`.
