## Why

Closes #1142, a task under the epic #160 (instruments that fail, not warn). A `StatefulComponent` is kept by
position across its parent's rebuilds, and it learns what the parent hands it this time only through
`UiComponent.AdoptConfig`, whose default copies nothing. Each component overrides it by hand and lists what it
copies, and nothing checks the list. When `CodeEditor` gained `Diagnostics`, `DiagnosticProviders` and
`HoverProviders` (#1008), none of the three went into its `AdoptConfig`, and every test passed until one handed
a second configuration.

A test that hands every shipped component a second configuration, one input at a time, found the same hole in
ten more of them:

- `Disabled` is not carried by `DatePicker`, `DateTimePicker`, `TimePicker`, `Select` and `TextInput`: a form
  that disables its fields while it submits leaves them enabled.
- `TextInput` also keeps its first `Autofocus`, `Obscure` and `OnFocusChanged`, so a password field a parent
  reveals stays obscured and a form that starts listening for focus never hears it.
- `CookieConsent` has no override at all: new copy or a new policy link never reaches the card.
- `ListView` keeps its first `Overscan`, `Width` and `Height`, `Menu` its first `Placement`, `Accordion` its
  first `Multiple`, and `Calendar` its first `Label`.

## What Changes

For a developer using the SDK, nothing to write: a setting a page hands one of the SDK's components on a
rebuild reaches the component on screen, as the first one did.

- **Every input the nine components dropped is carried.** An `init` setting is backed by a field
  `AdoptConfig` writes, as `ProgressBar` already does for its label, so the public surface stays as it is.
  `CookieConsent`, whose every setting is `init`, keeps the fresh instance whole as its configuration and
  reads from it, as `TableView` and the spreadsheet's parts do.
- **eqc writes a component's field of a vocabulary struct as it writes a property of one.** A field of a
  struct whose twin cannot build its zero (`SizeValue`), declared with no initializer, was written
  `_width: SizeValue = undefined`, a value `tsc` refuses for the type it names. It is the slot left alone
  now (`_width!: SizeValue;`), as a property of the same type always was. `ListView`'s width and height
  were the first fields of that shape.
- **A test fails when a shipped component drops an input.** `AdoptConfigCoverageTests` finds every concrete
  `StatefulComponent` in the assemblies beside it, varies each public `set` or `init` property and each
  constructor argument alone on a fresh instance, lets a kept one adopt it, and fails naming the input the
  kept one does not answer. An input read once on purpose is written in a baseline with its reason, by hand,
  and the baseline may only shrink: `CodeEditor.InitialCode` opens the document, and an `Accordion`'s
  `openIndex` is the section open when it first appears.

The parts it reaches: the component library (`eQuantic.UI.Components`) and, through it, the runtime it ships
in; eqc's emitter of a component's fields; the web and compiler tests. The public surface gains one member, `CookieConsent.AdoptConfig`; the developer surface does
not move. Nothing an app wrote breaks.
