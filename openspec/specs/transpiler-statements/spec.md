# transpiler-statements Specification

## Purpose
How a statement's own semantics cross to the browser where JavaScript's statement of the same name
means something else: a lock's expression, a for loop's variable, a delegate called as a value.

## Requirements

### Requirement: A lock runs its expression

A `lock` statement SHALL evaluate its expression once, before its body, unless the expression only
reads a local, a parameter, a field or `this`.

#### Scenario: A gate made by a call

- **WHEN** browser-side code locks on `Gate()`, which counts its calls
- **THEN** `Gate` has been called once when the body runs, as in .NET

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

### Requirement: A delegate value is called as the value its expression gives

A delegate invoked through any expression but a name or a member (`fs[0]()`, `Make()()`) SHALL call
the value the expression gives.

#### Scenario: A delegate from a list

- **WHEN** browser-side code calls `ops[1](ops[0](3))` over a local array of two lambdas
- **THEN** it answers what .NET answers

### Requirement: A new object is a value of its own

`new object()` SHALL be a fresh value equal only to itself.

#### Scenario: Two objects

- **WHEN** browser-side code compares two `new object()` with `==`
- **THEN** the answer is `false`, as in .NET

### Requirement: A local function's out parameter reaches its caller

A local function with an `out` or `ref` parameter SHALL follow the callee contract every call site
unwraps, as a method's and a lambda's do: the out leaves the signature, and the function hands its
value and each out back in one object.

#### Scenario: A measuring helper

- **WHEN** a local function `int Measure(string s, out int length)` sets `length = s.Length` and is
  called as `Measure("abc", out var n)`
- **THEN** `n` is `3`, as in .NET
