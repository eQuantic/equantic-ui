## ADDED Requirements

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
