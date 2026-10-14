## ADDED Requirements

### Requirement: LINQ finds a key and an element as the type's default comparer does

Every LINQ operator that holds a key or an element SHALL find it as `EqualityComparer<T>.Default` finds
it, by the decision `ElementEquality` makes for a dictionary, a set, `Contains` and `IndexOf`: a record, a
struct, a decimal and a date by value, an anonymous object and a tuple by their members, an array they
hold by reference, a class by its own `Equals` where it or a subclass overrides one and by reference
otherwise, and a value behind an interface or `object` by what it turns out to be. That SHALL hold for
`Join` and `GroupJoin` by their key, `Union`, `Intersect`, `Except` and `Distinct` by their element,
`DistinctBy`, `UnionBy`, `IntersectBy` and `ExceptBy` by their key, and `GroupBy`, `ToLookup` and a
lookup's indexer by their key. A float or a double key SHALL find a NaN equal to itself, and 0 equal to
-0, as .NET does.

#### Scenario: A join by a composite key

- **WHEN** `a.Join(b, x => new { x.K }, y => new { y.K }, (x, y) => x.V + y.W)` runs over `a` holding
  `(K: 1, V: "a")` and `(K: 2, V: "b")` and `b` holding `(K: 1, W: "x")`, and the same join by `(x.K, 0)`
- **THEN** both match one pair, `ax`, as in .NET, where the browser matched nothing

#### Scenario: Set operators over anonymous objects and tuples

- **WHEN** `p.Union(q)`, `p.Intersect(q)` and `p.Except(q)` count `{ A = 1 }, { A = 2 }` against
  `{ A = 1 }, { A = 3 }`
- **THEN** they answer 3, 1 and 1, as in .NET, where the browser answered 4, 0 and 2

#### Scenario: A record, a decimal and a date

- **WHEN** `Distinct`, `Union`, `Intersect`, `Except`, `GroupBy` and a join count records, decimals of
  another scale (`1.0m` and `1.00m`) and equal dates
- **THEN** they find the equal ones equal, as in .NET

#### Scenario: An array a key holds

- **WHEN** records, tuples and anonymous objects hold two different arrays of the same items, and the
  same array, as keys and elements
- **THEN** the different arrays are different keys and the same array one key, as in .NET, where
  `Distinct` by `JSON.stringify` and `GroupBy` by `$eq.equals` walked the arrays

#### Scenario: A class that overrides Equals, and its base

- **WHEN** `Distinct`, `Union`, `Intersect`, `Except`, `GroupBy`, a join, `DistinctBy` and a lookup run
  over a class that overrides `Equals` and `GetHashCode`, and over an `Animal[]` of a `Dog` that does
- **THEN** each asks that `Equals`, as in .NET, while a class that overrides nothing is found by
  reference

#### Scenario: A NaN key

- **WHEN** `new[] { double.NaN, double.NaN, 0.0, -0.0 }.GroupBy(x => x).Count()` runs
- **THEN** it answers 2, as in .NET, where each NaN was a group of its own
