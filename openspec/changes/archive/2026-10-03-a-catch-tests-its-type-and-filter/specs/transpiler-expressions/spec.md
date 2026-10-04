## ADDED Requirements

### Requirement: An expression the transpiler lowers runs where C# runs it

A C# expression the transpiler lowers through a construct of its own SHALL run where C# runs it: once,
in C#'s order, and in the function C# runs it in, so that an `await` in it is the enclosing method's.
`checked(…)` and `unchecked(…)` SHALL be their operand, each operation in it settled under the bound
tree's context; a `throw` expression's exception, `Trim`'s characters and receiver, and
`Enumerable.Range`'s and `Repeat`'s arguments SHALL be passed as arguments, never held in a function
the transpiler writes around them. `Trim` of no characters SHALL trim white space, and `Range` and
`Repeat` SHALL refuse a negative count with an `ArgumentOutOfRangeException`, as .NET does.

#### Scenario: An await in checked

- **WHEN** `async Task<int> F() { await Task.Yield(); return 1; } return checked(await F() + 1);` runs
- **THEN** it answers 2, as .NET does, where the module did not parse

#### Scenario: An await in a throw expression

- **WHEN** `string s = null; try { return s ?? throw new InvalidOperationException(await M()); } catch (InvalidOperationException e) { return e.Message; }` runs, `M` answering `m` after a yield
- **THEN** it answers `m`, as .NET does

#### Scenario: An await among Trim's characters

- **WHEN** `return "xxaxx".Trim(await C());` runs, `C` answering `'x'` after a yield
- **THEN** it answers `a`, as .NET does

#### Scenario: Range's start evaluated once

- **WHEN** `var calls = 0; int Start() { calls++; return 1; } var r = string.Join(",", Enumerable.Range(Start(), 3)); return r + ":" + calls;` runs
- **THEN** it answers `1,2,3:1`, as .NET does, where `Start` ran three times and the answer was `1,3,5:3`

#### Scenario: Repeat's element evaluated once

- **WHEN** `var xs = Enumerable.Repeat(new List<int>(), 3).ToList(); return object.ReferenceEquals(xs[0], xs[1]);` runs
- **THEN** it answers true, as .NET does, where the three lists were three
