## Context

`Select`, `SegmentedControl`, `Tabs` and `RadioGroup` (all in `src/eQuantic.UI.Components`) take
`IReadOnlyList<string>` and an `int`, and answer `Action<int>`. Every sample that picks one of its own
values keeps a parallel list and casts both ways (`(int)_range` in, `(DateRange)i` out, in
`PaymentsPage`, `StudioShell`; `_mode == ThemeMode.Dark ? 1 : 0` in `StudioShell`). `CultureSwitcher`
does the same internally, from `CultureOption` to an index and back. `Select` is stateful (its
`AdoptConfig` carries every input) and flattens `Options` and `Groups` (`OptionGroup(string Label,
IReadOnlyList<string> Options)`, #677) into one reading order its keyboard, its `Anchored.ActiveIndex` and
its index all count in. The other three are stateless and wrap their row in an `Adjustable` whose arrows
compute `(selected + direction + count) % count`.

Measured facts this design leans on (the report at
`.claude/pipelines/1516-a-choice-answers-the-apps-own-value/design.md` has each measurement):

- The catalog has no generic component today. A scratch `ScratchTabs<T>` with a `ScratchChoice<T>`
  record and a generic factory in `UI.cs` was built, transpiled, bundled and type-checked, then deleted.
  The Components build, the transpilation, the Server's bundle (the component is in `runtime.js`) and the
  JavaScript all held. The runtime's `tsc` refused the twins: eqc's first gap, decision 8. One guard
  failed on the generic type, and two more are read to drop or refuse it: decision 9.
- The browser selects as .NET does: the scratch conformance case (a generic class over
  `IReadOnlyList<Choice<T>>` and `EqualityComparer<T>.Default`) passed 9 of 9 on both sides, over string,
  enum, int, long, Guid, record, nullable enum, decimal, DateTime, NaN, a class, a tuple and a duplicate.
- C# infers `T` from `value` at every call shape proposed here, with target-typed entries
  (`[new("chk", "Checking")]`), a nullable field (`DateRange?` gives `Select<DateRange?>`), a spread of
  LINQ (`[.. accounts.Select(c => new Choice<string>(c.Id, c.Name))]`) and nested target-typed groups.
- An unconstrained `T?` parameter defaulting to `default` is `T` itself for a value type: omitted, it is
  `DateRange.Week` (the first member) and `0` for an `int`.
- `EqualityComparer<T>.Default` in .NET: `"a"` and `"A"` differ, a `Guid` parsed upper case equals the
  one parsed lower case, two records with equal members are equal, NaN equals NaN.

## How Flutter solves it

`docs/FLUTTER-PARITY.md` compares mechanisms and has no row for a catalogue component, so no row applies
or changes. Flutter's answer, from its API (cited, not measured here):

- `DropdownMenu<T>` takes `List<DropdownMenuEntry<T>>`, `T? initialSelection` and `ValueChanged<T?>
  onSelected`; `DropdownMenuEntry<T>({required T value, required String label, …})`. `DropdownButton<T>`
  takes `T? value` and asserts that exactly one item carries it.
- `SegmentedButton<T>` takes `List<ButtonSegment<T>> segments` and `Set<T> selected`;
  `ButtonSegment<T>({required T value, Widget? label, Widget? icon, …})`.
- `RadioGroup<T>` holds `T? groupValue` and `ValueChanged<T?> onChanged`, and each `Radio<T>` its `value`.
- `TabBar` is index-based: it is driven by a `TabController` shared with a `TabBarView`, which pairs each
  tab with a page by position.

Equality is Dart's `==` on `T`. A missing selection is `null`, which Dart's sound `T?` gives for every `T`.

Which tree: "the app's value is the chosen one" is configuration, a `Widget`-tree fact (the first tree,
our component's constructor). The keyboard's highlight is state (the second tree: `Select`'s `_highlight`,
kept by its element). `aria-activedescendant` and the `Adjustable`'s step are produced at lowering (the
third tree). So the value lives on the component and nothing reaches the vocabulary: `Adjustable`,
`Anchored.ActiveIndex` and `PressableRole` stay positions and flags.

.NET's own precedent agrees with Flutter: Blazor's `InputSelect<TValue>` and `InputRadioGroup<TValue>` are
generic components whose `TValue` is inferred from the bound value (cited, not measured). WPF's and MAUI's
`SelectedValue`/`SelectedItem` are `object`, the shape design B below follows and this design refuses.

## Goals / Non-Goals

**Goals:**

- The four take the app's values and answer the chosen one, on the web and on Photon, with no index left
  in their surface.
- An authoring line needs no type argument where the value already says the type.
- The browser selects the entry .NET selects, for every type an app passes.

**Non-Goals:**

- A comparer of the app's (`IEqualityComparer<T>`): `EqualityComparer<T>.Default` is what `List<T>`,
  `Dictionary` and Flutter's `==` use; an app that needs another normalises its value.
- Multi-selection (`SegmentedButton`'s `Set<T>`), disabled entries, typeahead: each stays a fence of its
  component.
- `BottomNavigation`, `NavigationRail` and `Menu`: index-based like Flutter's `NavigationBar`, `NavigationRail`
  and `MenuAnchor`; the issue names four. Open question 3 asks whether the two navigations follow.
- A factory for an app's own generic component: the generator writes none (`FactorySurface` skips a
  generic symbol, `component-factories` spec). Unchanged here; open question 4.

## Decisions

### 1. The four become generic over the app's value, and the index goes

`Select<T>`, `SegmentedControl<T>`, `Tabs<T>`, `RadioGroup<T>`, each `T` unconstrained:

```csharp
Select<T>(IReadOnlyList<Choice<T>> options, T value, Action<T>? onChanged = null,
    string? placeholder = null, IReadOnlyList<OptionGroup<T>>? groups = null)
SegmentedControl<T>(IReadOnlyList<Choice<T>> segments, T value, Action<T>? onChanged = null)
Tabs<T>(IReadOnlyList<Choice<T>> items, T value, Action<T>? onChanged = null)
RadioGroup<T>(IReadOnlyList<Choice<T>> options, T value, Action<T>? onChanged = null, string? label = null)
```

Every one says `value` and `onChanged`, the words of Flutter's `DropdownButton` and of the issue's sketch:
`selectedIndex`, `selected` and `onSelect` go. A list keeps its component's word: `options`, `segments` (Flutter's), and `items` for
`Tabs`, the word of its navigation siblings `BottomNavigation` and `NavigationRail`, since C# refuses a
member named like its type (CS0542, measured on `Tabs<T>.Tabs`) and Flutter's `tabs` cannot be both the
parameter and its property. `Select`'s `options` becomes required: an all-grouped Select
passes `options: []`, because a required `value` must come before the optional parameters and a list that
is optional before a required value would be a second, odd ordering. Rejected: an erased component over
`object` with a typed factory (design B in the report): the factory would stop mirroring its constructor,
`new Select(…)` would answer `object`, and a value of the wrong type would select nothing at run time where
this design refuses it at compile time.

