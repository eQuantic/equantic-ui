## MODIFIED Requirements

### Requirement: A for loop's variable is one per loop

A variable a `for` statement declares SHALL be one variable for the whole loop, so a closure created
in any iteration reads its current value, as .NET's does. A variable its initializer declares (`out
var n`) SHALL be one for the whole loop too, declared before the code that assigns it.

#### Scenario: Three closures

- **WHEN** a loop over `i` from 0 to 2 adds `() => i` to a list, and the three are called after it
- **THEN** they answer `3`, `3` and `3`, as in .NET

#### Scenario: An initializer's out variable

- **WHEN** a loop is written `for (int i = Seed(out var n); i < n; i++)`, `Seed` setting `n` to `2`,
  and adds `() => i * 10 + n` to a list
- **THEN** the two closures answer `22` each, as in .NET

## ADDED Requirements

### Requirement: A local function's out parameter reaches its caller

A local function with an `out` or `ref` parameter SHALL follow the callee contract every call site
unwraps, as a method's and a lambda's do: the out leaves the signature, and the function hands its
value and each out back in one object.

#### Scenario: A measuring helper

- **WHEN** a local function `int Measure(string s, out int length)` sets `length = s.Length` and is
  called as `Measure("abc", out var n)`
- **THEN** `n` is `3`, as in .NET
