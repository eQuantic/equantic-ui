## ADDED Requirements

### Requirement: A binding the emitted code declares starts with a dollar

Every name the emitted code declares where C# declared nothing, a lowering's accumulator, an arrow's or a comparator's parameter, a loop's variable, SHALL start with a `$`, which no C# identifier holds. The one exception SHALL be a setter's `value`, which is C#'s own name for the value the developer's setter reads.

#### Scenario: A captured local named like an accumulator

- **WHEN** `new[] { 1, 2 }.Sum(v => v + _sum)` reads a local `_sum` that holds 10
- **THEN** it answers 23, as in .NET

#### Scenario: A captured local named like a comparator's parameter

- **WHEN** `new[] { 3, 1, 2 }.OrderBy(x => x * a).First()` reads a local `a` that holds -1
- **THEN** it answers 3, as in .NET

### Requirement: A local named like a global the output reads is renamed

A C# local or parameter whose name is a global the emitted code reads (`crypto`, `undefined`, `Math`, `Number`, `console` and the rest the compiler writes) SHALL be declared and read under that name with a `$`, so the global stays visible to every lowering in its scope.

#### Scenario: A local named crypto

- **WHEN** `var crypto = "xy";` stands beside `Guid.NewGuid()`
- **THEN** the guid is made and the local reads "xy", as in .NET

#### Scenario: A local named undefined

- **WHEN** `int undefined = 5;` stands beside a call whose named argument skips an optional parameter
- **THEN** the skipped parameter takes its default, as in .NET

### Requirement: A verbatim name is written without its escape

A name written with C#'s verbatim escape SHALL be written without it: a member and a `with` key as the name itself (`class`), and a label and a type parameter, declaration and references, through the rule that renames a word JavaScript reserves (`package$`, `class$`).

#### Scenario: A member written @class

- **WHEN** `new R2(3).@class` is read, and `new R2(3) with { @class = 5 }` is printed
- **THEN** they answer 3 and `R2 { class = 5 }`, as in .NET

#### Scenario: A label named package

- **WHEN** a loop labeled `package` is left with `continue package` and `break package`
- **THEN** the module parses and the loop counts as in .NET

#### Scenario: A type parameter written @class, one level down

- **WHEN** a method declares `Count<@class>(IReadOnlyList<@class> values, @class[] more)`, in a plain
  class or in a component
- **THEN** it is declared `count<class$>`, its annotations say `class$[]`, the module parses, and it
  counts as in .NET

### Requirement: A member reached through using static translates as its qualified spelling

A .NET type's member reached bare through `using static` SHALL be converted as its qualified spelling is. One the qualified spelling does not translate either SHALL fail the build with EQ2004, reported where the bare spelling is written.

#### Scenario: A clock and a guid reached bare

- **WHEN** `Now`, `UtcNow`, `Today`, `NewGuid()` and `Empty` are read beside `using static System.DateTime` and `using static System.Guid`
- **THEN** they translate as `DateTime.Now`, `Guid.NewGuid()` and the rest do, with no diagnostic

#### Scenario: A member with no translation

- **WHEN** `GetEnvironmentVariable("HOME")` is called beside `using static System.Environment`
- **THEN** the build fails with EQ2004, naming the member and `using static`