### 2. One entry, `Choice<T>(T Value, string Label)`, for the four

Flutter has one entry type per component because each carries that component's own widget slots
(`ButtonSegment.icon`, `DropdownMenuEntry.leadingIcon`, `labelWidget`, `style`). Here those slots are the
component's `<part>Builder` and `style:` (#1545), never the entry's, so the entries of the four carry the
same two facts. One type also lets the list move unchanged between the components, which
`SegmentedControl`'s own doc asks for ("past five segments … the control becomes a Select").

The name passes the vocabulary rule: a choice exists whether or not the web does, and no target names a
type so (HTML says `option`, AppKit `NSMenuItem`, Android a spinner's item). `Option<T>` was weighed and
refused: it collides with `System.CommandLine.Option<T>` and reads as the functional "maybe a value".
`OptionGroup<T>(string Label, IReadOnlyList<Choice<T>> Options)` keeps its name: it is the group of a
Select's `options`, beside the `Group` node #677 named. `Label` is the entry's name, a string, as CLAUDE.md
says a part with a name keeps: its accessible name, a Select's typeahead key and closed field's text.
`Choice<T>` lives in `eQuantic.UI.Components`, beside `OptionGroup`, `NavItem` and `CultureOption`.

### 3. `value` is required, and "nothing chosen" is the null of a nullable `T`

C#'s unconstrained `T?` is not `Nullable<T>`: measured, `T? value = default` omitted is `DateRange.Week`
and `0`, so an optional value would draw the first enum member selected when the app meant none. So
`value` has no default. An app that may have nothing chosen declares its field nullable, and inference
gives `Select<DateRange?>` (measured); the component defines no sentinel. A value no entry carries selects
nothing (the `choice-values` spec says what each component draws then).

### 4. The selected entry is the first `EqualityComparer<T>.Default` finds equal

`==` does not compile on an unconstrained `T`, and `object.Equals` boxes and skips a typed
`IEquatable<T>`. `EqualityComparer<T>.Default.Equals(entry.Value, value)` is what `List<T>.IndexOf`
answers with. The first match wins, so two entries with one value never both draw selected, where
Flutter's `DropdownButton` asserts (a component cannot fail a running page for it). In the twin, a type
parameter's equality is `'gen'` (`ElementEquality`), the runtime's `sameGeneric`, which dispatches to the
value's own typed `Equals` and otherwise compares by what the value is; the conformance case of task 1
executes the lookup over string, enum, int, long, Guid, record, nullable enum, decimal, DateTime, NaN, a
class and a tuple on both sides; its scratch passed 9 of 9.

### 5. The index stays inside, derived from the value at each build

Each component finds `selected = IndexOf(value)` once per build (`Select`: over `_choices`, the flattened
list it already keeps, settled in its constructor and in `AdoptConfig`), and everything it already does
by position keeps doing it: the row marked `Selected`, the `Anchored.ActiveIndex`, `_highlight`, the
`Adjustable`'s step. Only the boundaries speak the value: `value` in, `onChanged(entry.Value)` out. A
step from no selection lands on the first entry forward and the last backward; today
`(-1 - 1 + count) % count` lands on the second-to-last, which this fixes.

### 6. `Tabs` joins, unlike Flutter's `TabBar`

`TabBar` is positional because a `TabController` pairs it with a `TabBarView`. This catalogue has no tab
view and no controller: its `Tabs` is a controlled single choice drawn as a strip, and the samples use it
as a filter (`All · Income · Expenses · Bills`) with the same casts as the segmented control. The issue
names it among the four. A pager that later pairs with it keys its pages by the same value, or derives the
position the way decision 5 does.

### 7. Generic factories, and `RadioGroup` gains one

`UI.Select<T>`, `UI.SegmentedControl<T>` (with its `stretch` tail) and `UI.Tabs<T>` mirror their
constructors; `UI.RadioGroup<T>` is added, because `new RadioGroup<T>(…)` needs its type argument written
and the factory infers it. The factory and its type share a name and C# resolves the simple name `Select`
to the method (a type of arity 1 does not match a simple name with no type arguments), measured in the
scratch.

### 8. eqc writes a generic twin that type-checks: the first gap the catalogue shows

Measured on the scratch, the runtime's `tsc` (strict, `noUnusedLocals`, `noUnusedParameters`) refused
three things eqc wrote, and nothing else in the pipeline noticed, because an app's modules reach bun,
which strips types, and are never type-checked:

- the record twin `export class ScratchChoice { declare value: T; … }` names a `T` it never declares
  (TS2304), and `UI.ts`'s `scratchTabs<T>(tabs: ScratchChoice<T>[], …)` then names a class that is not
  generic (TS2315);
- the component twin imports its type parameter as a sibling module, `import { T } from "./T"` (TS2307,
  and TS6133 for the unused import), and declares `class ScratchTabs<T>` while every member is `any`
  (TS6133 on the class's `T`).

The JavaScript is right: bun elided the type-only import, the Server's bundle built with the component in
it, and the conformance case executed. So the fix is eqc's and only in type positions
(`RecordTypeEmitter`, and the import collection and class header of `TypeScriptEmitter`): a type
parameter is never an import, a twin declares the type parameters its text writes and only those, and a
reference with type arguments matches the declaration. Principle 14: the gap is fixed in the SDK, not
worked around by a non-generic catalogue; design B (the report) would meet the record half of it too,
since its `Choice<T>` is a generic record. The `transpiler-classes` delta states it.

### 9. The guards learn a generic component, and none skips one

Three reflection guards meet the first generic component of the catalogue:

- `ComponentCatalog.Derive` (native suite, read by the variant matrix and the semantics parity fixture)
  FILTERS `ContainsGenericParameters` (read, not run), so the four would leave both silently. It closes a
  generic definition over `string` instead, names it by its definition without the arity (the pins keep
  `Components.SegmentedControl.txt`), and fails naming a definition it cannot close.
- `FactoryArguments` (linked into both suites) cannot make a value of a type parameter (read, not run: it
  answers "nothing plausible to pass"), so `AdoptConfigCoverageTests` would report `Select<T>` unmeasured,
  which fails rather than skips. The callers close the type and the factory over `string` first.
- `UiFactoryConformanceTests.EveryFactory_IsNamedExactlyLikeItsReturnType` failed on the scratch:
  "Expected factory.Name to be "ScratchTabs`1"". It compares the name without the arity, and closes a
  generic factory before it calls one.

`CatalogCustomizationTests` already strips the arity and walks a generic argument: on the scratch it named
`ScratchChoice` and `ScratchTabs` as owned by no family, the failure any new component meets. Its
baselines move (task 7.2). `DeclarativeFactoryTests`, `FactorySemanticSurfaceTests`,
`ComponentIdentityTests`, `SharedComponentTranspilationTests` and `DeveloperSurfaceContractTests` passed
with the scratch in the tree.

### 10. `CultureSwitcher` speaks names

Its segmented form becomes a `SegmentedControl<string>` of `Choice(option.Name, option.Label)`, its value
the active option's `Name`, and `Switch` takes the name. Its `Menu` form stays index-based (`Menu` is not
one of the four).

### 11. Room for the customization slices (#1606 to #1611)

They add `style:` and `<part>Builder` to these constructors. Both land after `onChanged` and the other
parameters here, and neither collides with a value: a builder stays `Func<int, VisualNode>` over the
entry's position in the list the app gave (for a Select, in reading order), the shape CLAUDE.md fixes, and
`Choice<T>.Label` stays the name the component draws when no builder is given.

## Risks / Trade-offs

- [The first generic component of the catalogue] -> the scratch built, transpiled, bundled and
  type-checked it; decision 8 names the compiler's gap it showed and decision 9 every guard that met it.
  The compiler is fixed first and `Tabs` converted next, so the rest follows a measured path.
- [eqc's fix reaches every generic twin, an app's included] -> it moves type positions only; the
  component pins show the diff, and the JavaScript is unchanged (the `transpiler-classes` scenario).
- [JavaScript erases `T`: a `Select<int>` and a `Select<string>` at one position are one `$typeId`
  (`Select\`1`), so the web adopts the new configuration where .NET's `next is not Select<T>` refuses it
  and builds a new instance] -> both draw the new configuration; the `_highlight` is the only state that
  could survive, and `AdoptConfig` already clamps it. Pinned by no test today; open question 2.
