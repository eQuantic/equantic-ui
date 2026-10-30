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

#### Scenario: Every spelling of a code unit

- **WHEN** each of `'\a'`, `'\e'`, `'\x7'`, `'\x007'`, `'\x0041'`, `'\0'`, `' '` and `'A'` runs alone as
  `var s = "" + <literal>; return s.Length + ":" + (int)s[0];`
- **THEN** each answers a length of 1 and the code .NET reads, `7`, `27`, `7`, `7`, `65`, `0`, `32` and `65`,
  where JavaScript read the token text of `'\x7'` as a syntax error, of `'\x007'` as two characters and of
  `'\x0041'` as three

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
list, a local function's parameters and a lambda's. A lambda's parameter SHALL be annotated as a member
of its type is: a `long` or a `ulong` as `bigint`, and a `decimal` as `Decimal`, which the module imports. A record's static that starts as null SHALL be annotated with
its type and the null. The build context, which C# names `ComponentContext`, SHALL be annotated
`BuildContext`, the name the runtime exports to every module, and a module that writes it SHALL import
it; its `typeScale` SHALL be the number C# holds. An app's own type named `BuildContext` SHALL be the
app's: no module SHALL import that name from the runtime for it.

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

#### Scenario: A helper class that takes the build context

- **WHEN** a static class of the shared library declares a method that takes a `ComponentContext` and
  scales a `TypeStyle`'s line height by its `TypeScale`
- **THEN** the twin annotates the parameter `BuildContext`, imports it, and passes the runtime's type
  check

#### Scenario: An app's own class named BuildContext

- **WHEN** an app declares a class named `BuildContext`
- **THEN** its module declares the class, and imports nothing of that name from the runtime

#### Scenario: A lambda over a long and one over a decimal

- **WHEN** a component of the shared library's fixtures maps a `List<long>` with `tick => tick * 2L`
  and averages a `List<decimal>` with `each => each`
- **THEN** the twin annotates `(tick: bigint)` and `(each: Decimal)`, imports `Decimal`, and passes the
  runtime's type check

### Requirement: An enum member is its name, whatever it is called

An enum member SHALL be written as its enum's representation whatever its name, `Value` and `HasValue`
included. Those two names SHALL be taken for `Nullable<T>`'s members only where the semantic model
cannot say what they are.

#### Scenario: Members named like Nullable's

- **WHEN** a method compares `reading == Reading.Value` and `reading == Reading.HasValue`, where
  `Reading` is an enum
- **THEN** the twin compares with `'value'` and `'hasValue'`, and reads no `Reading` object

#### Scenario: Nullable's own

- **WHEN** a method reads `count.HasValue` and `count.Value` of an `int?`
- **THEN** neither is read as a member in the browser

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

#### Scenario: A chain behind a guard

- **WHEN** `p?.Name.Pick()?.Length` is stored, where `Pick` is an extension that answers null, so the
  outer link is a guard rather than a chain
- **THEN** the stored value is null, as in .NET

#### Scenario: A call that returns nothing

- **WHEN** `b?.Ring()` calls a method that returns `void`
- **THEN** it stays a bare optional chain, with no value to answer

### Requirement: A method group reads its receiver once

A method group bound to its receiver SHALL evaluate the receiver once, when the delegate is made, as C#
does. A receiver whose second read nothing can observe, a plain name or `this`, MAY be written twice.

#### Scenario: A receiver that is a call

- **WHEN** `Func<int> read = c.Make().Value` is made and called, where `Make` counts its calls
- **THEN** `Make` has run once, as in .NET

### Requirement: A method group on base or of an extension binds what C# binds

A method group on `base` SHALL call the base's method on this object, and a method group of an
extension method the build compiles SHALL be bound on the home its call goes to, with the receiver
first and read once. A group of an extension no source of the build declares SHALL be the delegate
its call is, and SHALL fail the build with that call's refusal where the call has no translation.

#### Scenario: A group on base

- **WHEN** `Func<string> quiet = base.Sound` is made in a class that overrides `Sound`, and called
- **THEN** it answers the base's `Sound`, and the module that holds it parses

