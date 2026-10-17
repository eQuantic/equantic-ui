## ADDED Requirements

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

## MODIFIED Requirements

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
