## MODIFIED Requirements

### Requirement: A null-conditional call evaluates as C# does, an awaited argument included

A null-conditional access (`a?.M(x)`, `a?[i]`, and a chain behind one) SHALL evaluate its receiver
once and SHALL answer null when the receiver is null, without evaluating the rest of the chain or
its arguments. An argument that awaits SHALL be awaited in the method it is written in, only when
the receiver is not null, so a method that meets a null receiver goes on without suspending, and
the call's own answer SHALL NOT be awaited: a task it returns stays a task. This SHALL hold whatever
the receiver is (a local, a parameter, `this`, a call, an element, a property, or a guard in the
tail of another), in a statement, in a loop a label names and in a lambda, every call of which
SHALL keep its own receiver.

#### Scenario: An awaited argument behind a string

- **WHEN** `string s = "abc";` runs `var r = s?.StartsWith(await Needle(), StringComparison.Ordinal);`,
  where `Needle` counts its calls and answers `"a"`
- **THEN** `r` is true and `Needle` was called once

#### Scenario: An awaited argument behind a null receiver

- **WHEN** `string s = null;` runs the same line
- **THEN** `r` is null and `Needle` was never called

#### Scenario: A null receiver does not suspend the method

- **WHEN** an async method runs `var r = s?.StartsWith(await Needle(), StringComparison.Ordinal);`
  and then sets `finished = true`, with `s` null, and its caller reads `finished` before awaiting it
- **THEN** the caller reads true

#### Scenario: A receiver that is not a local

- **WHEN** a method runs `var r = Get()?.StartsWith(await Needle(), StringComparison.Ordinal);`,
  where `Get` counts its calls and answers `"abc"` and `Needle` counts its calls and answers `"a"`
- **THEN** it builds, `r` is true, and `Get` and `Needle` were each called once

#### Scenario: An awaited argument behind a call that answers null

- **WHEN** `Get` answers null in the same line
- **THEN** `r` is null, `Get` was called once and `Needle` never

#### Scenario: A guard in the tail of another

- **WHEN** a method returns `Get()?.Replace((await Inner())?.Trim() ?? "q", "x")`, where `Get`
  answers `"abc"` and `Inner` answers `" b "`
- **THEN** it returns `"axc"`

#### Scenario: A lambda run three times at once

- **WHEN** `Func<int, Task<string>> f = async i => arr[i]?.Replace(await Echo("b"), "x");` runs
  `f(0)`, `f(1)` and `f(2)` under `Task.WhenAll`, with `arr` holding `"abc"`, `"bcd"` and null
- **THEN** the answers are `"axc"`, `"xcd"` and null

#### Scenario: A loop a label names

- **WHEN** `outer: while (Next()?.Trim() is { } v) { if (v == "b") continue outer; seen += v; }`
  runs while `Next` hands out `" a "`, `" b "` and null, counting the calls in `at` from -1
- **THEN** `seen + at` is `"a2"`, as in .NET
