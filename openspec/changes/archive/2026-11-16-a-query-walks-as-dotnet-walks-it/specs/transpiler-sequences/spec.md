## ADDED Requirements

### Requirement: A LINQ query reads its source when it is walked, one element at a time

A deferred LINQ operator SHALL check its arguments when it is called, refusing a null by .NET's
parameter name, and SHALL read nothing else then. Walking its query SHALL read the source one element
at a time, each only after the callback before it returned, so a chain of operators interleaves its
callbacks, a change a callback makes to its source changes what is read next, and a query made before
a change to its source reads the change. An operator that answers SHALL stop at the element that
answers.

#### Scenario: A chain interleaves its callbacks

- **WHEN** `l.Where(x => { log += "w" + x; return x != 2; }).Select(x => { log += "s" + x; return x * 10; }).ToList()` runs over `{ 1, 2, 3 }`
- **THEN** the log is `w1s1w2w3s3`, as .NET's, where every `Where` callback ran before the first `Select`

#### Scenario: An answer stops where it is found

- **WHEN** `l.Select(x => { log += x; return x; }).Any(x => x == 2)` runs over `{ 1, 2, 3 }`
- **THEN** the log is `12`, and `Take(2)` over a projection of `{ 1, 2, 3, 4 }` logs `12`

#### Scenario: A query sees a change made before its walk

- **WHEN** `var q = l.Where(x => x > 0); l.Add(3);` and `q` is walked
- **THEN** it reads `1,2,3`

#### Scenario: A null source is refused when the operator is called

- **WHEN** `none.Where(x => true)` is called on a null list, without walking it
- **THEN** it throws `ArgumentNullException` naming the parameter `source`

#### Scenario: A callback that changes its source

- **WHEN** `string.Join(",", d.Select(kv => { d["b"] = 5; return kv.Value; }))` runs over a dictionary of `a = 1` and `b = 2`
- **THEN** it answers `1,5`, as .NET's, where it answered `1,2`

#### Scenario: A sorted query read after its source changed

- **WHEN** `var q = l.OrderBy(x => x);` over `{ 3, 1 }`, then `l.Add(2)`, and `q` is joined
- **THEN** it answers `1,2,3`, as .NET's, where it answered `1,3`

#### Scenario: A query held in a local

- **WHEN** `var q = l.Select(x => { log += x; return x; });` over `{ 1, 2, 3 }`, then `q.First()`
- **THEN** the log is `1`, as when the query is written in place

#### Scenario: Select's index overload

- **WHEN** `l.Select((x, i) => { log += x; return x; }).Last()` runs over `{ 1, 2, 3 }`
- **THEN** the log is `123`, as .NET's plain iterator runs it, where a projection without the index reads the last element alone

#### Scenario: A callback that changes the list under a terminal operator

- **WHEN** `l.Where(x => { if (x == 1) l.Add(9); return true; }).ToList()` runs over `{ 1, 2, 3 }`
- **THEN** it throws `InvalidOperationException`, .NET's documented contract and what .NET in WebAssembly answers; .NET on a server, whose LINQ is optimized for speed, reads the list by a span and answers 3, and the case is listed with that reason