#### Scenario: A group of an extension

- **WHEN** `Func<string> twice = s.Twice` is made over an extension method `Twice`, and called
- **THEN** it answers what `s.Twice()` answers, and a receiver that is a call has run once

#### Scenario: A group of an extension of the platform

- **WHEN** `Func<bool> any = list.Any` is made over the BCL's `Enumerable.Any`, and called
- **THEN** it answers what `list.Any()` answers, where the build failed with EQ2004

#### Scenario: A group of an extension nothing emits

- **WHEN** `Func<IEnumerable<int>> shuffled = list.Shuffle` is made over the BCL's `Enumerable.Shuffle`
- **THEN** the build fails with EQ2004 naming `System.Linq.Enumerable.Shuffle`

### Requirement: A range over a type with a Slice calls its Slice

A range used as an index over a type eqc writes, with a `Length` or a `Count` and a
`Slice(int start, int length)`, SHALL call that `Slice` as C# lowers the range: with the start and the
length computed from the endpoints and the count, the receiver evaluated once, then the endpoints in
the order they are written, then the count, read only where an endpoint counts from the end or the end
is left open. A range handed to an indexer that takes the `Range` itself SHALL fail the build with
EQ2004, since a System.Range value has no translation. A range over a string, an array or a list SHALL
keep answering as it does.

#### Scenario: A range over a type with Slice

- **WHEN** browser-side code computes `new Strip()[1..3]` over a type whose `Slice(start, length)` copies five elements
- **THEN** the slice holds 2 elements, `2,3`, as in .NET, where `slice(1, 3)` held three

#### Scenario: The order a range is read in

- **WHEN** browser-side code computes `Strip.R(s)[^Strip.At("A", 3)..^Strip.At("B", 1)]`, each call logging its step and `Length` logging `L`
- **THEN** the log is `RABL` before the slice, as in .NET

#### Scenario: An indexer over Range

- **WHEN** a component computes `new Ranged()[1..3]` over a type that declares `this[Range r]`
- **THEN** the build fails with EQ2004 at the range

### Requirement: A ToString method group writes what its call writes

The `ToString` method group of a value whose type writes its text SHALL write what the call writes, of
the value as it is when the delegate is made: an array's, a list's, a dictionary's, a set's and a
delegate's type name (`System.Int32[]`), an enum's member name, and a tuple's, an anonymous object's
and a data value's text. Its receiver SHALL be read once, when the delegate is made, and a null
reference SHALL be refused there with the NullReferenceException .NET throws.

#### Scenario: An array's group

- **WHEN** `var xs = new[] { 1, 2 }; Func<string> text = xs.ToString; xs = new[] { 3 };` and `text()` runs
- **THEN** it writes `System.Int32[]`, as in .NET, where the browser wrote `1,2`

#### Scenario: An enum's group

- **WHEN** `Func<string> f = Scale.Large.ToString` is made and called
- **THEN** it writes `Large`, as in .NET

#### Scenario: A receiver that is a call

- **WHEN** `Func<string> t = Make().ToString` is made and called twice, where `Make` counts its calls
- **THEN** `Make` has run once, and both calls write `System.Int32[]`

### Requirement: A nullable tuple's element reads through the guard

An element of a nullable tuple read through `?.`, by its declared name or by its position, SHALL
answer the element, and null when the tuple is null, as in C#.

#### Scenario: By name and by position

- **WHEN** browser-side code computes `t?.A ?? -1` and `t?.Item2 ?? -1` for `(int A, int B)? t = (1, 2)`
- **THEN** they are 1 and 2, as in .NET; in the browser `t?.A` read undefined

#### Scenario: A null tuple

- **WHEN** browser-side code computes `t?.B ?? -1` for a null `(int A, int B)? t`
- **THEN** it is -1, as in .NET

### Requirement: A method group of a member no twin carries is the delegate its call is

