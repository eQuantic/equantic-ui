## Why

Closes #1516, a sub-issue of #190 (Flutter parity: the gaps that are work), left open by #677's design
(`openspec/changes/archive/2026-11-13-a-select-groups-its-options/design.md`).

`Select`, `SegmentedControl`, `Tabs` and `RadioGroup` answer an index, so every app that picks one of its
own values keeps a parallel list and casts both ways: `(int)_range` in and `(DateRange)i` out in
`PaymentsPage`, `_mode == ThemeMode.Dark ? 1 : 0` in `StudioShell`, and `CultureSwitcher` does it inside
the SDK. The capability missing is general, not one screen's: a single choice among the app's own values,
of whatever type, answered as that value. Flutter types it (`DropdownMenu<T>` over `DropdownMenuEntry<T>`,
`SegmentedButton<T>` over `ButtonSegment<T>`, `RadioGroup<T>` over `Radio<T>`), and so does Blazor
(`InputSelect<TValue>`).

## What Changes

What a developer writes:

```csharp
SegmentedControl([new(DateRange.Week, "7d"), new(DateRange.Month, "30d")], _range,
    r => SetState(() => _range = r))

Select(options: [.. accounts.Select(a => new Choice<string>(a.Id, a.Name))], value: _account,
    onChanged: id => SetState(() => _account = id), placeholder: "Destination account")
```

No type argument is written: C# takes `T` from `value`. An app with nothing chosen yet declares its field
nullable (`DateRange? _range`) and gets a `Select<DateRange?>` showing its placeholder. The browser and
the native shell draw exactly what they drew; only what crosses the component's boundary changes.

- **BREAKING** `Select`, `SegmentedControl`, `Tabs` and `RadioGroup` become `Select<T>`,
  `SegmentedControl<T>`, `Tabs<T>` and `RadioGroup<T>`: each takes `IReadOnlyList<Choice<T>>`, the selected
  `T value` and `Action<T>? onChanged`. `selectedIndex`, `selected` and `onSelect` go, and
  `Tabs`'s `labels` becomes `items`, its navigation siblings' word. `Select`'s `options` becomes required (`options: []` for an all-grouped one).
- **BREAKING** `OptionGroup(string, IReadOnlyList<string>)` becomes
  `OptionGroup<T>(string Label, IReadOnlyList<Choice<T>> Options)`.
- New `Choice<T>(T Value, string Label)`, the one entry of the four.
- The factories `UI.Select`, `UI.SegmentedControl` and `UI.Tabs` become generic; `UI.RadioGroup<T>` is new.
- The selected entry is the first `EqualityComparer<T>.Default` finds equal to `value`, in .NET and in the
  browser alike. A value no entry carries selects nothing, and the arrows from there land on the first or
  the last entry (today a backward step from no selection lands on the second-to-last).
- `CultureSwitcher` speaks culture names to its segmented control instead of indices; its surface does not
  change.

Migration, the one line: `SegmentedControl(["7d", "30d"], (int)_range, i => _range = (DateRange)i)`
becomes `SegmentedControl([new(DateRange.Week, "7d"), new(DateRange.Month, "30d")], _range, r => _range = r)`,
and the same for `Select`, `Tabs` and `RadioGroup`; `new OptionGroup(label, names)` becomes
`new OptionGroup<T>(label, [new(value, name), …])`.

## Capabilities

### New Capabilities

- `choice-values`: a single-choice component takes the app's values, selects by .NET's default equality
  on both targets, and answers the value chosen.

### Modified Capabilities

- `anchored-panels`: a grouped Select's options are `Choice<T>` entries, and the one list answers the
  chosen option's value instead of its position.
- `screen-reader-parity`: the grouped Select read on both targets is written in the new shape.
- `transpiler-classes`: a generic type's TypeScript twin passes the runtime's type check, as every other
  class's twin already must.

## Impact

- Parts: `src/eQuantic.UI.Components` (the four, `Choice`, `OptionGroup`, `CultureSwitcher`, `UI.cs`) and
  their transpiled twins under `src/eQuantic.UI.Runtime/src/shared/components/`, which the runtime embeds;
  the web realizer and Photon draw the same nodes and do not change.
- eqc: the first generic type of the shared library showed a gap, measured on a scratch `Tabs<T>`: the
  JavaScript, the bundle and the execution held, and the runtime's `tsc` refused the TypeScript twins (a
  generic record's twin names a `T` it never declares, a generic component's imports its `T` from
  `"./T"`). `RecordTypeEmitter` and `TypeScriptEmitter` are fixed in type positions only; the JavaScript
  does not move. No change to the vocabulary, the SDKs, the shells or the templates.
- Public surface: `src/eQuantic.UI.Components`'s API moves (the four, `OptionGroup`, three factories go;
  their generic forms and `Choice<T>` and `UI.RadioGroup<T>` arrive). The developer surface
  (`developer-surface.baseline.txt`) does not move.
- Tests: three reflection guards learn a generic component (`ComponentCatalog`, `FactoryArguments`,
  `UiFactoryConformanceTests`); the catalog-customization baselines shrink by four; about forty test files
  and the runtime's specs move to the new shape.
- Samples: `DefaultUIDashboard` (`PaymentsPage`, `MarkdownScreen`), `WalletMobile`, `PhotonDesktop`
  (`Gallery`, `StudioShell`); their README pictures are recorded again (open question 1 of design.md).
- Principles followed: 2 (the general capability, not the dashboard's), 4 (written once: only the shared
  components change), 5 (`Choice` is no target's word), 6 (Flutter's typed entries, and better: one entry
  for the four, and no sentinel), 9 (.NET's own `EqualityComparer<T>.Default`; guards that fail on a
  generic component instead of skipping it), 13 (the index shape goes, with no member kept), 15 (the entry's
  `Label` stays the name, so #1545's builders and styles fit after these parameters). Bent: none. Principle 14 decided the eqc fix: the gap is fixed in the SDK,
  never worked around by keeping the catalogue non-generic.
