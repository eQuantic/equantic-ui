## MODIFIED Requirements

### Requirement: A dictionary's members answer as .NET's

`Count`, `ContainsKey`, `TryGetValue`, `TryAdd`, `ContainsValue`, `Remove`, `Keys.Contains` and
`Keys.Count` SHALL answer as .NET's `Dictionary` answers them, and a sorted dictionary's `Keys.Contains`
as .NET's `SortedDictionary` answers it. A sorted dictionary's and a sorted list's
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

#### Scenario: A lookup through a sorted dictionary's keys

- **WHEN** a `SortedDictionary<string, int>` holding `b` hands out `var ks = d.Keys`, gains `a`, and asks `ks.Contains("a")` and `ks.Contains("z")`
- **THEN** they answer true and false, as in .NET; the build refused the call with EQ2004

#### Scenario: TryAdd on a sorted dictionary

- **WHEN** browser-side code calls `d.TryAdd(k, 9)` on a `SortedDictionary<int, int>` holding `k`
- **THEN** it answers false and the walk over the keys goes on, as in .NET; the browser threw a ReferenceError, `CollectionExtensions is not defined`

#### Scenario: An extension of the app's own

- **WHEN** a component calls `s.TryAdd("a", 1)` on a `SortedDictionary<string, int>`, and its own static class `Ledger` declares `TryAdd(this SortedDictionary<string, int>, string, int)`
- **THEN** the lowering is `Ledger.tryAdd(s, 'a', 1)`, the call C# binds; it was the runtime class's

#### Scenario: An extension of the app's own in the collections' namespace

- **WHEN** the app declares the same `Ledger` inside `namespace System.Collections.Generic`, and a component calls `s.TryAdd("a", 1)` and `s.GetValueOrDefault("a")` on a `SortedDictionary<string, int>`
- **THEN** the lowerings are `Ledger.tryAdd(s, 'a', 1)` and `Ledger.getValueOrDefault(s, 'a')`; the namespace alone took them for the runtime class's
