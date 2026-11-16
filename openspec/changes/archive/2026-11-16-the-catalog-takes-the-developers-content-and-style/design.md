## Context

The composition half of #1545 already holds: every capability is a node that wraps any child (`Pressable`,
`Draggable`, `Hoverable`, `ContextTarget`, `Sortable`), and `ARealizer_NeverReferencesTheLibraryItRealizes` keeps
the catalog built from the vocabulary an app has. The customization half does not: 28 of 55 components take
their content only as a `string`, two take a builder, one takes a style.

Two mechanisms it needs are on main. The theme slots of #938 (`docs/ELEMENT-TREE-PLAN.md` §3.10): one record
per family (`ButtonTheme`, `InputTheme`, `ListTheme`…), every member nullable, each with a `Merge` that lays one
record over another, resolved in an order whose first step is "the instance's own argument, where the component
has one", which no component but `Markdown` has. And the colour a content takes from the control around it:
`BoxStyle.Foreground`, half of Flutter's `DefaultTextStyle` and `IconTheme` (docs/FLUTTER-PARITY.md, section 3,
the `WidgetStateProperty` row).

How Flutter solves it: a widget in every place a component draws content (`ElevatedButton(child)`,
`ListTile(leading, title, subtitle, trailing)`, `AlertDialog(title, content, actions)`), an `itemBuilder` where a
list repeats, `DefaultTextStyle` and `IconTheme` so a bare `Text` inside a button looks like the button's label,
and one `ButtonStyle` that both the theme (`ElevatedButtonThemeData.style`) and the instance
(`ElevatedButton.style`) hold, resolved per property. The design takes all four, and does better in two places:
a `string` converts to a node, which Dart cannot do, and every family has its style record, where Flutter gives
most components a row of loose properties instead.

## Goals / Non-Goals

**Goals:**

- One shape for content, repeated parts and style, that five family slices apply in parallel without asking.
- The answers to #1545's three open questions.
- A guard that fails on today's tree the moment a component is born, or changed, against the rule.

**Non-Goals:**