- [Every call site moves: about forty test files, five sample files, the runtime's specs] -> one migration
  line, and each component's call sites move in the commit that converts it, so the tree compiles at every
  commit.
- [`new X(…)` now needs a type argument] -> the samples move to the factory where the line reads better,
  which is the authoring rule anyway; a test keeps `new X<T>(…)` where it constructs on purpose.

## Migration

The one line an upgrading app needs, for the Upgrading page:

`Select(options, selectedIndex, onChanged)`, `SegmentedControl(segments, selectedIndex, onChanged)`,
`Tabs(labels, selected, onSelect)` and `RadioGroup(options, selected, onChanged)` take
`Choice<T>(value, label)` entries, the selected `value` and an `onChanged` that receives the value:
`SegmentedControl(["7d", "30d"], (int)_range, i => _range = (DateRange)i)` becomes
`SegmentedControl([new(DateRange.Week, "7d"), new(DateRange.Month, "30d")], _range, r => _range = r)`,
and `new OptionGroup(label, names)` becomes `new OptionGroup<T>(label, [new(value, name), …])`.

## Open Questions

1. The README pictures of `PaymentsPage` (web), `Gallery`/`StudioShell` (desktop, a Mac) and `WalletApp`
   (iOS on a Mac, Android on an emulator) name files this change edits, so `ReadmeCapturesTests` fails
   until they are recorded. The screens are meant to be unchanged pixel for pixel. The web one can be
   retaken here; the lead decides whether the others wait for a Mac and an emulator or are recorded after
   the variant matrix pins (unchanged bytes) show the components draw what they drew.
2. Whether a test should pin the erased-type adoption of the second risk, or the identity should carry
   the type argument. This design keeps the identity of the open type (`ComponentIdentity` already
   answers `Grid\`1` for `Grid<int>`).
3. Whether `BottomNavigation` and `NavigationRail` follow in their own issue under #190: they meet the same
   parallel list, and Flutter keeps them positional.
4. Whether the app-factory generator should write a factory for an app's generic component, now that the
   catalogue shows the shape: a separate issue under the factories feature.
