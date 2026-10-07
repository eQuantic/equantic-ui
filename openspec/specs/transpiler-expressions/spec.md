# transpiler-expressions Specification

## Purpose
How eqc translates a C# expression, pattern or local into the JavaScript twin: the answer the twin
gives is the one .NET gives, and plain JavaScript holds nothing of TypeScript.

## Requirements

### Requirement: An extended property pattern reads each member of its path

An extended property pattern (`{ A.B: pattern }`) SHALL be translated as the nested pattern
`{ A: { B: pattern } }`: each member of the path named as a member access names it, and a member
that is null before the last SHALL make the pattern answer false.

#### Scenario: A count through a member

- **WHEN** `new Bag(new List<int> { 1, 2 }, null) switch { { Items.Count: > 1 } => 1, _ => 0 }` runs
- **THEN** it answers 1, as .NET does

#### Scenario: A null on the path

- **WHEN** `new Bag(new List<int> { 1 }, null) switch { { Corner.X: 0 } => 1, _ => 0 }` runs
- **THEN** it answers 0, as .NET does, and nothing throws

### Requirement: A string crosses whole

Every string the transpiler writes from a C# value SHALL be the value .NET holds, code unit for code
unit, in a module that parses and can be written as UTF-8: a string literal, a char literal, an
interpolated string's text, a `const` inlined where it is read, an interpolation's format, the
default a named argument skips, a resource lookup's id and key, and `nameof`. A line break, a
surrogate that is not half of a pair, a control, a format character, U+2028 and U+2029 SHALL be
written as escapes, and a well-formed surrogate pair SHALL stay one code point.

#### Scenario: A default holding a line break

- **WHEN** `record Sep(string Joined = "a" + "\n", char Line = '\n', string Plain = "\r\n")` is
  constructed without arguments and its three members are concatenated
- **THEN** the answer is `a`, a line feed, a line feed, a carriage return and a line feed, as .NET
  gives it

#### Scenario: A lone surrogate in a string literal

- **WHEN** `return (int)char.GetUnicodeCategory("x\uD83D", 1);` runs
- **THEN** it answers 16 (Surrogate), as .NET does, where the module holding the literal could not
  be written and the build stopped with EQ0001

#### Scenario: A pair between two lone halves

- **WHEN** `var s = "\uD83D\uD83D\uDE00\uDE00";` is read with `char.IsSurrogatePair(s, 1)`,
  `char.IsSurrogatePair(s, 0)` and `char.ConvertToUtf32(s[1], s[2])`
- **THEN** the answers are true, false and 128512, as .NET gives them

#### Scenario: A format with quoted text

- **WHEN** `$"{new DateTime(2026, 10, 3):dd 'de' MMMM}"` runs under the invariant culture
- **THEN** it answers `03 de October`, as .NET does, where Bun refused the module

#### Scenario: A skipped default holding a quote

- **WHEN** `string Join(string a = "it's", char c = ',', int b = 0) => a + c + b;` is called as
  `Join(b: 1)`
- **THEN** it answers `it's,1`, as .NET does, where the call closed the default's quotes

### Requirement: Plain JavaScript carries no annotation

A module eqc writes without type annotations, as the design host asks, SHALL hold no TypeScript
syntax, a local's declared type included.

#### Scenario: A local that starts null

- **WHEN** `string? label = null;` is compiled with type annotations off
- **THEN** it is written `let label = null;`, and with them on `let label: string | null = null;`

### Requirement: A char literal is its value

A char literal SHALL cross as the character C# reads, whichever escape spells it, and `nameof` SHALL
answer the name C# answers, not the identifier's spelling.

#### Scenario: The escapes only C# has

- **WHEN** `var s = "\a\e"; return s[0] == '\a' && s[1] == '\e';` runs
- **THEN** it answers true, as .NET does, where `'\a'` and `'\e'` were `'a'` and `'e'` to JavaScript

#### Scenario: A hexadecimal escape of any length

- **WHEN** `return "A" + '\x041' + '\U00000041';` runs
- **THEN** it answers `AAA`, as .NET does, where `'\x041'` was two characters

#### Scenario: nameof of a verbatim identifier

- **WHEN** `var @class = 1; return nameof(@class);` runs
- **THEN** it answers `class`, as .NET does, where the twin answered `@class`

### Requirement: A raw interpolated string keeps its braces

A raw interpolated string's braces SHALL be its text unless as many of them as the string has
dollars open a hole; only a regular or a verbatim interpolated string SHALL read a doubled brace as
one brace.

#### Scenario: Three dollars and two braces

- **WHEN** `var n = 1; return $$$"""a{{b}}c{{{n}}}""";` runs
- **THEN** it answers `a{{b}}c1`, as .NET does, where the twin answered `a{b}c1`

#### Scenario: A regular interpolated string's doubled brace

- **WHEN** `var n = 1; return $"{{a}}{n}";` runs
- **THEN** it answers `{a}1`, as .NET does

### Requirement: A literal with no JavaScript spelling is refused

A literal that JavaScript has no spelling for SHALL fail the build with EQ1004 at the literal,
and SHALL NOT be written into the module as C#.

#### Scenario: A UTF-8 literal

- **WHEN** a component reads `"ab"u8.Length`
- **THEN** the build fails with EQ1004 naming the literal, where Bun met `"ab"u8.length` in the
  module and reported a syntax error against no C# line

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

### Requirement: A type pattern tests the type

