## Context

The keyboard's sort (#1536) says each step through one polite live region on the web. Its words are eight keys
of `SdkResources` (`SortInstructions`, `SortPickedUp`, `SortMoved`, `SortMovedInto`, `SortRefused`, `SortDropped`,
`SortDroppedInto`, `SortCancelled`), which `dom/sortable.ts` reads from the runtime's culture catalog by key. The
Photon host (#1537) carries an item by the same keys and says nothing: `eQuantic.UI.Native.Components` does not
reference `eQuantic.UI.Components`, where the SDK's words live, and it must not, since a realizer never knows the
catalog (`ARealizer_NeverReferencesTheLibraryItRealizes`). An app has no way to put its own words in either.

## Goals / Non-Goals

**Goals:** the words travel as data on the `Sortable` node, the SDK's by default and the app's when it gives them;
both realizers read them from the node; Photon speaks every step of a keyboard carry on macOS, iOS and Android.

**Non-goals:** the instructions as a hint on Photon (#1596, a `SemanticNode` hint every shell must carry); the look of
the lift (`SortableTheme`, #1545); speaking a pointer's carry, which neither target does.

## Decisions

### How Flutter does it

Flutter's `ReorderableListView` says nothing while an item moves; its accessible path is semantic actions ("Move
up", "Move to the end") whose labels come from `MaterialLocalizations`, which a widget reads from its context and
an app replaces with its own `LocalizationsDelegate`. The words are the component library's, reached through the
tree, and replaceable by the app. Here the same split holds with the decision taken on #1564: the component library
(`UI.Sortable`) puts its words on the node, the realizer reads only the node, and the app replaces them through the
same argument. `docs/FLUTTER-PARITY.md`'s sortable row stops calling Photon's silence a gap.

### A record of texts with numbered holes, written once

`SortAnnouncements(Instructions, PickedUp, Moved, MovedInto, Refused, Dropped, DroppedInto, PutBack)`, eight
strings, each with the holes .NET's composite format numbers: `{0}` the item's name; for a place, `{1}` its position
from one, `{2}` the count and `{3}` the list's name; for a place inside, `{1}` the item it lands inside. They are the
`SdkResources` texts as they are, so `SdkStrings.Sorting` is a straight read of the eight keys.

`SortWords.Fill(text, holes)` fills them in one pass: each `{n}` takes the n-th hole, so a hole's own value is never
read as a hole (a file called `{1}.txt`), a hole the text never names is left out, and a `{n}` past the holes stays as
it is written. It is not `string.Format`, which throws on a stray brace and would let an app's words crash a key
press. `SortWords` lives in `Primitives/Sorting/`, which the runtime embeds as `SortPlacement` already is, so the
browser's controller runs the transpiled `Fill` and the Photon host the C#: one rule for both.

The record itself lives beside `SortDrop` in `Primitives/Nodes/`, with a hand-written mirror in the runtime, as
`SortDrop` has. In `Sorting/` it would be compiled from source into the runtime's twin build beside the Primitives
assembly that already holds it, and `Sortable`'s constructor, which takes the assembly's type, would stop binding
there: the transpiler then emits `Object.assign(new Sortable(…))` with no model, and a method group handed to it
loses its `this` (the tree's `Dropped`, found by its twin's diff). `SortPlacement` escapes that because no member of
the assembly takes one.

Alternatives: a `Func<SortStep, string>` would let an app compute words, but a function cannot cross to the
browser's controller as data on an element, and the steps are sentences, not computations. A text per step on the
node directly (eight properties of `Sortable`) would make the node's shape the catalog's; the record keeps them
one value the factory hands down.

### A constructor parameter, filled by the factory

`Sortable(child, id, onDropped, announcements = null)`. The factory mirrors the constructor, so `UI.Sortable` takes
`announcements` in the same place and passes `announcements ?? SdkStrings.Sorting`. A null record says nothing on
either target: the realizers have no words of their own to fall back on, and the factory, the authoring path, never
leaves it null. It is a constructor parameter rather than an init property because the factory's tail must apply
the property's own default (`UiFactoryConformanceTests`), and the default here is a culture's words that only the
component library knows.

### The web: words on the list's element

The lowering, C# and TS alike, writes each text as `data-eq-sort-say-<step>` on the list's element, beside the
marks the controller already reads. The controller builds the record back from them for the list the item was picked
up from and fills it with the twin's `Fill`. Every step of a carry, the ones in another list of the group included,
speaks in that list's words: one carry, one voice, as Photon's one live region. The focused item's description is
its own list's instructions; a list with none describes nothing.

Attributes rather than a property on the element, because the server renders the list and the client hydrates it:
an attribute is in the HTML both write, and the parity fixture compares them.

### Photon: the source list's live region

A live region on Photon is recorded by the emit walk (`LiveRegionMark`) and diffed once a frame by
`LiveRegionAnnouncer`, which owns arrival, the quiet window and what a region owes; macOS and iOS post what it
produces, and Android reads a live node's words from `LiveRegionText`. A spreadsheet's surface speaks through it
with words of its own (#1081). The sortable takes the same path:

- the host keeps what the carry last said on the source list's position (`SortVoiceState`), set at each step;
- `EmitSortable` records the list as a polite live region whose text is that, while it has one;
- the semantics walk makes the list's node live while it has one: a named list is its labelled group, and an
  unnamed one, which reads as nothing of its own, becomes a group with no name for as long as it speaks, so Android
  has a node to report.

The words stay after the drop, as the web's region keeps its last words: cleared at once, a reader would lose the
drop said inside a quiet window, and Android reads the node's words after the event, not with it.

An item's name is what a reader hears it called: the labels of what its subtree says, joined by a space, as the
web takes an item's accessible name or its text.

## Risks / Trade-offs

- [Eight attributes on every sortable list's element in the HTML] → a few hundred bytes per list, the words of one
  carry; a board's columns pay it once each. The alternative, one JSON attribute, would need the C# and TS encoders
  to agree byte for byte on non-ASCII text.
- [An unnamed list's semantics gain a group while it speaks] → the group has no name and holds what it held, so a
  reader walking it hears the carry's last words where it heard nothing before; that is where the web's status is
  too, a region at the end of the page.
- [A carry across lists speaks in the source list's words] → lists of one group are one app's, in one language.
