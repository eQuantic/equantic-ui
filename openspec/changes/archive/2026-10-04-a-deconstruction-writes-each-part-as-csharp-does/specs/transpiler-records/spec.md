## ADDED Requirements

### Requirement: A deconstruction's part is converted to its target's type and written by what its target is

A deconstruction SHALL write each part as C# writes it: converted to its target's type through the
conversion the bound tree names for that part, and written by what the target is. A dictionary's
entry SHALL be written by its class, its receiver and key evaluated before the value, and a part a
declaration or a loop converts SHALL be declared in its own type. A deconstruction whose parts need
neither SHALL keep its destructuring.

#### Scenario: A dictionary's entries as targets

- **WHEN** browser-side code writes `(map["a"], map["b"]) = new Point(1, 2)` over a
  `Dictionary<string, int>`
- **THEN** `map["a"]` is `1` and `map["b"]` is `2`, as in .NET, where the module did not parse

#### Scenario: An int part into a long

- **WHEN** browser-side code writes `long total; int n; (total, n) = new Point(1, 2);` and then
  `(total + 1L).ToString()`
- **THEN** it answers `2`, as in .NET, where the long arithmetic threw

#### Scenario: A converted declaration and a converted loop

- **WHEN** browser-side code writes `(long t, int m) = pair` for an `(int, int)` pair, and sums
  `a * 10 + b` over `foreach ((long a, int b) in pairs)`
- **THEN** both compute in long, as in .NET