A method group of a member the browser has no twin of, a .NET type's or a package's, SHALL be the
delegate its call is: a function over the method's own parameters, which are the delegate's, whose
body is the call those parameters make, translated as that call is wherever it is written. No argument
the caller of the delegate passes beyond the delegate's parameters SHALL reach the call. The receiver
SHALL be read once, where the delegate is made, and a null one SHALL be refused there, as .NET refuses
to make the delegate: with an ArgumentException for a method .NET calls directly and a
NullReferenceException for a virtual one. A group whose call has no translation SHALL fail the build
with the call's own refusal, reported where the group is written, and a group whose method hands a
value back through an `out` or a `ref` parameter SHALL fail the build with EQ2004.

#### Scenario: A reader handed to Select, in the page's culture

- **WHEN** browser-side code in pt-BR runs
  `new List<string> { "2024-01-15", "2024/01/15 21:30", "01/02/2024" }.Select(DateTime.Parse)` and writes
  each date as `ToString("o")`
- **THEN** the third is the first of February, as in .NET; the index `Select` hands its callback is
  never read as the invariant culture's flag

#### Scenario: A TimeSpan's ToString as a delegate

- **WHEN** browser-side code runs `var t = new TimeSpan(1, 2, 3, 4, 500); Func<string, string> f = t.ToString;
  t = TimeSpan.Zero; return f("g");`
- **THEN** it is `1:2:03:04.5`, as in .NET, written from the value the delegate was made with

#### Scenario: Math.Round as a delegate

- **WHEN** browser-side code runs `Func<double, double> round = Math.Round;` over 0.5, 1.5, 2.5, -0.5 and -1.5
- **THEN** they are 0, 2, 2, -0 and -2, as in .NET

#### Scenario: A null receiver

- **WHEN** browser-side code makes `Func<string> f = s.ToUpper` and `Func<int, bool> g = l.Contains` with
  `s` a null string and `l` a null `IList<int>`
- **THEN** the first throws ArgumentException with "Delegate to an instance method cannot have null
  'this'." and the second NullReferenceException, where each delegate is made

#### Scenario: A group whose call has no translation

- **WHEN** a component writes `Func<string, string, IFormatProvider, TimeSpan> read = TimeSpan.ParseExact`
- **THEN** the build fails with EQ2004 naming `System.TimeSpan.ParseExact` at the group, and the module
  names no `parseExact`

#### Scenario: A group that hands a value back through an out

- **WHEN** a component writes `SpanReader read = TimeSpan.TryParse`, with
  `delegate bool SpanReader(string text, out TimeSpan value)`
- **THEN** the build fails with EQ2004 naming `System.TimeSpan.TryParse`

### Requirement: A delegate built with new is the function it is built from

`new D(x)`, where `D` is a delegate type, SHALL be the function `x` is where `x` is a method group or
a lambda. Where `x` is another delegate, it SHALL be a new function over `x`'s `Invoke`, as .NET builds
it, and never `x` itself: `x` read once where the new one is made, and a null `x` refused there with
the ArgumentException .NET throws. Where `D`'s parameters hand a value back through an out or a ref, a
delegate built from another SHALL fail the build with EQ2004.

#### Scenario: Three delegates built with new

- **WHEN** browser-side code runs `var parse = new Func<string, DateTime>(DateTime.Parse);
  var twice = new Func<int, int>(x => x * 2); var again = new Func<int, int>(twice);` and calls each
- **THEN** they answer as in .NET, where `new Func(…)` named a class nothing defines

#### Scenario: A delegate built from another

- **WHEN** browser-side code makes `var g = new Func<int, int>(f);` over a delegate `f`, compares `g == f`,
  points `f` at another function and calls `g`, and builds one over a null delegate
- **THEN** `g == f` is False, `g` calls the `f` it was made over, and the null one throws ArgumentException
  "Delegate to an instance method cannot have null 'this'." where it is made, as in .NET, where `g` was
  `f` itself

### Requirement: A struct's operator over nullable values is lifted as C# lifts it

