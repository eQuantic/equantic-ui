## ADDED Requirements

### Requirement: A LINQ query runs when it is walked

A deferred LINQ operator SHALL check its arguments when it is called and SHALL read its source only when
its query is walked, each time it is walked, as .NET's iterators do. An operator the runtime walks itself
(`Where`, `Select`, `SelectMany`, `Concat`, `Append`, `Prepend`, `TakeWhile`, `SkipWhile`, `Skip`, `Take`)
SHALL read one element at a time, each only after the callback before it returned, so a chain interleaves
its callbacks; every other deferred operator SHALL read its source as it is when its query is walked. An
operator that answers from a query (`Any`, `All`, `First`, `FirstOrDefault`, `ElementAt`, `Last`) SHALL
stop at the element that answers, and a list changed under the walk SHALL end it with .NET's
InvalidOperationException.

#### Scenario: A callback that changes its source

- **WHEN** browser-side code joins `d.Select(kv => { d["b"] = 5; return kv.Value; })` over a dictionary of
  `a = 1` and `b = 2`
- **THEN** it answers `1,5`, as .NET does; it answered `1,2`

#### Scenario: A list changed under the walk

- **WHEN** browser-side code joins `l.Select(v => { l[1] = 5; return v; })` over `new List<int> { 1, 2 }`
- **THEN** it throws an InvalidOperationException, "Collection was modified; enumeration operation may not
  execute.", as .NET does; it answered

#### Scenario: A query read after its source changed

- **WHEN** browser-side code takes `q = l.OrderBy(x => x)` over `new List<int> { 3, 1 }`, adds 2 to the
  list, and joins `q`
- **THEN** it answers `1,2,3`, as .NET does; it answered `1,3`

#### Scenario: An answer stops the walk

- **WHEN** browser-side code computes `l.Select(x => { log += x; return x; }).Any(x => x == 2)` over
  `new List<int> { 1, 2, 3 }`
- **THEN** the log reads `12`, as in .NET; it read `123`