A type pattern with nothing bound (`o is int`, `int => …`, `case int:`, either side of `or` and
`and`) SHALL test the value's type as a declaration pattern does, read off the type's symbol: a
string, a bool, a long as a BigInt, an integral type as a whole number, a real as a number, and a
decimal and the dates as the runtime's classes.

#### Scenario: A long among ints

- **WHEN** browser-side code asks `o is int or long` of an `object` holding `5L`
- **THEN** the answer is `true`, as in .NET

#### Scenario: A switch over types

- **WHEN** a switch statement over an `object` holding `5` has `case long:` and then `case int:`
- **THEN** the `int` section runs, as in .NET

### Requirement: A named constant in an is test compares with its value

`x is C`, where `C` binds as a constant (a `const` field or an enum member), and a constant pattern
in any other spelling (`x is 1.5m`, `case Limits.Max:`) SHALL test `x` against the constant's value
through one rule: a null constant SHALL match any absence, a decimal and a NaN SHALL compare by value,
and any other constant SHALL compare as its literal, an enum member as the value the browser holds for
it and a long as a BigInt.

#### Scenario: A const

- **WHEN** browser-side code asks `x is Limits.Max` of `x = 3`, where `Limits.Max` is the const `10`
- **THEN** the answer is `false`, as in .NET

#### Scenario: A long, a decimal and a NaN

- **WHEN** browser-side code asks `x is Limits.Five` of `long x = 5`, `o is Limits.Rate` of an
  `object` holding `1.5m`, and `d is double.NaN` of `double d = double.NaN`
- **THEN** each answer is `true`, as in .NET

### Requirement: A long constant is a long

A `long` or `ulong` constant SHALL cross as the BigInt every long is in the browser, whatever its size.

#### Scenario: A constant in arithmetic

- **WHEN** browser-side code computes `t.Ticks / TimeSpan.TicksPerDay` for a two-day `TimeSpan`
- **THEN** it answers `2`, as in .NET

### Requirement: A char type test asks for one code unit

`o is char` SHALL be true only for a string of one UTF-16 code unit, the browser's value of a char.

#### Scenario: A string is no char

- **WHEN** a switch over an `object` holding `"hello"` has a `char` arm before a `string` arm
- **THEN** the `string` arm is taken, as in .NET

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

### Requirement: A method named Invoke is a method

`x.Invoke(…)` SHALL be a call of `x` only when `x` is a delegate, decided by the bound symbol; a method
of that name on any other type SHALL be called as the method it is.

#### Scenario: A class with an Invoke method

- **WHEN** a class declares `int Invoke()` and another calls `other.Invoke()`
- **THEN** the twin calls `other.invoke()`, and a `Func<int>`'s `make.Invoke()` is `make()`

### Requirement: An annotation names what a module has

A twin's annotation SHALL name a type whose C# name names nothing in TypeScript by what it crosses as:
an exception as `Error`, the JavaScript error that carries its .NET types, an interface as `any`, an
enum as its members (the vocabulary's union, a number when it is [Flags], a string otherwise), and a
delegate as its function. One rule SHALL decide it on every path that writes an annotation: a class's
members and parameters, a record's members, parameters and setters, a local, the item type of an empty
list, and a local function's parameters. A record's static that starts as null SHALL be annotated with
its type and the null.

#### Scenario: An event of exceptions and lists of an interface and of exceptions

- **WHEN** a class declares `event Action<Exception>? Failed` and a method declares
  `new List<Exception>()` and `new List<IDisposable>()`
- **THEN** the twin annotates `(exception: Error) => void`, `Error[]` and `any[]`

#### Scenario: A record and a method that reach every path

- **WHEN** a record declares an `Exception`, an interface, an app enum and a delegate `Measure` among
  its members, and a method declares an `IThing` local, a null `Exception?`, an empty `List<Action>`
  and a local function taking an exception
- **THEN** no annotation of the twin names `Exception`, `IThing`, the enum, `Measure` or `Action`, and
  they read `Error`, `any`, `string`, `(text: string) => number` and `(() => void)[]`

### Requirement: A null-conditional read answers null where its value is used

A null-conditional read that C# answers with `null` SHALL answer `null` in the browser wherever its
value is used, never `undefined`: assigned, passed, returned, stored or compared. Where nothing can
tell the two apart (a call that returns nothing, a statement that discards the value, the left of a
`??`, the tail of another null-conditional) the translation MAY stay JavaScript's optional chain.

#### Scenario: A dictionary that looks for null

- **WHEN** a value read as `p?.Name` with `p` null is stored in a dictionary, and the code asks
  `ContainsValue(null)`
- **THEN** it answers true, as in .NET

#### Scenario: A chain of two

- **WHEN** `b?.Next?.Tag` is assigned to a local
- **THEN** the local holds null when either link is null, the chain answering once where it ends

#### Scenario: A call that returns nothing

- **WHEN** `b?.Ring()` calls a method that returns `void`
- **THEN** it stays a bare optional chain, with no value to answer

### Requirement: A method group reads its receiver once

A method group bound to its receiver SHALL evaluate the receiver once, when the delegate is made, as C#
does. A receiver whose second read nothing can observe, a plain name or `this`, MAY be written twice.

#### Scenario: A receiver that is a call

- **WHEN** `Func<int> read = c.Make().Value` is made and called, where `Make` counts its calls
- **THEN** `Make` has run once, as in .NET