An operator applied over nullable values where C# lifts it, a record struct's equality, the equality of
a value the browser holds as data and an operator or a conversion a struct declares, SHALL answer as
C#'s lifted operator answers. An equality SHALL find two absent values equal and an absent value unequal
to any value, without the operator, and SHALL compare two values by the struct's own equality: a record
struct's member by member, a struct's by the `==` or `!=` it declares. A relation SHALL be false where a
value is absent. Any other operator, a unary one, a compound assignment and a step included, and a
user-defined conversion from a nullable value to a nullable value, SHALL answer null for an absent value,
the operator running only over a value. The operands SHALL be evaluated once each, in order. An operator
declared over the nullable values themselves SHALL be called as it is written, a null included, and an
element of a nullable type in a tuple's equality SHALL compare through the same lift.

#### Scenario: Two nullable record structs built apart

- **WHEN** browser-side code compares `Spot? a = new Spot(1, 2), b = new Spot(1, 2);` with `a == b` and
  `a != b`, for `record struct Spot(int Row, int Col)`, and keeps `var before = target;`, builds
  `target = new Spot(1, 2);` again and answers `target != before`
- **THEN** they are True, False and False, as in .NET, where the browser compared two references

#### Scenario: An absent record struct

- **WHEN** browser-side code compares `Spot? a = null, b = null;` and `Spot? c = new Spot(1, 2), d = null;`
  with `a == b` and `c == d`
- **THEN** they are True and False, as in .NET

#### Scenario: The spreadsheet's range

- **WHEN** a class of an app keeps a `SheetRange?`, rebuilds it for a row and answers whether it moved
  with `Target != before`, built as an app's build builds it, in TypeScript and in JavaScript
- **THEN** the same row is no move and another row is one, as in .NET, where every move was one

#### Scenario: A struct's own equality over nullable values

- **WHEN** `struct Reach` declares `==` and `!=` over `Start` alone, and browser-side code compares
  `Reach? a = new Reach(1, 5), b = new Reach(1, 9);`, `Reach? c = null, d = null;` and
  `Reach? e = new Reach(1, 5), f = null;` with `==`
- **THEN** they are True, True and False, as in .NET, the operator called for the two values only, where
  the browser handed it the null and threw

#### Scenario: A relation and the other operators of a struct

- **WHEN** browser-side code computes `a < b`, `a + b` and `-a` for `Reach? a = null, b = new Reach(2, 0);`,
  and `m += new Reach(2, 0)` for a null `Reach? m`
- **THEN** they are False, null, null and null, as in .NET

#### Scenario: A conversion between two nullable types

- **WHEN** `record struct Cents(int Amount)` declares `implicit operator Cents(int)` and
  `explicit operator int(Cents)`, and browser-side code computes `Cents? c = n;` and `(int?)c` for a null
  `int? n` and a null `Cents? c`
- **THEN** both are null, as in .NET, where the browser built a `Cents` holding null and threw

#### Scenario: An operator declared over the nullable values

- **WHEN** `struct Probe` declares `operator ==(Probe? a, Probe? b)`, which reads a null as -1, and
  browser-side code compares `Probe? a = null, b = new Probe(-1);` with `a == b`
- **THEN** it is True, the operator's own answer, as in .NET

#### Scenario: A tuple's nullable element

- **WHEN** browser-side code compares `(Reach?, int) a = (null, 1), b = (null, 1);` with `a == b`
- **THEN** it is True, as in .NET, where the browser handed `Reach`'s operator the nulls and threw

### Requirement: A nullable struct's own member is read as the struct's

A member a struct declares, read on a nullable value of the struct through `?.` or in a property pattern,
SHALL be read as the struct's own member, whatever its name, `Value`, `HasValue`, `GetValueOrDefault` and
`Count` included, and SHALL answer null through `?.` for an empty value. `Nullable<T>`'s own `Value`,
`HasValue` and `GetValueOrDefault` SHALL be taken by the member the semantic model binds, and by their
names only where the model cannot say. A `Count` read on a nullable value SHALL be its value's, since
`Nullable<T>` declares none.

#### Scenario: A struct's own Value and HasValue through ?.

