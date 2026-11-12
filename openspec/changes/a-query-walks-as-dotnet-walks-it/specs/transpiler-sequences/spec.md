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
