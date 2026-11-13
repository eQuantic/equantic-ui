## Context

`Select` (`src/eQuantic.UI.Components/Select.cs`) builds an `Anchored` whose panel is a
`AnchorPanelRole.Listbox`: a `Column` of `Pressable { Role = PressableRole.Option }` rows, a flat
`_highlight` index moved by `Shortcut`s on ArrowDown, ArrowUp and Enter, and `ActiveIndex` handed to
the realizers. Its own XML documentation fences "option groups" out of v1.

- The web realizer (`WebLoweringVisitor.Containers.cs`, `LowerAnchored`) makes the trigger the
  combobox, the panel `role="listbox"`, and numbers every `option` row of the lowered panel IN TREE
  ORDER, recursively (`NumberItemRows`, `{panelId}-{n}`); the TypeScript twin does the same
  (`lowering.ts`, `lowerAnchored`, `numberItemRows`). An option nested one element deeper is still
  numbered, in reading order.
- Photon (`SemanticsVisitor.Interaction.cs`) announces the trigger as `SemanticRole.ComboBox` and each
  row as `SemanticRole.Option`; its semantics tree is flat, and a container announces through
  `AnnounceGroup` (one stop, then keep walking).
- `SemanticRole.Group` exists, is appended and bridged (AXGroup, UIKit's accessibility container,
  Android's group). No vocabulary NODE produces it for an author: `Navigable`, `LiveRegion` and a
  `TableViewport` in its `Group` role do, each for its own reason.
- `Select.ts` is the TRANSPILED twin of `Select.cs` (`SharedComponentTranspilationTests`,
  `EQ_UPDATE_TRANSPILED=1`); the hand-written twin is the lowering of the vocabulary.

## How Flutter solves it

Flutter has no grouped select. `DropdownMenu` and `DropdownButton` take a flat list of entries. Its
menus (`MenuAnchor`, `MenuItemButton`) draw sections from a header `Text` and a `Divider`, and say
nothing to a reader about the section: the grouping is paint. A labelled group in Flutter's
semantics is `Semantics(container: true, label: …)`, a `SemanticsNode` produced from the render tree.
`docs/FLUTTER-PARITY.md` rows 154 and 155 (`Semantics`, `SemanticsNode`) cover that mechanism and
mark it SAME and PARTIAL; neither changes here.

Which tree the question is about: the declaration "these options are one group, named X" is a
`Widget`-tree fact (our `VisualNode`, the first tree), and the announcement is produced from the third
tree (`LayoutNode` on Photon, the lowered element on the web). The group needs no parent and knows
none: the walk over the laid-out tree already visits it in place, so nothing moves to `VisualNode`.

Doing better than Flutter: ARIA has the listbox `group`, HTML has `optgroup`, SwiftUI's `Picker` takes
`Section`s, UIKit has the `semanticGroup` container and AppKit `AXGroup`. Every target this SDK draws
on can say the group to a reader, so the vocabulary says it once and both realizers announce it.

## Goals / Non-Goals

**Goals:**

- A Select draws its options under labelled headings, and every reader hears each heading as a group.
- The keyboard walks the options as one list, across the groups; the index is one index.
- One vocabulary word for "a labelled group a reader names and walks into", usable beyond the Select.

**Non-Goals:**

- A choice whose value is the app's (`SelectOption(id, name)`, a generic `Select<T>`): a separate
  capability, left open below.
- Typeahead, disabled options, multi-select, Home/End, dividers between groups, sections in `Menu`.
- An `ExcludeSemantics` node (still a GAP row in `docs/FLUTTER-PARITY.md`). This change takes only
  Flutter's `excludeFromSemantics` property, on `Text` (decision 6).

## Decisions

### 1. The vocabulary takes `Group`, a node (not an attribute of a layout node)

`Group : SingleChildNode` with `string Label` (init, default `""`), `NodeKind => "group"`. It draws
nothing and carries no geometry; it emits a host of its own on the web, so it is measured as
`LiveRegion` is (`MeasureWrapper(..., s.Constraints.Inline(), ...)`) and lowered with the same
`fills`/`capsAt` rule. Factory: `UI.Group(VisualNode child, string label = "")`, mirroring the
constructor with the `Label` tail (`UiFactoryConformanceTests`, `EveryVocabularyNode_IsReachableThroughTheSurface`).

- Why a node: no word in the vocabulary can say it, the argument that made `LiveRegion` a node. A
  `Label` on `Column` would put semantics on a layout node, which every semantics walk treats as pure
  layout.
- Why the word: `SemanticRole.Group` is already ours, and AppKit (`AXGroup`), UIKit
  (`UIAccessibilityContainerType.semanticGroup`) and ARIA (`group`) all use it, so it passes "would
  this word exist if the web did not?". `Section` was weighed and refused: SwiftUI's `Section` draws a
  header and lays out content, and this node draws nothing.
- Why not `optgroup`'s shape on the node: the node is the general one; the Select is its first user.
- Web: `<div role="group" aria-label="{Label}">`. Photon: `AnnounceGroup(new(SemanticRole.Group,
  IdOf(laidOut), laidOut.Bounds, node.Label, null, false))`. Email: walks into the child (both the HTML
  and the text alternative), since the medium has no reader stop to give.
- AN EMPTY LABEL IS NO GROUP, settled at the node and not inside the Select (gate round 1, H2): every
  realizer lets the child stand in its place. The web lowers no host, Photon announces nothing for
  it and measures it as a plain wrapper (no inlined constraints, because no host exists on the web).
  A nameless group is noise to every reader, and a required label would throw on a page whose
  culture's string came back empty. The rule is ONE predicate, `Group.IsNamed`, which every realizer
  reads (gate round 2, R2-H2), and so does the Select, which asks it rather than restating it (R3-7).
  Blank space is no name (R3-8). The predicate is twinned as `Group.isNamed`, since the Select crosses.
  Where it is false the web lowers the child on the parent's axis (R2-D2). A container rule that looks
  for a `Positioned` (a Stack's layer) or an `AdaptiveNode` (a placed child) looks through it as it
  looks through a component (R3-4, `StandsIn`/`standsIn`).
- ONE FUNNEL RULE for every node that lowers to its child's element (a Shortcut, a Simulated, an
  InheritedComponent, a nameless Group), with no Group branch (R3-1, R3-2, R3-3, R3-10). The element
  reaches the funnel once per node, innermost first. The OUTERMOST key wins, because the outermost node
  is the one among the siblings, so keyed siblings keep distinct keys, with or without a key of the
  child's own. A bookmark lands on the element. A second, different id on the same element (another
  bookmark, or an id the realizer gave it) is REFUSED with an exception naming both, because an element
  has one id and a dropped one is a link that silently goes nowhere. Photon keys a path segment per
  node, so both keys hold there (`KeyedNamelessGroupsKeepDistinctIdentities_AndStateFollowsTheirKeys`).
