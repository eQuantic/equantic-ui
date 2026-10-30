## ADDED Requirements

### Requirement: An element counted from the end reads its receiver once

An element of an array, a list or a string counted from the end (`a[^n]`) SHALL be read and written with
its receiver evaluated once, then its offset, then the length, as C# evaluates them, whatever writes it
(an assignment, a null-conditional assignment, a compound assignment, an increment, `??=`, a deconstruction), and its offset SHALL be
one value whatever expression it is written as.

#### Scenario: A receiver made by a call

- **WHEN** `int[] Get() { calls++; return arr; }` and `Get()[^1] += 5` runs over `{ 1, 2, 3 }`
- **THEN** the array holds `1,2,8` and `Get` ran once, as in .NET; it ran twice

#### Scenario: An offset written as a sum

- **WHEN** `var a = new[] { 1, 2, 3, 4 }; int i = 1; return a[^(i + 1)];` runs
- **THEN** it answers 3, as .NET does

#### Scenario: An offset that changes the receiver's variable

- **WHEN** `a[^Swap()]` is read, where `Swap` assigns another array to `a` and answers 1
- **THEN** it reads the last element of the array `a` held before `Swap` ran, as in .NET

#### Scenario: An out argument

- **WHEN** `Set(out Get()[^1])` runs, `Set` assigning 9 to its parameter
- **THEN** the last element is 9 and `Get` ran once, before the call, as in .NET

#### Scenario: An await beside a receiver made by a call

- **WHEN** `Get()[^1] = await F()` and `Get()[^(await Offset())]` run in an async method
- **THEN** they answer as .NET does, the await suspending that method; the JavaScript did not parse

#### Scenario: A value that awaits and may not run

- **WHEN** `Get()[^1] ??= await F()` runs over an element that is not null
- **THEN** `F` does not run, as in .NET, and `Get()[^1] += await F()` reads the element before the await

#### Scenario: A Task read beside an await

- **WHEN** `var t = Get()[^(await Offset())]` reads a `Task` from an array
- **THEN** `t` is that Task, as in .NET, unawaited

#### Scenario: An out target after code

- **WHEN** `Put(Swap(), out a[^1])` is built, `Swap` assigning another array to `a`
- **THEN** the build fails with EQ1004, since the element is taken before the call and C# takes it after
  `Swap` ran