- Changing a component. The family slices do that (#1547 to #1551), and the tree's #1532 and #1533.
- The content defaults themselves. They are #1577, which this design cuts.

## Decisions

### 1. Content is a node, named as Flutter names it

Where a component draws what the developer gives it, the parameter is a `VisualNode`. A single content is
`child`; named parts take Flutter's slot names: `leading`, `title`, `subtitle`, `trailing`, `content` (a dialog's
or a banner's body), `actions`, `avatar`, `icon`.

`label` is never content. In this vocabulary `Label` is the accessible name, a string (`Pressable.Label`,
`IconButton.Label`, `TreeView.Label`, `Slider.Label`), and one word cannot hold two meanings, the rule
`VisualNode.Bookmark` was named by. Where Flutter calls content `label` (`Chip`, `InputDecoration`,
`DataColumn`), ours is `child` or `title`.

### 2. A `string` converts to a node (open question 1)

`VisualNode` gains `implicit operator VisualNode(string text)`, which makes a `Text` that names nothing of its
own, so `Button("Save")`, `ListItem("Inbox", "3 new")` and `Dialog("Delete?", "It cannot be undone.")` read as
they do today. The conversion crosses the transpiler as every vocabulary conversion does, to a static of the
runtime's twin (`IconGlyph.fromIcons` is the precedent).

Rejected: every call names `Text(...)`, as Flutter's do. Dart has no user-defined conversion and C# does, and
the SDK already uses it where a value has one obvious node (`IconGlyph` from `Icons`, `SizeValue` from a
number). It would also rewrite every call site in the samples, the tests and the wiki's five languages for no
gain in what a developer can say. Rejected: an overload per shape. A factory has none, because the twin is
JavaScript, and two shapes of one parameter are the old shape kept alive (CLAUDE.md, the preview rule).

### 3. The content takes the look its component resolved (#1577)

A text with nothing of its own inside a button must draw as the button's label: its type by size, its colour by
variant and state, one line. The colour already follows (`BoxStyle.Foreground`). #1577 adds the rest on the same
box: the text style (role, override, line limit, alignment) and the icon size the content takes when it names
none, Flutter's `DefaultTextStyle` and `IconTheme`. A `Text` whose role is left out takes the nearest one, and
`BodyL` where none is stated, so every tree written today draws as it does. A node that names its own wins.

Rejected: the component restyles a `Text` it was handed. It reads the developer's tree, and misses a `Text`
inside a `Row` beside an icon. Rejected: a separate inherited node for the type, beside the box that already
states the colour: one look, one place.

### 4. The accessible name comes from the content (open question 2)

A part's name is the text of its content, read as both targets already derive one: Photon gathers every `Text`
under a control with no `Label` (`TextWithin`), and the web takes a control's name from its content. A component
whose content can carry no text (an icon alone) takes `label: string?` for its name, kept as text by nature.

An item that has a NAME (an option, a tab, a destination, a menu item, a tree node) keeps it as a `string`: it is
the item's accessible name, its typeahead key and the closed field's text, which no node can be. The builder
(decision 5) draws what the item shows, and the component draws the name when no builder is given. This is
Flutter's `DropdownMenuEntry`, which takes a `label` string for the name and filtering and a `labelWidget` for
the look.

### 5. What a component repeats has a builder

An option, a tab, a destination, a crumb, a menu item, a table's header or cell, a list's row and a tree's row
are drawn by a builder named after the part: `optionBuilder`, `tabBuilder`, `destinationBuilder`,
`crumbBuilder`, `itemBuilder`, `headerBuilder`, `cellBuilder`, `rowBuilder`. It is a `Func<int, VisualNode>`
over the part's index (`Func<int, int, VisualNode>` over row and column for a cell), as `ListView`'s
`itemBuilder` already is.

The builder draws the part's CONTENT; the component keeps its chrome: the selection mark, the indicator, the
focus ring, the hover and the pressed fill, and the state reaches the content through the look the component
hands down (decision 3), as Flutter's tabs and menu items take their colour per state. So the builder takes
the index and nothing else.

Rejected: a node per item record (`labelWidget` on each option). Two places to draw one part, and a list built
from data reads better as one function than as a projection into records. Rejected: a builder that also takes
the part's state. The state belongs to the chrome, which stays the component's.

### 6. The instance's style is the family's slot record, and it is the whole style (open question 3)

A component takes `style:`, of its family's slot record, and resolves
`var slot = ButtonTheme.Merge(theme.Button, Style)`: the first step of §3.10's order, as `Markdown` already does
with `MarkdownTheme`. The record is the WHOLE instance style. A look an instance needs that the record lacks (a
button in the brand's other colour) is added to the record, where the theme can set it for a subtree too; it is
never a property of one component. Where the theme needs the member per variant, the record holds it as a table
keyed by `Variant`, which §3.10 already allows, and the instance names it for the variant it has.

The records keep their names (`ButtonTheme`, not `ButtonStyle`). `Markdown.Style` is a `MarkdownTheme` already,
and renaming eighteen records would touch every family slice's files at once.

Rejected: loose properties per component (`Chip(backgroundColor:, labelStyle:)`), Flutter's older shape, which
multiplies with every component and that no theme can set; Material 3 moved buttons to `ButtonStyle` for that
reason. Rejected: a style type per component beside the family record: two records of one family's look, merged
in two places.

### 7. All of it is a constructor parameter

A factory mirrors its constructor, parameter for parameter (`UiFactoryConformanceTests`), so content, builders
and `style` are optional named constructor parameters. An `init` member (`Button.Leading` today) is reached only
by `new Button(...) { Leading = ... }`, which the declarative surface does not write. A family slice moves the
members it changes into the constructor.

### 8. The guard

`CatalogCustomizationTests` reads the catalog: every assembly beside the test that references the vocabulary and
ships a public, non-abstract `UiComponent` (today `eQuantic.UI.Components`, `eQuantic.UI.Charts` and
`eQuantic.UI.Spreadsheets`; a test fails when the scan finds fewer). For each component, and each item type of
the catalog it takes through a constructor parameter or a property (an option, a column, a node of a tree), it
names:

- a member that carries text: a settable property declared by the catalog (never the vocabulary's own `Key`), or
  a constructor parameter no settable property of its name carries, whose type is a `string`, a sequence of
  text, or a delegate that answers text;
- a component without a style of its own: a constructor parameter `style` and a property `Style` of one type,
  which is a slot record (a type `IAppTheme` answers with a `Merge`).

Each is an entry of a list written by hand, which may only shrink, as `one-type-per-file.baseline.txt` and
`adopt-config.baseline.txt` are. The reason names the issue that removes the entry (`#N`), or begins `kept:` and
says why the text is text by nature: a field's value, an accessible name, an item's name (decision 4), an
identifier, an address, a source to parse. A category waved through ("a `Label` is an accessible name") would
hide the careless one inside it, which is why every entry is a line.

The list is ONE FILE PER FAMILY, because five slices shrink it in parallel and one shared file would conflict on
every pair of them. A file's `# owns:` line names its components and the item types that carry text through
them, every component of the catalog is owned exactly once, and an entry sits in its owner's file. So a new
component cannot pass until someone places it in a family, and its text and its missing style are then entries
someone wrote. The regenerator (`EQ_UPDATE_CATALOG_CUSTOMIZATION=1`) drops what is no longer measured and never
writes an entry.

### 9. The families, re-cut by slot record

Each component that no family slice named joins the slice whose theme slot styles it (§3.10's table), so the
slice that adds `style:` to a record's components adds it to all of them:

| File | Slice | Components |
|---|---|---|
| `buttons-selection-and-progress` | #1547 | Button, FormSubmit, IconButton, Chip, Checkbox, RadioGroup, Switch, Slider, ProgressBar, PullToRefresh, Skeleton |
| `inputs-and-pickers` | #1548 | TextInput, SearchField, Select, FormInput, FileUpload, CultureSwitcher, DatePicker, TimePicker, DateTimePicker, Calendar, SegmentedControl, Stepper |
| `navigation` | #1549 | Tabs, AppBar, BottomNavigation, NavigationRail, Breadcrumb, Pagination, PageIndicator |
| `dialogs-menus-and-notices` | #1550 | Dialog, BottomSheet, Drawer, Popover, Menu, ContextMenu, Tooltip, Toast, Banner, CookieConsent, EmptyState, Badge, Avatar |
| `lists-and-tables` | #1551 | ListItem, List, ListView, ListDetail, Divider, Accordion, SwipeableRow, Card, Table, TableView, DataTable |
| `tree` | #1532, #1533 | TreeView |
| `code-and-documents` | none | CodeBlock, CodeDiff, CodeEditor, Markdown, Mermaid |
| `charts` | #1545 | BarChart |
| `spreadsheet` | #1545 | Spreadsheet, FormulaBar, NameBox, StatusBar |

Code and documents are made of text, and their look is the theme's code palette, which no slot record carries:
every entry there is `kept:`, except `Markdown`'s style, whose constructor does not take it yet. The charts' titles
and the spreadsheet's style are owed with no slice of their own, so their entries cite the parent.

## Risks / Trade-offs

- [A `string` that silently becomes a node where a node was not expected, as in `Column(children: ["a"])`] → the
  node it becomes is the one an author would have written, a `Text` in the look around it, and a `string` is
  accepted only where a node is.
- [An item's name is `kept:`, so the guard cannot see whether its builder arrived] → the entry's reason names
  the slice that adds the builder, and the slice's review reads it; the builder's existence is a constructor
  parameter a test of that slice asserts.
- [A delegate that answers text, a formatter, is listed like content] → it is one line with `kept:` when the
  text is a value in words, and one line is the price of not waving a category through.
- [#1577 lands after a family slice wants its content as a node] → the family slice starts with its style and
  builders, which need nothing new, and takes its content after #1577.

## Migration Plan

Nothing migrates in this slice. Each family slice widens its constructors, fixes every call site and deletes
what the change replaced, with its `*REMOVED*` lines in the release notes and one migration line per renamed
parameter (`label:` becomes `child:` where a call named it).

## Open Questions

None for the family slices. Whether the code surfaces get a slot record of their own, and the charts' and the
spreadsheet's owed entries, are #1545's to cut.
