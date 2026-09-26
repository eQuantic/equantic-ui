# runtime-dictionaries Specification

## Purpose
What a C# `Dictionary`, `IDictionary` or `IReadOnlyDictionary` answers on the web: the order it
enumerates in, the type of its keys, how a key compares, what its members answer, and how it
crosses the JSON wire.

## Requirements

### Requirement: A dictionary enumerates by slot, as .NET's does

A dictionary SHALL enumerate its keys, its values and its pairs in slot order: in insertion order
while nothing is removed, with a removed entry's slot the next one an insertion takes (the last
slot freed is the first reused), and a copy SHALL enumerate compacted, in the order of the
dictionary it copies.

#### Scenario: Integer keys keep insertion order

- **WHEN** `d[3] = 30; d[1] = 10;` runs on a new `Dictionary<int, int>`
- **THEN** `string.Join(",", d.Keys)` answers "3,1" and `string.Join(",", d.Values)` answers "30,10", as in .NET

#### Scenario: String keys that look like integers keep insertion order

- **WHEN** `d["2"] = 2; d["1"] = 1;` runs on a new `Dictionary<string, int>`
- **THEN** its keys enumerate "2,1", as in .NET

#### Scenario: An insertion reuses the slot a removal freed

- **WHEN** a dictionary holding `a`, `b` and `c` removes `a` and then adds `d`
- **THEN** its keys enumerate "d,b,c", as in .NET

#### Scenario: The last slot freed is the first reused

- **WHEN** a dictionary holding `a`, `b` and `c` removes `b`, then `a`, and then adds `x` and `y`
- **THEN** its keys enumerate "x,y,c", and when it removes `a` before `b` they enumerate "y,x,c", as in .NET

#### Scenario: A copy enumerates compacted

- **WHEN** a dictionary holding `a`, `b` and `c` removes `a`, is copied with `new Dictionary<string, int>(d)`, and the copy adds `a`
- **THEN** the copy's keys enumerate "b,c,a", as in .NET

### Requirement: A key keeps its own type

A key SHALL come back from `Keys`, from a pair and from a deconstruction in the type the dictionary
declares: a number for a numeric key, a `long` as a long, a char as a char.

#### Scenario: Numeric keys add as numbers

- **WHEN** a `Dictionary<int, int>` holding keys 3 and 1 sums `foreach (var k in d.Keys) total += k`
- **THEN** the total is 4, not a concatenation

### Requirement: A key compares as .NET's default comparer compares it

Two keys SHALL be the same key when .NET's default equality comparer for the key type says so: by
value for a record, a struct, a tuple, a decimal, a date and a class that overrides `Equals`, by
reference for a class that does not, and by value for a number (NaN equal to NaN), a string, a char,
a bool, a long, an enum and a `Guid`.

#### Scenario: A date key

- **WHEN** a `Dictionary<DateTime, int>` sets `d[new DateTime(2026, 1, 1)] = 1` and reads `d[new DateTime(2026, 1, 1)]`
- **THEN** it answers 1, as in .NET

#### Scenario: A char key

- **WHEN** a `Dictionary<char, int>` sets two keys and a `foreach` walks its pairs
- **THEN** the walk visits both in insertion order, as in .NET, where the plain path threw

### Requirement: A dictionary's members answer as .NET's

`Count`, `ContainsKey`, `TryGetValue`, `TryAdd`, `ContainsValue`, `Remove`, `Keys.Contains` and
`Keys.Count` SHALL answer as .NET's `Dictionary` answers them.

#### Scenario: TryAdd keeps a present key's value

- **WHEN** a dictionary holding `a` = 1 calls `TryAdd("a", 2)` and `TryAdd("b", 3)`
- **THEN** the calls answer false and true, `d["a"]` is still 1, and `Count` is 2

#### Scenario: A lookup through the keys

- **WHEN** a dictionary holding key 3 answers `d.Keys.Contains(3)` and `d.Keys.Count`
- **THEN** they answer true and the dictionary's count

### Requirement: ToDictionary builds the same dictionary

`Enumerable.ToDictionary` SHALL build the same dictionary as a constructed one, keyed as the key
type's default comparer keys it, in the order of its source.

#### Scenario: Integer keys from a source

- **WHEN** `new[] { 3, 1, 2 }.ToDictionary(x => x, x => x * 10)` is computed
- **THEN** its keys enumerate "3,1,2", as in .NET

#### Scenario: A key a plain object could not hold

- **WHEN** a source is keyed by a `DateTime` with `ToDictionary`
- **THEN** it builds and reads by date, where it was refused

### Requirement: A dictionary crosses the wire as its class

A dictionary held in server-rendered state or answered by a Server Action SHALL arrive as the
dictionary class, its keys in their type, in the order the parsed JSON object holds them, and a
dictionary sent to the server SHALL write itself as the JSON object System.Text.Json reads for it.

#### Scenario: A state field with integer keys

- **WHEN** a `Dictionary<int, string>` field arrives in server-rendered state
- **THEN** it hydrates into the dictionary class, `ContainsKey(3)` answers true for a key the server held, and its keys are numbers

#### Scenario: A dictionary argument

- **WHEN** a dictionary is sent as a Server Action argument
- **THEN** it serializes as a JSON object of its entries, keyed by each key's wire text
