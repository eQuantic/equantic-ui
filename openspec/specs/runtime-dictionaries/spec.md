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
a bool, a long, an enum and a `Guid`. A key whose type does not decide (`object`, an interface, a
type parameter) SHALL compare as the value it holds compares. A tuple key SHALL compare each element
by its own type's rule, an array element by reference. The rule SHALL be the one a set's elements and
a list's searches take for the same type.

#### Scenario: A date key

- **WHEN** a `Dictionary<DateTime, int>` sets `d[new DateTime(2026, 1, 1)] = 1` and reads `d[new DateTime(2026, 1, 1)]`
- **THEN** it answers 1, as in .NET

#### Scenario: A record under object

- **WHEN** a `Dictionary<object, int>` sets `d[new Point(1, 2)] = 1` and then `d[new Point(1, 2)] = 2`
- **THEN** it holds one key, whose value is 2, as in .NET

#### Scenario: A char key

- **WHEN** a `Dictionary<char, int>` sets two keys and a `foreach` walks its pairs
- **THEN** the walk visits both in insertion order, as in .NET, where the plain path threw

#### Scenario: A tuple key holding an array

- **WHEN** a `Dictionary<(int[], int), int>` keyed by `(a, 1)` is asked whether it contains `(new[] { 1 }, 1)`
- **THEN** it answers false, as in .NET, where comparing the array element by element found it

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

A dictionary held in server-rendered state, answered by a Server Action or published on a server
topic SHALL arrive as the dictionary class, its keys in their type, enumerating in the order the
server enumerated it, and a dictionary sent to the server as a Server Action argument SHALL arrive
enumerating in the order the browser enumerated it. Both directions SHALL write it as a JSON array
of `[key, value]` pairs, each key and each value written as a value of its type is written.

#### Scenario: A state field with integer keys

- **WHEN** a `Dictionary<int, string>` field holding the keys 3, then 1 arrives in server-rendered state
- **THEN** it hydrates into the dictionary class, `ContainsKey(3)` answers true, its keys are numbers, and they enumerate "3,1", as in .NET

#### Scenario: Keys that look like integers

- **WHEN** a Server Action answers a `Dictionary<string, int>` holding the keys "b", then "3"
- **THEN** the browser's dictionary enumerates "b,3", as in .NET

#### Scenario: A dictionary argument

- **WHEN** a dictionary holding the keys 3, then 1 is sent as a Server Action argument
- **THEN** it serializes as `[[3,…],[1,…]]`, and the server's dictionary enumerates "3,1"

#### Scenario: A key of a type JSON has no number for

- **WHEN** a `Dictionary<long, string>` keyed by 9007199254740993 crosses in either direction
- **THEN** the key is written as its text, "9007199254740993", and arrives as that long

#### Scenario: A dictionary-like value of another type

- **WHEN** a member declared `IReadOnlyDictionary<int, string>` holds an `ImmutableDictionary`, which System.Text.Json writes as a JSON object
- **THEN** it still hydrates into the dictionary class, in the order the parsed object holds

### Requirement: A change while the pairs are walked answers as .NET's

A dictionary SHALL end a walk over its pairs with an InvalidOperationException when a new key is added
during it, and SHALL let an overwrite, a removal and `Clear` run on. A sorted dictionary and a sorted
list SHALL end the walk on any change.

#### Scenario: A key added while walking

- **WHEN** a `foreach` over a `Dictionary<int, int>` adds a key on each step
- **THEN** the walk ends with an InvalidOperationException, as in .NET

#### Scenario: A removal while walking

- **WHEN** a `foreach` over a `Dictionary<int, int>` removes the key it is on
- **THEN** the walk goes on over the rest, as in .NET

### Requirement: Add refuses a key already there

A dictionary's `Add`, a collection initializer and the constructor that copies pairs SHALL refuse a key
the dictionary already holds with .NET's `ArgumentException` message for the collection: a `Dictionary`
names the key, a `SortedDictionary` the pair it was handed, and a `SortedList` the key and its
parameter. The indexer's write, an object initializer's `[key] = value` and `TryAdd` SHALL replace or
decline as they do in .NET.

#### Scenario: Add twice

- **WHEN** browser-side code adds `"a"` to a `Dictionary<string, int>` twice, and builds
  `new Dictionary<string, int> { ["a"] = 1, ["a"] = 2 }`
- **THEN** the second `Add` throws "An item with the same key has already been added. Key: a", and the
  object initializer holds `2`, as in .NET

### Requirement: A pair built by the app is the pair a dictionary yields

`new KeyValuePair<K, V>(key, value)`, `KeyValuePair.Create(key, value)` and the parameterless
constructor SHALL build the pair a dictionary yields, which reads `Key` and `Value` and deconstructs
into both, and `default(KeyValuePair<K, V>)` SHALL be the pair of the two defaults.

#### Scenario: A pair by hand

- **WHEN** browser-side code builds `new KeyValuePair<int, string>(value: "c", key: 3)` and asks a
  dictionary holding `[1] = "a"` whether it `Contains(new KeyValuePair<int, string>(1, "a"))`
- **THEN** the pair reads `3` and `"c"`, and the dictionary answers true, as in .NET

### Requirement: A comparer that asks for the default builds the dictionary

A `Dictionary`, a `SortedDictionary` or a `SortedList` constructed with a comparer that asks for
nothing the browser does not already do (`null`, `EqualityComparer<T>.Default`,
`Comparer<T>.Default`, or `StringComparer.Ordinal`) SHALL build, and SHALL answer as .NET's does:
its entries, its lookups, a copy of the dictionary or pairs it was handed, and, for a sorted one, the
order its comparer asks for, which is the code-unit order for `StringComparer.Ordinal` and the key
type's own for the default. A comparer that changes equality or order beyond that SHALL fail the
build once, with EQ2007, and never become the dictionary's seed.

#### Scenario: An ordinal dictionary with an indexer initializer

- **WHEN** a component builds `Dictionary<string, int> d = new(StringComparer.Ordinal) { ["b"] = 1, ["a"] = 2 }`
- **THEN** the build succeeds, the keys enumerate `b, a`, `d["a"]` is 2 and `d.ContainsKey("A")` is false

#### Scenario: A sorted dictionary with the ordinal comparer

- **WHEN** a component builds a `SortedDictionary<string, int>` with `StringComparer.Ordinal` and the
  keys `b`, `B` and `a`
- **THEN** the keys enumerate `B, a, b`

#### Scenario: A comparer that changes equality

- **WHEN** a component builds `new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)`
- **THEN** the build fails with EQ2007, and with no other error for the same construction