- **WHEN** `struct Probe` declares `int Value` and `bool HasValue => Value > 0`, and browser-side code
  computes `a?.Value` and `a?.HasValue` for `Probe? a = new Probe(0)`
- **THEN** they are 0 and False, as in .NET, where the browser answered the `Probe` itself and True

#### Scenario: A record struct's own Count

- **WHEN** browser-side code computes `c?.Count` and `c is { Count: 4 }` for `Cents? c = new Cents(4)`,
  where `record struct Cents(int Count)`
- **THEN** they are 4 and True, as in .NET, where the browser read a `length` no struct has

#### Scenario: A vocabulary value and the spreadsheet's

- **WHEN** browser-side code computes `s?.Value` for `SizeValue? s = SizeValue.Fixed(10)`, and a class of
  an app answers `Last?.Value` for a `SheetCellSnapshot?` recorded with `"x"`, built as an app's build
  builds it, in TypeScript and in JavaScript
- **THEN** they are 10 and `"x"`, as in .NET

#### Scenario: Nullable's own members

- **WHEN** browser-side code computes `a.HasValue` and `a.Value.Value` for `Probe? a = new Probe(3)`, and
  `b.GetValueOrDefault(new Probe(7)).Value` for an empty `Probe? b`
- **THEN** they are True, 3 and 7, as in .NET

### Requirement: An empty nullable unwrapped to a value throws

A nullable value unwrapped to a type that is not nullable SHALL throw `InvalidOperationException` with
.NET's message, "Nullable object must have a value.", when it is empty, before anything converts it,
wherever C# unwraps it: `Value`, a cast to the value's type or to another (a number, a char, a decimal, a
struct, an enum), the conversion C# applies around the value a struct's own conversion operator takes, and
a `foreach` element of a type that is not nullable. The value SHALL be read once. A present value SHALL
convert as it did, and a lifted conversion, to a nullable type, SHALL still answer null for null.

#### Scenario: A struct's own conversion operator

- **WHEN** `record struct Cents(int Count)` declares `explicit operator Cents(int value)`, and browser-side
  code computes `(Cents)n` for an empty `int? n`, catching `InvalidOperationException`
- **THEN** it is caught with "Nullable object must have a value.", as in .NET, where the browser built a
  `Cents` holding null; for `int? n = 5`, `((Cents)n).Count` is 5

#### Scenario: A cast and Value

- **WHEN** browser-side code computes `(int)n`, `(long)n`, `n.Value`, `(Level)e` and `(int)e` for an empty
  `int? n` and an empty `Level? e`, where `Level` is an enum
- **THEN** each throws `InvalidOperationException`, as in .NET, where the browser carried the null on

#### Scenario: A foreach over nullable elements

- **WHEN** browser-side code adds up `foreach (int x in xs)` over `new List<int?> { 1, null }`
- **THEN** the first element is added and the second throws `InvalidOperationException`, as in .NET

#### Scenario: A lifted conversion keeps its null

- **WHEN** browser-side code computes `(Cents?)n` and `long? l = n` for an empty `int? n`
- **THEN** both are null, as in .NET

### Requirement: A params parameter takes what C# hands it

eqc SHALL pass a params parameter what the bound tree says the call hands it: each element a call lists,
as it is, after a named argument too, or a collection handed whole, spread into the rest parameter the
parameter is on the other side, named or not. An array handed to a params parameter of another element
type SHALL be one element where C# makes it one. A C# 13 params collection SHALL be read as an array is.
This SHALL hold for a call, an app type's constructor and a vocabulary type's constructor alike.

#### Scenario: A collection handed whole

- **WHEN** `record FontFamily(string Name, params IReadOnlyList<FontFile> Files)` is built as
  `new FontFamily("Brand", [regular, italic])`
- **THEN** `Files.Count` is `2`, as in .NET

#### Scenario: Elements listed after a named argument

- **WHEN** `static string Tagged(string tag, params int[] xs)` is called as `Tagged(tag: "t", 1, 2, 3)`
- **THEN** `xs.Length` is `3`, as in .NET

