## ADDED Requirements

### Requirement: A member reached through using static answers as its qualified spelling

A .NET member reached by its simple name through `using static` SHALL answer in the browser what
its qualified spelling answers where the bare spelling reaches that translation: a `Math` and `MathF`
member, a `string` static, an inlined constant and an enum's member. Any other platform member reached
bare SHALL fail the build with EQ2004 rather than be emitted as a member of its class, even where its
qualified spelling translates (`DateTime.Now`), until the bare spelling reaches that translation too.

#### Scenario: Constants and methods

- **WHEN** browser-side code writes, under `using static System.Double;`, `using static System.Math;`
  and `using static System.String;`, `IsNaN(NaN)`, `Round(PI * 100) / 100` and
  `Join(",", parts) + Empty`
- **THEN** each answers what .NET answers: `true`, `3.14` and `a,b` for `parts` of `a` and `b`

#### Scenario: An enum's member

- **WHEN** browser-side code writes, under `using static System.DayOfWeek;`, `Monday == DayOfWeek.Monday`
- **THEN** the answer is `true`, as in .NET

#### Scenario: A member nothing translates

- **WHEN** a component calls `WriteLine("built")` under `using static System.Console;`
- **THEN** the build fails with EQ2004 naming `System.Console.WriteLine`