- The interface member `IVisualNodeVisitor.Visit(Group, TState)` makes each visitor that does not
  name it fail the build (CS0535), which is the instrument that fails.

### 2. The component takes `OptionGroup`, a record beside `MenuItem`

`public sealed record OptionGroup(string Label, IReadOnlyList<string> Options);` in
`eQuantic.UI.Components`. The issue's word is kept: it composes two words the vocabulary already owns
(`SemanticRole.Option`, `SemanticRole.Group`), which is exactly what it is announced as on every
platform. A data record has no factory, as `MenuItem` and `NavItem` have none.

### 3. One `Select`, ungrouped options first, then the groups (chosen of three)

Constructor and factory, one shape, no overload:

```csharp
public Select(IReadOnlyList<string>? options = null, int selectedIndex = -1,
    Action<int>? onChanged = null, string? placeholder = null, IReadOnlyList<OptionGroup>? groups = null)
```

`Options` stays `IReadOnlyList<string>` (null becomes empty) and `Groups` is added as
`IReadOnlyList<OptionGroup>` (null becomes empty), both carried by `AdoptConfig`
(`AdoptConfigCoverageTests`). The reading order is `Options`, then each group's `Options`; the index
`selectedIndex` names and `onChanged` reports is the position in that order. Given both, nothing is
invalid: the ungrouped options lead, as options before the first `optgroup` do in HTML.

The three designs weighed:

| Design | What it costs | What it buys |
|---|---|---|
| A, chosen: `options` optional + trailing `groups` | ungrouped options can only LEAD the groups | every flat call in the tree (25 sites, two samples bound to README captures) compiles unchanged; no invalid state; the issue's call is one named argument |
| B: `groups` only, a flat Select is one unlabelled group | every flat call rewritten to `[new OptionGroup("", [...])]`, the common case noisier for the rare one | one parameter |
| C: a closed hierarchy `SelectEntry` (option or group) with an implicit `string` conversion | a base and two records, a user-defined conversion crossing eqc, `Options` changes type | ungrouped options anywhere between groups |

A is the smallest change that serves the request completely; C's only gain (an ungrouped option
between groups) has no consumer.

### 4. One index across the groups