#### Scenario: An array of ints handed to params object[]

- **WHEN** `static int Objects(params object[] xs) => xs.Length` is called with an `int[]` of two
- **THEN** it answers `1`, as in .NET

#### Scenario: A params collection listed at a constructor

- **WHEN** a class whose constructor takes `params IReadOnlyList<int>` is built as `new Bag(1, 2, 3)`
- **THEN** the build completes and the constructor reads three elements, as in .NET

### Requirement: A pattern reads its input once

The value an `is` pattern tests SHALL be evaluated once, whatever the pattern's shape (`or`, `and`, `not`,
relational, type, property, positional and list patterns, a binding, and their combinations), and so SHALL
the value of `x is Type`: a call, a property whose getter has an effect, and a member of `this` alike. An
input the pattern names nothing of SHALL be evaluated all the same. A switch expression and a switch
statement SHALL evaluate their input once.

#### Scenario: A getter through an or

- **WHEN** `var c = new Counter(); var hit = c.Value is 2 or 1; return c.Reads * 10 + (hit ? 1 : 0);` runs,
  `Value` a property whose getter counts its reads in `Reads` and answers 1
- **THEN** it answers 11, as .NET does, where the getter ran twice and the answer was 21

#### Scenario: A getter through a binding

- **WHEN** `var c = new Counter(); return c.Value is > 0 and var v ? c.Reads * 10 + v : -1;` runs
- **THEN** it answers 11, as .NET does

#### Scenario: A getter through the type test

- **WHEN** `var c = new Counter(); var hit = c.Text is char; return c.Reads * 10 + (hit ? 1 : 0);` runs,
  `Text` a getter that counts its reads and answers `"xy"`
- **THEN** it answers 10, as .NET does, where the getter ran to ask the type and again to ask the length

#### Scenario: A call through a property pattern

- **WHEN** `int n = 0; Pt Make() { n++; return new Pt(1, 2); } var hit = Make() is { X: 1, Y: 2 }; return n * 10 + (hit ? 1 : 0);` runs
- **THEN** it answers 11, as .NET does

#### Scenario: An input the pattern names nothing of

- **WHEN** `int n = 0; System.Func<int> f = () => { n++; return 1; }; var hit = f() is var _; return n * 10 + (hit ? 1 : 0);` runs
- **THEN** it answers 11, as .NET does, where `f` was never called and the answer was 1

#### Scenario: An input behind an alternative that matches everything

- **WHEN** `int n = 0; System.Func<int> f = () => { n++; return 1; }; var hit = f() is var _ or 2; return n * 10 + (hit ? 1 : 0);` runs
- **THEN** it answers 11, as .NET does, where the input's one read sat behind `true ||` and never ran

### Requirement: A property pattern reads its member as a member access reads it

eqc SHALL translate the read of a member a property pattern names exactly as it translates a member access
of the same member on a value of the same type, through the same strategies, at any depth: in a pattern
nested in another, in a positional part, in a list's element and in each member of an extended path. The
type of the value a pattern reads from SHALL be the one the bound pattern narrows to. A .NET member no value
in the browser carries SHALL be refused in a pattern with EQ2004, as its member access is.

#### Scenario: A count in a nested pattern

- **WHEN** `h is { Roles: { Count: 1 } }` and `h is { Inner: { Count: 2 } }` run, where `Roles` is a
  `HashSet<int>` holding one element and `Inner` is a `record Stats(int Count)` built with 2
- **THEN** both answer True, as .NET does

#### Scenario: A tuple's element

- **WHEN** `(1, 2) is { Item1: 1, Item2: 2 }` runs
- **THEN** it answers True, as .NET does

#### Scenario: A dictionary's keys and values

- **WHEN** `map is { Keys.Count: 1, Values.Count: 1 }` runs over a dictionary holding one entry
- **THEN** it answers True, as .NET does

#### Scenario: A member no browser value carries

- **WHEN** browser-side code tests `l is { Capacity: 8 }` for a `List<int>`
- **THEN** the build fails with EQ2004, as it fails for `l.Capacity`

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
