## MODIFIED Requirements

### Requirement: A dictionary's members answer as .NET's

`Count`, `ContainsKey`, `TryGetValue`, `TryAdd`, `ContainsValue`, `Remove`, `Keys.Contains` and
`Keys.Count` SHALL answer as .NET's `Dictionary` answers them. A sorted dictionary's and a sorted list's
`TryAdd`, which C# binds to `CollectionExtensions`' extension, SHALL be the runtime class's, through the
extension's static form too. A lookup or a `TryAdd` SHALL be the dictionary's only when the call binds to the
framework's own member, a dictionary's or `CollectionExtensions`': an extension of the app's own under one of
those names SHALL stay the app's call, whatever namespace it is declared in.

#### Scenario: TryAdd keeps a present key's value

- **WHEN** a dictionary holding `a` = 1 calls `TryAdd("a", 2)` and `TryAdd("b", 3)`
- **THEN** the calls answer false and true, `d["a"]` is still 1, and `Count` is 2

#### Scenario: A lookup through the keys

- **WHEN** a dictionary holding key 3 answers `d.Keys.Contains(3)` and `d.Keys.Count`
- **THEN** they answer true and the dictionary's count

#### Scenario: TryAdd on a sorted dictionary

- **WHEN** browser-side code calls `d.TryAdd(k, 9)` on a `SortedDictionary<int, int>` holding `k`
- **THEN** it answers false and the walk over the keys goes on, as in .NET; the browser threw a ReferenceError, `CollectionExtensions is not defined`

#### Scenario: An extension of the app's own

- **WHEN** a component calls `s.TryAdd("a", 1)` on a `SortedDictionary<string, int>`, and its own static class `Ledger` declares `TryAdd(this SortedDictionary<string, int>, string, int)`
- **THEN** the lowering is `Ledger.tryAdd(s, 'a', 1)`, the call C# binds; it was the runtime class's

#### Scenario: An extension of the app's own in the collections' namespace

- **WHEN** the app declares the same `Ledger` inside `namespace System.Collections.Generic`, and a component calls `s.TryAdd("a", 1)` and `s.GetValueOrDefault("a")` on a `SortedDictionary<string, int>`
- **THEN** the lowerings are `Ledger.tryAdd(s, 'a', 1)` and `Ledger.getValueOrDefault(s, 'a')`; the namespace alone took them for the runtime class's

### Requirement: A change while the pairs are walked answers as .NET's

A dictionary SHALL end a walk over its pairs with an InvalidOperationException when a new key is added
during it, and SHALL let an overwrite, a removal and `Clear` run on. A sorted dictionary and a sorted
list SHALL end a walk over their pairs, their keys or their values on any change, with System.Collections'
words, "Collection was modified after the enumerator was instantiated.", at the step after it, the step
after the last element included. A change SHALL be what .NET counts as one: an addition, an overwrite, a
removal and `Clear`; for a sorted dictionary that holds anything, also a `Remove` of a key it does not hold
and an `Add` of a key it holds, which its red-black tree counts before it answers; for a sorted list,
neither. A `TryAdd` of a key either holds SHALL be no change, and so SHALL a `Remove(key, out value)` of a
key a sorted dictionary does not hold, which reaches it through `CollectionExtensions`, which asks for the
key before it removes it.

#### Scenario: A key added while walking

- **WHEN** a `foreach` over a `Dictionary<int, int>` adds a key on each step
- **THEN** the walk ends with an InvalidOperationException, as in .NET

#### Scenario: A removal while walking

- **WHEN** a `foreach` over a `Dictionary<int, int>` removes the key it is on
- **THEN** the walk goes on over the rest, as in .NET

#### Scenario: A key added while a sorted dictionary's keys are walked

- **WHEN** browser-side code runs `foreach (var k in d.Keys) d[k + 10] = 0;` over a `SortedDictionary<int, int>` holding 1 and 2
- **THEN** it throws "Collection was modified after the enumerator was instantiated." after the first key, as in .NET; the browser walked a copy and saw both keys

#### Scenario: A removal that finds nothing

- **WHEN** browser-side code calls `Remove(42)` at each step of a walk over the keys of a `SortedDictionary<int, int>` and of a `SortedList<int, int>`, each holding 1 and 2
- **THEN** the sorted dictionary's walk ends after the first key and the sorted list's sees both, as in .NET

#### Scenario: A removal with an out that finds nothing

- **WHEN** browser-side code calls `d.Remove(42, out _)` at each step of a walk over the keys of a `SortedDictionary<int, int>` holding 1 and 2
- **THEN** the walk sees both keys, as in .NET; it ended after the first

### Requirement: A dictionary's keys and values are live views

`Keys` and `Values` of a dictionary, a sorted dictionary and a sorted list SHALL read the collection when
they are read, as .NET's do, hand back each element as it is, a delegate included, and answer
`IsReadOnly` true. A key added while a dictionary's are walked SHALL end the walk with .NET's
InvalidOperationException. A change asked of a view SHALL be refused with .NET's NotSupportedException:
in a dictionary's words for a dictionary's and a sorted dictionary's views ("Mutating a key collection
derived from a dictionary is not allowed.", and a value collection's likewise); for a sorted list's, a
call (`Add`, `Remove`, `Clear`) and a value written through `Values[i]` in its nested types' words ("This
operation is not supported on SortedList nested types because they require modifying the original
SortedList."), and a key written through `Keys[i]` in a dictionary's. A `Remove` SHALL be refused whether
or not the view holds the value.

#### Scenario: A view read after a change

- **WHEN** `var ks = d.Keys; d["z"] = 2;` runs on a dictionary holding `a`, and `ks` is read
- **THEN** it holds `a` and `z`, as in .NET

#### Scenario: A key added during the walk

- **WHEN** `foreach (var k in d.Keys) d["z"] = 2;` runs
- **THEN** it throws "Collection was modified; enumeration operation may not execute.", as .NET does

#### Scenario: A sorted dictionary's view read after a change

- **WHEN** `var ks = d.Keys; d[1] = 1;` runs on a `SortedDictionary<int, int>` holding 2, and `ks` is read
- **THEN** its count is 2 and it holds 1 and 2, as in .NET; the browser's copy held 2 alone

#### Scenario: A change asked of a sorted list's keys

- **WHEN** browser-side code calls `d.Keys.Add(2)` and writes `d.Keys[0] = 3` on a `SortedList<int, int>`
- **THEN** each throws a NotSupportedException, the first in the nested types' words and the second in a dictionary's, as in .NET

#### Scenario: A removal of a key a view does not hold

- **WHEN** browser-side code calls `keys.Remove("z")` through an `ICollection<string>` holding a dictionary's keys, `z` not among them
- **THEN** it throws "Mutating a key collection derived from a dictionary is not allowed.", as .NET does; it answered false
