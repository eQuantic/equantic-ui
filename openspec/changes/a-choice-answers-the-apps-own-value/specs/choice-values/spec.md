## ADDED Requirements

### Requirement: A choice answers the value the app gave its entry

`Select<T>`, `SegmentedControl<T>`, `Tabs<T>` and `RadioGroup<T>` SHALL take their entries as
`Choice<T>` values, each the app's own value and its name, and the selected value as `T value`, and
SHALL hand `onChanged` the `Value` of the entry chosen, by a press or by the keyboard, on the web and on
Photon. No member of the four SHALL take or answer an index. An entry's `Label` SHALL be what the
component draws for it when no builder draws it and its accessible name, as each component's string was
before.

#### Scenario: A segmented control of an enum, pressed

- **WHEN** `SegmentedControl(segments: [new(DateRange.Week, "7d"), new(DateRange.Month, "30d"), new(DateRange.Quarter, "90d")], value: DateRange.Week, onChanged: chosen.Add)`
  has its segment `30d` pressed, on the web through `ComponentTester` and on Photon through `PhotonHost`
- **THEN** `chosen` holds `DateRange.Month` on both targets, and the segment `Selected` before the press
  is `7d`

#### Scenario: A Select of the app's ids, chosen with the keyboard

- **WHEN** `Select(options: [new("chk", "Checking"), new("sav", "Savings")], value: "sav", onChanged: chosen.Add)`
  is laid out, opened, and ArrowUp and Enter are pressed, on both targets
- **THEN** the closed field shows `Savings`, the highlight opens on `Savings`, and `chosen` holds `"chk"`

#### Scenario: A tab and a radio answer their values

- **WHEN** `Tabs(items: [new(Feed.All, "All"), new(Feed.Mine, "Mine")], value: Feed.All, onChanged: chosen.Add)`
  has `Mine` pressed, and `RadioGroup(options: [new(Ship.Standard, "Standard"), new(Ship.Express, "Express")], value: Ship.Standard, onChanged: chosen.Add)`
  has `Express` pressed, on both targets
- **THEN** `chosen` holds `Feed.Mine` and then `Ship.Express`

#### Scenario: The factory takes its type from the value

- **WHEN** a component writes `Select(options: [new("chk", "Checking"), new("sav", "Savings")], value: account, onChanged: a => account = a)`
  with `string account`, and `SegmentedControl([new(DateRange.Week, "7d")], range, r => range = r)` with
  `DateRange range`
- **THEN** both compile with no type argument, as `Select<string>` and `SegmentedControl<DateRange>`: C#
  infers `T` from `value` and the target-typed entries take it (measured with the .NET 10 SDK)

### Requirement: The selected entry is the first whose value .NET's default comparer finds equal

The entry drawn as selected SHALL be the first, in reading order (for a `Select`, its ungrouped options
and then each group's), whose `Value` `EqualityComparer<T>.Default` finds equal to `value`; a second
entry carrying the same value SHALL never be drawn selected. The component's twin in the browser SHALL
find the same entry as .NET for every type an app passes: a string by its ordinal text, an enum and an
integer by their value, a `Guid` by its value whatever case it was parsed from, a record by its members,
a `double` NaN equal to NaN, and a class by its own `Equals` or else by reference.

#### Scenario: The lookup executed on both sides

- **WHEN** the conformance suite runs a generic class that holds `IReadOnlyList<Choice<T>>` and answers
  the first index whose value `EqualityComparer<T>.Default.Equals` the given one, over `string` ("b" in
  `a`, `b`; "A" in `a`), an enum, `int`, `long`, a `Guid` parsed upper case looked up lower case, a record
  `P(3, 4)`, `Level?` with a null entry, `decimal` `1.0m` looked up as `1.00m`, a `DateTime`, NaN, a class
  without `Equals` and a tuple
- **THEN** the browser answers what .NET answers for every case: `1`, `-1`, the enum's index, the
  Guid's index, the record's index, the null entry's index, `0` for the decimal, the DateTime and NaN,
  the same instance found and another one not, and the tuple found

#### Scenario: Two entries with one value

- **WHEN** `RadioGroup(options: [new(Level.High, "High"), new(Level.High, "Again")], value: Level.High)` is
  laid out on both targets
- **THEN** `High` is the one radio whose check is on

### Requirement: A value no entry carries selects nothing

When `value` equals no entry's value, a `Select` SHALL show its placeholder in the closed field, mark no
option `Selected`, and open its highlight on the first option; a `SegmentedControl` SHALL draw no thumb,
a `Tabs` no indicator, and a `RadioGroup` no checked radio, and none of the three SHALL mark an entry
`Selected`. Every entry SHALL stay pressable. From no selection the arrows SHALL hand the first entry's
value for a forward step and the last entry's for a backward one. An app says "nothing chosen" with the
null of a nullable `T` (`DateRange?`, `string?`, `Guid?`), which C# infers from the app's own field; the
four define no sentinel of their own.

#### Scenario: An empty Select of a nullable enum

- **WHEN** `Select(options: [new(DateRange.Week, "7d"), new(DateRange.Month, "30d")], value: range, onChanged: chosen.Add, placeholder: "Range")`
  is laid out with `DateRange? range = null`, then opened and Enter is pressed, on both targets
- **THEN** it is a `Select<DateRange?>`, the closed field shows `Range`, no option is `Selected`, and
  `chosen` holds `DateRange.Week`

#### Scenario: The arrows from no selection

- **WHEN** a `SegmentedControl` of `7d`, `30d` and `90d`, given a value none of them carries, takes one
  backward step of its `Adjustable`, and another such control one forward step, on both targets
- **THEN** the first hands the value of `90d` and the second the value of `7d`

### Requirement: The keyboard walks the entries by position and answers a value

The arrows of a `SegmentedControl`, a `Tabs` and a `RadioGroup` SHALL move the choice from the selected
entry to the next or the previous one in the order the app gave them, wrapping at the ends, and SHALL hand
that entry's value. The `Adjustable`, the `Anchored`'s `ActiveIndex` and a `Select`'s highlight SHALL stay
positions in the drawn list, derived from the value at each build; the vocabulary SHALL take no value of
the app's.

#### Scenario: A tab strip wraps forward

- **WHEN** `Tabs(items: [new("a", "One"), new("b", "Two"), new("c", "Three")], value: "c", onChanged: chosen.Add)`
  takes one forward step on both targets
- **THEN** `chosen` holds `"a"`