The web's own `HTMLSelectElement.selectedIndex` counts options across `optgroup`s, both realizers
already number the panel's options across any nesting, and `_highlight` is already one integer. So the
grouped Select changes no index arithmetic in the realizers: `ActiveIndex = _highlight` names
`{panelId}-{_highlight}`, the option where it sits. An app that wants an id writes
`var all = [.. open, .. next];` and reads `all[i]`.

### 5. The keyboard

ArrowDown and ArrowUp move over the flat list and clamp at its ends, as today, so they cross a group
boundary without stopping on its heading (a heading is not an option and is not numbered). Enter
chooses the highlighted option; Escape closes (the `Anchored`'s own binding). Opening starts the
highlight on the selected option. No new `Shortcut` and no new chord.

### 6. How each target draws and announces a group

The Select builds, per group with a non-empty label:

```
Group(label) ── Column ── heading Box(Text(label, TypeRole.Caption, theme.TextMuted, maxLines: 1))
                       └─ the group's option rows, unchanged
```

- Heading: `Caption` in `TextMuted`, padded like a row (`PickerPaddingX`) across and `Space.S1` down,
  the RadioGroup's group label style (the sibling imitated). The `Space.S1` vertical padding is the
  engineer's choice until the handoff draws a grouped Select. The handoff (`docs/design/`) draws no grouped Select; it owes the
  drawing, and `Overline` ("the eyebrow over a group or a section") is the alternative it may choose.
- THE HEADING IS READ ONCE, as the group's name (gate round 1, D2). It is drawn as a `Text` with
  `ExcludeFromSemantics = true`, a new `Text` property and Flutter's own word
  (`Image.excludeFromSemantics`). The web writes `aria-hidden="true"` on it, and Photon's semantics walk
  passes over it, also when it derives a control's name from what the control holds (`TextWithin`,
  R2-D1), as the web's `aria-hidden` drops the text from a button's name. The group carries the same
  words as its `Label`.
- A PARAGRAPH WITH A LINKED RUN IS READ whatever `ExcludeFromSemantics` says (R2-H1, settled as
  "ignore"). Every realizer reads it through one predicate, `Text.IsHiddenFromReaders`, which is
  `[ServerOnly]` (R3-0): a page never asks it, and the TS lowering reads the two fields itself.
  `ServerOnlyAttribute` now also targets properties, which the compiler's fence already reads. A link is
  something a reader can follow, and a link hidden from the reader that a keyboard still reaches is
  a trap (`aria-hidden` over a focusable element). Refusing with a throw would take down a page over
  a semantics hint.
- Web: inside `role="listbox"`, a `role="group" aria-label="A seguir"` holding the hidden heading and
  the `role="option"` rows. This is WAI-ARIA APG's grouped listbox, named by `aria-label` instead of
  `aria-labelledby` because the label is a string the node already has.
- Photon: a section heading in the menu; the reader meets `Group "A seguir"`, then the options. The
  web's reading (`AnnouncedTree`) is the same. `GroupSemanticsTests` (web) and
  `ComboBoxAndDialogSemanticsTests` (Photon) hold the reading.
- `SemanticsParityTests` gets NO curated grouped case (gate round 1, D1). Every opened Select already
  differs between the targets in the combobox's name (#1328, baselined for `case select opened`), so a
  grouped case could only pass by growing a baseline that may only shrink.
- A group whose label is empty draws no heading, and its options read as ungrouped (the node's rule
  above).
- The panel id the C# realizer hashes from the panel's text (`TextContentOf`) gains a `Group` arm, so
  a grouped panel's options are part of its id as a flat one's are. The TS twin hashes the path and
  needs nothing.

### 7. The surface that moves

`eQuantic.UI.Primitives/PublicAPI.Unshipped.txt` (added):
`eQuantic.UI.Primitives.Group`, its constructor, `Label.get/init`, `NodeKind.get` and `Accept`
overrides, and `eQuantic.UI.Primitives.IVisualNodeVisitor<TState, TResult>.Visit(eQuantic.UI.Primitives.Group! node, TState state) -> TResult`.

`eQuantic.UI.Components/PublicAPI.Unshipped.txt`:

```text
*REMOVED*eQuantic.UI.Components.Select.Select(System.Collections.Generic.IReadOnlyList<string!>! options, int selectedIndex = -1, System.Action<int>? onChanged = null, string? placeholder = null) -> void
*REMOVED*static eQuantic.UI.Components.UI.Select(System.Collections.Generic.IReadOnlyList<string!>! options, int selectedIndex = -1, System.Action<int>? onChanged = null, string? placeholder = null) -> eQuantic.UI.Components.Select!
eQuantic.UI.Components.Select.Select(System.Collections.Generic.IReadOnlyList<string!>? options = null, int selectedIndex = -1, System.Action<int>? onChanged = null, string? placeholder = null, System.Collections.Generic.IReadOnlyList<eQuantic.UI.Components.OptionGroup!>? groups = null) -> void
eQuantic.UI.Components.Select.Groups.get -> System.Collections.Generic.IReadOnlyList<eQuantic.UI.Components.OptionGroup!>!
static eQuantic.UI.Components.UI.Select(…the same parameters…) -> eQuantic.UI.Components.Select!
static eQuantic.UI.Components.UI.Group(eQuantic.UI.Primitives.VisualNode! child, string! label = "") -> eQuantic.UI.Primitives.Group!
eQuantic.UI.Components.OptionGroup … (the record's lines, the shape `MenuItem`'s have: type, constructor, Label and Options get/init, one Deconstruct, <Clone>$, Equals, GetHashCode, ToString, == and !=)
```

The exact lines are what `./scripts/public-api.sh update` writes; the list above is what to read the
diff against. `tests/eQuantic.UI.Web.Tests/developer-surface.baseline.txt` does not move.

Added in gate round 1: `eQuantic.UI.Primitives.Text.ExcludeFromSemantics.get -> bool` and `.init -> void`. Added in gate round 2: `Group.IsNamed.get -> bool` and `Text.IsHiddenFromReaders.get -> bool`.

### 8. `docs/FLUTTER-PARITY.md` gets no row

The table compares the framework's mechanisms, and no component of the catalogue (`DropdownMenu`,
`MenuAnchor`, `TabBar`) has a row; a row needs a probe in `FlutterParityPinTests`. The mechanism used
here, a labelled semantics container, is row 154/155's, whose verdicts this change does not move.

## Risks / Trade-offs

- [A vocabulary node reaches every visitor] → the build names each one that is missing (CS0535);
  the tasks list them in order, and the wrapper suites (layout transparency, role-bearing host bounds,
  child slots) enumerate by reflection or by kind.
- [The served runtime grows by one class and one lowering] → `ServedRuntimeBudgetTests` measures it;
  a budget move is regenerated with `EQ_UPDATE_RUNTIME_BUDGET` and a sentence on what the bytes bought.
- [Ungrouped options cannot follow the groups] → accepted (design A); an app puts a trailing choice in
  a group of its own, as the issue's "Outro concelho" is.
- [`Text` grows a property] → `ExcludeFromSemantics` is init-only and false by default, so no existing
  text changes. An open Select with no options names no active option (`ActiveIndex` -1, R2-D4). The C# realizer and the TS twin write the same `aria-hidden`, and the parity fixture pins
  it.

## Migration Plan

No source change for any existing call: every `new Select([...], i, f)` and `Select([...], i, f)` in
`samples/`, `tests/` and `src/` compiles as written. Recompile against the new packages. The Upgrading
page records the two `*REMOVED*` signatures with that line.

## Open Questions

Settled while implementing (implement.md says how): `options ?? []` transpiles as written; the two
`NumberItemRows` recurse alike; `EmailWalk` walks a child as `InheritedComponent` does; the wrapper
suites list their wrappers, and `Group` joined each. Below, as first written:

- UNMEASURED: whether eqc transpiles `options ?? []` (a target-typed empty collection expression for
  `IReadOnlyList<string>`) as JavaScript `[]`. If the twin's pin shows otherwise, write
  `Array.Empty<string>()` and file the gap (`docs/PRINCIPLES.md` 14).
- UNMEASURED: that a browser's native `select` moves past an `optgroup` label with the arrows (the HTML
  standard's behaviour, not driven here). The design does not depend on it: the heading is not an
  option, so the flat list skips it by construction.
- UNREAD: whether the TS `numberItemRows` recurses exactly as the C# `NumberItemRows` does (its
  signature was read, its body was not). The parity fixture `select-grouped-opens` answers it.
- UNREAD: whether `EmailWalk` has a helper that walks a single child (`Visit(InheritedComponent …)`
  style); if it has none, `Refuse` is the fallback and the Select is refused in email anyway.
- UNREAD: whether `WrapperLayoutTransparencyTests`, `RoleBearingHostBoundsTests` and
  `role-host-bounds.spec.ts` enumerate role-bearing wrappers by reflection or by a list; if by a list,
  `Group` joins it beside `LiveRegion`.
- OPEN, not this change: a Select whose value is the app's (`SelectOption(value, label)`, the issue's
  sketch). It reshapes `Select`, `SegmentedControl`, `Tabs` and `RadioGroup` together and needs its own
  issue under #190.
- OPEN, design: the handoff draws the grouped Select (heading role `Caption` or `Overline`, a divider or
  none) in `docs/design/`; until it does, the Select uses the RadioGroup's label style.
