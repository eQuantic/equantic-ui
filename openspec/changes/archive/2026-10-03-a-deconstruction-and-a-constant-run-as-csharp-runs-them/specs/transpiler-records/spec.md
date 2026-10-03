## RENAMED Requirements

- FROM: `### Requirement: A record deconstructed by assignment fills its targets`
- TO: `### Requirement: Every deconstruction reads a record or a struct through its Deconstruct`

## MODIFIED Requirements

### Requirement: Every deconstruction reads a record or a struct through its Deconstruct

A deconstruction SHALL bind each part the way the bound tree says .NET does, in every shape it takes:
a declaration (`var (a, b) = p`, `(var a, var b) = p`), an assignment (`(a, b) = p`), a mixed one
(`(var a, b) = p`) and a `foreach (var (a, b) in ps)`. A tuple and a dictionary's pair SHALL bind by
position. Any other value SHALL bind through the `Deconstruct` each level calls: a record's own and a
BCL type's by the members its out parameters name, and one the app wrote by calling it. A nested
deconstruction SHALL bind each level by its own type, and a discard SHALL bind nothing.

#### Scenario: Existing locals

- **WHEN** browser-side code declares `int a, b;` and writes `(a, b) = new Point(1, 2)`
- **THEN** `a` is `1` and `b` is `2`, as in .NET

#### Scenario: A declaration written as a tuple, and a loop

- **WHEN** browser-side code writes `(var a, var b) = new Point(1, 2)`, or sums `a * 10 + b` over
  `foreach (var (a, b) in new[] { new Point(1, 2), new Point(3, 4) })`
- **THEN** it reads `12`, and the loop's sum is `46`, as in .NET

#### Scenario: A struct's own Deconstruct

- **WHEN** a struct's `Deconstruct(out double celsius, out double fahrenheit)` computes the second
  part, and browser-side code writes `var (c, f) = new Temperature(100)`
- **THEN** `c + f` is `312`, as in .NET

#### Scenario: A nested deconstruction

- **WHEN** browser-side code writes `var ((x1, y1), (x2, y2)) = new Line(new Point(1, 2), new Point(3, 4))`
- **THEN** the four names hold `1`, `2`, `3` and `4`, as in .NET

## ADDED Requirements

### Requirement: A static store starts as its declaration says

A static property's store SHALL start as its initializer, written into the store directly, or as its
type's default when it declares none, in a component, a plain class and a record alike, before any
code writes it.

#### Scenario: An initializer behind a doubling setter

- **WHEN** a class declares `public static int Total { get; set => field = value * 2; } = 5;`
- **THEN** reading `Total` before any write answers `5`, as in .NET

#### Scenario: No initializer

- **WHEN** a component declares `public static int Hits { get; set; }`
- **THEN** reading `Hits` before any write answers `0`, as in .NET
